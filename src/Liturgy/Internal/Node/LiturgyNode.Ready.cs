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
            callback?.Invoke(_root);
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
            callback(_root);
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
            var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
            return _ => del();
        }
        
        var paramType = parameters[0].ParameterType;
        
        if (paramType == typeof(Window)) return (Action<Window>)Delegate.CreateDelegate(typeof(Action<Window>), method);
        
        if (paramType == typeof(Viewport))
        {
            var del = (Action<Viewport>)Delegate.CreateDelegate(typeof(Action<Viewport>), method);
            return root => del(root);
        }
        
        if (paramType == typeof(Node))
        {
            var del = (Action<Node>)Delegate.CreateDelegate(typeof(Action<Node>), method);
            return root => del(root);
        }
        
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        LiturgyReport.AddInvalidParameterError(methodName, "OnReady (RootReady/Immediate mode, no target)", parameters[0].Name ?? "parameters[0]", paramType, "Window, Viewport, or Node");
        
        return null;
    }

    private void SubscribeToTargetedNodes()
    {
        foreach (var (targetPath, callbacks) in _onReadyTargetedCallbacks)
        {
            var node = _root.GetNodeOrNull(targetPath);
            
            if (node is null)
            {
                // Node doesn't exist yet, watch for it
                void OnNodeAdded(Node addedNode)
                {
                    if (addedNode.GetPath().ToString() != targetPath)
                    {
                        // Also check if the target is now resolvable
                        var resolved = _root.GetNodeOrNull(targetPath);
                        if (resolved is null) return;
                        addedNode = resolved;
                    }
                    
                    GetTree().NodeAdded -= OnNodeAdded;
                    SubscribeToTargetNode(addedNode, callbacks);
                }
                
                GetTree().NodeAdded += OnNodeAdded;
            }
            else
            {
                SubscribeToTargetNode(node, callbacks);
            }
        }
    }

    private static void SubscribeToTargetNode(Node node, List<Action<Node>> callbacks)
    {
        if (node.IsNodeReady())
        {
            foreach (var callback in callbacks) callback(node);
        }
        else
        {
            node.Ready += () =>
            {
                foreach (var callback in callbacks)
                {
                    callback(node);
                }
            };
        }
    }

    private void SubscribeToExistingNodesForPerNodeReady(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            SubscribeNodeForPerNodeReady(node);

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }
    }

    private void OnNodeAddedForPerNodeReady(Node node)
    {
        if (IsInternalNode(node)) return;
        SubscribeNodeForPerNodeReady(node);
    }

    private void SubscribeNodeForPerNodeReady(Node node)
    {
        if (IsInternalNode(node) || !_subscribedForReady.Add(node)) return;

        // Check if any PerNode callbacks match this node type
        var matchingCallbacks = _onReadyPerNodeCallbacks
            .Where(entry => entry.NodeType is null || entry.NodeType.IsInstanceOfType(node))
            .ToList();
        
        if (matchingCallbacks.Count == 0) return;

        var path = node.GetPath().ToString();

        LiturgyReport.IncrementReadySubscriptions();

        if (node.IsNodeReady()) FirePerNodeReadyCallbacks(node, path, matchingCallbacks);
        else node.Ready += () => FirePerNodeReadyCallbacks(node, path, matchingCallbacks);
    }

    private static void FirePerNodeReadyCallbacks(Node node, string path, List<(Action<Node> Callback, HashSet<string>? Excludes, Type? NodeType)> callbacks)
    {
        foreach (var (callback, excludes, _) in callbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            callback(node);
        }
    }
}

