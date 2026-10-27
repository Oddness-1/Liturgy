using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    private void SetupOnReadyCallbacks()
    {
        var methods = ScanForMethods<OnReady>();
        
        // Immediate mode entries (fire during Liturgy._Ready, before /root is ready)
        var immediateEntries = new List<(MethodInfo Method, int Priority)>();
        
        // RootReady mode entries
        var rootReadyImmediate = new List<(MethodInfo Method, int Priority)>();
        var rootReadyTargeted = new Dictionary<string, List<(MethodInfo Method, int Priority, Type? NodeType)>>();
        
        // PerNode mode entries
        var perNodeEntries = new List<(MethodInfo Method, int Priority, Type? NodeType, HashSet<string>? Excludes)>();

        foreach (var (method, attr) in methods)
        {
            if (!ValidateNodeParamMethod(method, "OnReady", out var nodeType))
                continue;

            var mode = attr.Mode;
            var target = attr.Target;
            var exclude = attr.Exclude;
            
            // Warn if Target is set in PerNode mode
            if (mode == ReadyMode.PerNode && target is not null)
            {
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] {method.DeclaringType?.FullName}.{method.Name}: Target is ignored in PerNode mode. Use Exclude for filtering, or use RootReady mode.");
            }
            
            // Warn if Target is set in Immediate mode
            if (mode == ReadyMode.Immediate && target is not null)
            {
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] {method.DeclaringType?.FullName}.{method.Name}: Target is ignored in Immediate mode. Use RootReady mode for targeted callbacks.");
            }
            
            var excludeSet = ParseExcludeSet(exclude);

            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var modeStr = mode switch
            {
                ReadyMode.RootReady => "RootReady",
                ReadyMode.Immediate => "Immediate",
                ReadyMode.PerNode => "PerNode",
                _ => "Unknown"
            };
            var typeFilterStr = nodeType?.Name;
            
            // Report to Markdown log
            LiturgyReport.AddOnReadyHook(
                methodName,
                modeStr,
                target,
                exclude,
                attr.Priority,
                typeFilterStr);
            
            // Check for dangerous patterns
            if (mode == ReadyMode.PerNode && (nodeType is null || nodeType == typeof(Node)))
            {
                LiturgyReport.AddWarning(
                    $"**PerNode OnReady without type filter**\n" +
                    $"`{methodName}` — Subscribes to Ready signal on EVERY node in the scene.\n" +
                    $"Consider adding a typed parameter to filter which nodes trigger this callback.");
            }

            switch (mode)
            {
                case ReadyMode.Immediate:
                    immediateEntries.Add((method, attr.Priority));
                    break;
                    
                case ReadyMode.RootReady:
                    if (target is null)
                    {
                        // RootReady mode, no target - fires once when /root is ready
                        rootReadyImmediate.Add((method, attr.Priority));
                    }
                    else
                    {
                        // RootReady mode with target - fires once per specified node
                        var targets = target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var t in targets)
                        {
                            var normalizedTarget = NormalizePath(t);
                            if (!rootReadyTargeted.ContainsKey(normalizedTarget))
                                rootReadyTargeted[normalizedTarget] = [];
                            rootReadyTargeted[normalizedTarget].Add((method, attr.Priority, nodeType));
                        }
                    }
                    break;
                    
                case ReadyMode.PerNode:
                    perNodeEntries.Add((method, attr.Priority, nodeType, excludeSet));
                    break;
            }
        }

        // === IMMEDIATE MODE ===
        // Fire immediately during Liturgy._Ready(), before /root is ready
        foreach (var (method, _) in immediateEntries.OrderBy(e => e.Priority))
        {
            var callback = CreateWindowCallback(method);
            if (callback is not null) RiteCall.Run(callback, _root);
        }

        // === ROOTREADY MODE (no target) ===
        // Store callbacks to fire when /root is ready
        foreach (var (method, _) in rootReadyImmediate.OrderBy(e => e.Priority))
        {
            var callback = CreateWindowCallback(method);
            if (callback is not null) _onReadyRootReadyCallbacks.Add(callback);
        }

        // === ROOTREADY MODE (with target) ===
        // Process targeted callbacks
        foreach (var (targetPath, entries) in rootReadyTargeted)
        {
            if (!_onReadyTargetedCallbacks.ContainsKey(targetPath)) 
                _onReadyTargetedCallbacks[targetPath] = [];
                
            foreach (var (method, _, nodeType) in entries.OrderBy(e => e.Priority))
            {
                var callback = CreateNodeCallback(method, nodeType);
                if (callback is not null) _onReadyTargetedCallbacks[targetPath].Add(callback);
            }
        }

        // === PERNODE MODE ===
        // Process PerNode mode callbacks
        foreach (var (method, _, nodeType, excludeSet) in perNodeEntries.OrderBy(e => e.Priority))
        {
            var callback = CreateNodeCallback(method, nodeType);
            if (callback is not null) _onReadyPerNodeCallbacks.Add((callback, excludeSet, nodeType));
        }
        
        // Subscribe to all nodes for PerNode mode
        if (_onReadyPerNodeCallbacks.Count > 0)
        {
            SubscribeToExistingNodesForPerNodeReady(_root);
            GetTree().NodeAdded += OnNodeAddedForPerNodeReady;
        }

        // === SUBSCRIBE TO /root READY ===
        // When /root is ready: execute AssignNode, then fire RootReady callbacks, then setup targeted nodes
        if (_root.IsNodeReady())
        {
            OnRootReady();
        }
        else
        {
            _root.Ready += OnRootReady;
        }
    }

    private void OnRootReady()
    {
        // 1. Execute AssignNode assignments first
        ExecuteAssignNodeEntries();
        
        // 2. Fire RootReady (no target) callbacks
        foreach (var callback in _onReadyRootReadyCallbacks)
        {
            RiteCall.Run(callback, _root);
        }
        
        // 3. Subscribe to targeted nodes for RootReady mode
        if (_onReadyTargetedCallbacks.Count > 0)
        {
            SubscribeToTargetedNodes();
        }
    }

    private static Action<Window>? CreateWindowCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        
        if (parameters.Length == 0)
        {
            var del = (Action)RiteProbe.Create(typeof(Action), method);
            return _ => del();
        }
        
        var paramType = parameters[0].ParameterType;
        
        if (paramType == typeof(Window)) return (Action<Window>)RiteProbe.Create(typeof(Action<Window>), method);
        
        if (paramType == typeof(Viewport))
        {
            var del = (Action<Viewport>)RiteProbe.Create(typeof(Action<Viewport>), method);
            return root => del(root);
        }
        
        if (paramType == typeof(Node))
        {
            var del = (Action<Node>)RiteProbe.Create(typeof(Action<Node>), method);
            return root => del(root);
        }
        
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        LiturgyReport.AddInvalidParameterError(methodName, "OnReady (RootReady/Immediate mode, no target)", parameters[0].Name ?? "parameters[0]", paramType, "Window, Viewport, or Node");
        
        return null;
    }

    // Targeted OnReady follows its path like AssignNode: the node there when /root is ready gets it, and so does each new
    // node that later becomes ready at that path (after a scene change, say). A node moved there already ready doesn't.
    private void SubscribeToTargetedNodes()
    {
        foreach (var (targetPath, callbacks) in _onReadyTargetedCallbacks)
        {
            if (_root.GetNodeOrNull(targetPath) is { } node) SubscribeToTargetNode(node, callbacks, initial: true);

            // The last segment is the node's own name (a %unique name is the name too), so most added nodes are ruled out
            // without building their path.
            var name = targetPath[(targetPath.LastIndexOf('/') + 1)..].TrimStart('%');
            if (!_onReadyTargetNames.TryGetValue(name, out var paths)) _onReadyTargetNames[name] = paths = [];
            paths.Add(targetPath);
        }

        GetTree().NodeAdded += OnNodeAddedForTargetedReady;
    }

    private void OnNodeAddedForTargetedReady(Node node)
    {
        if (node.IsNodeReady() || !_onReadyTargetNames.TryGetValue(node.Name, out var paths)) return;

        foreach (var targetPath in paths)
        {
            if (_root.GetNodeOrNull(targetPath) == node) SubscribeToTargetNode(node, _onReadyTargetedCallbacks[targetPath], initial: false);
        }
    }

    private void SubscribeToTargetNode(Node node, List<Action<Node>> callbacks, bool initial)
    {
        if (IsShielded(node)) return;

        if (node.IsNodeReady())
        {
            if (!initial) return;
            foreach (var callback in callbacks) RiteCall.Run(callback, node);
            return;
        }

        node.Connect(Node.SignalName.Ready, Callable.From(() =>
        {
            foreach (var callback in callbacks) RiteCall.Run(callback, node);
        }), (uint)ConnectFlags.OneShot);
    }

    private void SubscribeToExistingNodesForPerNodeReady(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (IsShielded(node)) continue;
            SubscribeNodeForPerNodeReady(node, initial: true);

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }
    }

    private void OnNodeAddedForPerNodeReady(Node node) => SubscribeNodeForPerNodeReady(node, initial: false);

    // Like _Ready: once per node. Nodes already ready when Liturgy starts get it now; a node re-entering the tree is
    // already ready and doesn't. Only nodes still waiting for their ready are tracked, by id, so nothing is held after.
    private void SubscribeNodeForPerNodeReady(Node node, bool initial)
    {
        if (node.IsNodeReady() && !initial) return;
        if (IsHiddenFromRites(node)) return;

        List<(Action<Node> Callback, HashSet<string>? Excludes, Type? NodeType)>? matching = null;
        foreach (var entry in _onReadyPerNodeCallbacks)
        {
            if (entry.NodeType is null || entry.NodeType.IsInstanceOfType(node)) (matching ??= []).Add(entry);
        }

        if (matching is null) return;

        if (node.IsNodeReady())
        {
            LiturgyReport.IncrementReadySubscriptions();
            FirePerNodeReadyCallbacks(node, matching);
            return;
        }

        var id = node.GetInstanceId();
        if (!_pendingPerNodeReady.Add(id)) return;

        LiturgyReport.IncrementReadySubscriptions();
        node.Connect(Node.SignalName.Ready, Callable.From(() =>
        {
            _pendingPerNodeReady.Remove(id);
            FirePerNodeReadyCallbacks(node, matching);
        }), (uint)ConnectFlags.OneShot);
    }

    private static void FirePerNodeReadyCallbacks(Node node, List<(Action<Node> Callback, HashSet<string>? Excludes, Type? NodeType)> callbacks)
    {
        string? path = null;
        foreach (var (callback, excludes, _) in callbacks)
        {
            if (excludes is not null && PathMatchesAnyExclude(path ??= node.GetPath().ToString(), excludes)) continue;
            RiteCall.Run(callback, node);
        }
    }
}

