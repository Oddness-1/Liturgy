using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    private readonly List<Action<Window, double>> _processWhilePausedCallbacks = [];
    private readonly List<Action<Window, double>> _physicsProcessWhilePausedCallbacks = [];
    
    private readonly record struct ProcessCallbackEntry(
        Action<double, Node> Callback,
        HashSet<string>? Targets,
        HashSet<string>? Excludes,
        Type? NodeType,
        MethodInfo Method,
        string AttrName);

    private void SetupFrameCallbacks()
    {
        SetupProcessHook<OnProcess>(
            _processGlobalCallbacks,
            _processWhilePausedCallbacks,
            _processTargetedEntries,
            attr => attr.Mode,
            attr => attr.Target,
            attr => attr.Exclude,
            attr => attr.Priority,
            attr => attr.WhilePaused,
            "OnProcess",
            isPhysics: false
        );

        SetupProcessHook<OnPhysicsProcess>(
            _physicsProcessGlobalCallbacks,
            _physicsProcessWhilePausedCallbacks,
            _physicsProcessTargetedEntries,
            attr => attr.Mode,
            attr => attr.Target,
            attr => attr.Exclude,
            attr => attr.Priority,
            attr => attr.WhilePaused,
            "OnPhysicsProcess",
            isPhysics: true
        );

        // Enable our own processing only if we have global callbacks
        SetProcess(_processGlobalCallbacks.Count > 0);
        SetPhysicsProcess(_physicsProcessGlobalCallbacks.Count > 0);

        // The tree's frame signals fire every frame regardless of pause, unlike this node's own processing
        if (_processWhilePausedCallbacks.Count > 0) GetTree().ProcessFrame += OnProcessFrame;
        if (_physicsProcessWhilePausedCallbacks.Count > 0) GetTree().PhysicsFrame += OnPhysicsFrame;

        // If we have targeted entries, subscribe to existing nodes and watch for new ones
        if (_processTargetedEntries.Count <= 0 && _physicsProcessTargetedEntries.Count <= 0) return;

        var matchCounts = SubscribeToExistingNodesForProcess(_root);
        WarnForHighMatchCounts(matchCounts);
        GetTree().NodeAdded += OnNodeAddedForProcess;
    }

    private void WarnForHighMatchCounts(Dictionary<ProcessCallbackEntry, int> matchCounts)
    {
        const int WarningThreshold = 50;

        foreach (var (entry, count) in matchCounts)
        {
            var methodName = $"{entry.Method.DeclaringType?.FullName}.{entry.Method.Name}";
            
            // Seed the runtime tracking with initial counts
            _processEntryMatchCounts[methodName] = count;
            
            if (count < WarningThreshold)
                continue;

            var hasSuppression = entry.Method.GetCustomAttribute<SuppressWarnings>() is not null;
            if (hasSuppression)
            {
                // Mark as warned so we don't warn again at runtime
                _processEntryWarned.Add(methodName);
                continue;
            }

            _processEntryWarned.Add(methodName);
            LiturgyReport.AddConsoleWarning(
                $"[Liturgy] [{entry.AttrName}(PerNode)] '{methodName}' matched {count} nodes. " +
                $"Each creates a ProcessHook child that runs every frame. " +
                $"Consider narrowing with a type filter or Exclude pattern. " +
                $"Add [Rite.SuppressWarnings] to silence this.");
        }
    }

    private void SetupProcessHook<TAttr>(
        List<Action<Window, double>> globalCallbacks,
        List<Action<Window, double>> whilePausedCallbacks,
        List<ProcessCallbackEntry> targetedEntries,
        Func<TAttr, FrameMode> getMode,
        Func<TAttr, string?> getTarget,
        Func<TAttr, string?> getExclude,
        Func<TAttr, int> getPriority,
        Func<TAttr, bool> getWhilePaused,
        string attrName,
        bool isPhysics) where TAttr : Attribute
    {
        var methods = ScanForMethods<TAttr>();
        
        // Global mode, no target - fires once per frame
        var globalMethods = new List<(MethodInfo Method, int Priority, FrameSignature Signature, bool WhilePaused)>();
        
        // Global mode with target OR PerNode mode - uses ProcessHook
        var targetedMethods = new List<(MethodInfo Method, int Priority, FrameSignature Signature, HashSet<string>? Targets, HashSet<string>? Excludes, Type? NodeType)>();

        foreach (var (method, attr) in methods)
        {
            if (!ValidateFrameMethod(method, out var signature, out var nodeType))
                continue;

            var mode = getMode(attr);
            var target = getTarget(attr);
            var exclude = getExclude(attr);
            var priority = getPriority(attr);
            var whilePaused = getWhilePaused(attr);
            
            // Warn if Target is set in PerNode mode
            if (mode == FrameMode.PerNode && target is not null)
            {
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] {method.DeclaringType?.FullName}.{method.Name}: Target is ignored in PerNode mode. Use Exclude for filtering, or use Global mode.");
            }

            // Warn if WhilePaused is set where ProcessHooks (which follow their target's pause mode) do the work
            if (whilePaused && (mode == FrameMode.PerNode || target is not null))
            {
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] {method.DeclaringType?.FullName}.{method.Name}: WhilePaused only applies in Global mode without a Target, and is ignored here.");
            }

            var excludeSet = ParseExcludeSet(exclude);

            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var modeStr = mode == FrameMode.Global ? "Global" : "PerNode";
            var typeFilterStr = nodeType?.Name;
            
            // Report to Markdown log
            if (isPhysics)
            {
                LiturgyReport.AddOnPhysicsProcessHook(
                    methodName,
                    modeStr,
                    target,
                    exclude,
                    priority,
                    typeFilterStr);
            }
            else
            {
                LiturgyReport.AddOnProcessHook(
                    methodName,
                    modeStr,
                    target,
                    exclude,
                    priority,
                    typeFilterStr);
            }
            
            // Check for dangerous patterns: PerNode process without a type filter
            if (mode == FrameMode.PerNode && (nodeType is null || nodeType == typeof(Node)))
            {
                var hasSuppression = method.GetCustomAttribute<SuppressWarnings>() is not null;
                
                if (!hasSuppression)
                {
                    LiturgyReport.AddConsoleWarning(
                        $"[Liturgy] [{attrName}(PerNode)] '{methodName}' has no type filter — " +
                        $"this attaches a ProcessHook to EVERY node. " +
                        $"Add a typed parameter like 'MyNode node' to filter, or add [Rite.SuppressWarnings] to silence this.");
                }
            }

            if (mode == FrameMode.Global && target is null)
                // Global mode, no target - simple once-per-frame callback
                globalMethods.Add((method, priority, signature, whilePaused));
            
            else
            {
                // Global mode with target OR PerNode mode - needs ProcessHook
                HashSet<string>? targetSet = null;
                if (mode == FrameMode.Global && !string.IsNullOrEmpty(target))
                {
                    targetSet = target
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(NormalizePath)
                        .ToHashSet();
                }
                
                targetedMethods.Add((method, priority, signature, targetSet, excludeSet, nodeType));
            }
        }

        // Create global callbacks (fires once per frame with root), routed by pause behavior
        foreach (var (method, _, signature, whilePaused) in globalMethods.OrderBy(e => e.Priority))
        {
            var callback = CreateGlobalFrameCallback(method, signature);
            if (callback is null) continue;

            if (whilePaused) whilePausedCallbacks.Add(callback);
            else globalCallbacks.Add(callback);
        }

        // Store targeted entries for later attachment via ProcessHook
        foreach (var (method, _, signature, targetSet, excludeSet, nodeType) in targetedMethods.OrderBy(e => e.Priority))
        {
            var callback = CreateTargetedFrameCallback(method, signature);
            if (callback is not null) targetedEntries.Add(new ProcessCallbackEntry(callback, targetSet, excludeSet, nodeType, method, attrName));
        }
    }

    private static bool ValidateFrameMethod(MethodInfo method, out FrameSignature signature, out Type? nodeType)
    {
        signature = FrameSignature.NoParams;
        nodeType = null;
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";

        if (parameters.Length > 2)
        {
            LiturgyReport.AddMethodError(methodName, "OnProcess/OnPhysicsProcess", "Must have at most two parameters.");
            return false;
        }

        int? nodeParamIndex = null;
        int? deltaParamIndex = null;

        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            
            if (param.ParameterType == typeof(double))
            {
                if (deltaParamIndex is not null)
                {
                    LiturgyReport.AddMethodError(methodName, "OnProcess/OnPhysicsProcess", "Has multiple double parameters.");
                    return false;
                }
                deltaParamIndex = i;
            }
            else if (typeof(Node).IsAssignableFrom(param.ParameterType) || param.ParameterType.IsInterface)
            {
                if (nodeParamIndex is not null)
                {
                    LiturgyReport.AddMethodError(methodName, "OnProcess/OnPhysicsProcess", "Has multiple Node parameters.");
                    return false;
                }
                nodeParamIndex = i;
                nodeType = param.ParameterType;
            }
            else
            {
                LiturgyReport.AddInvalidParameterError(methodName, "OnProcess/OnPhysicsProcess", param.Name ?? "param", param.ParameterType, "double (delta) and/or Node (or derived type, or an interface nodes implement)");
                return false;
            }
        }

        signature = (nodeParamIndex, deltaParamIndex) switch
        {
            (null, null) => FrameSignature.NoParams,
            (null, not null) => FrameSignature.DeltaOnly,
            (not null, null) => FrameSignature.NodeOnly,
            (not null, not null) when nodeParamIndex < deltaParamIndex => FrameSignature.NodeDelta,
            (not null, not null) => FrameSignature.DeltaNode
        };

        return true;
    }

    private enum FrameSignature
    {
        NoParams,
        DeltaOnly,
        NodeOnly,
        NodeDelta,
        DeltaNode
    }

    private static Action<Window, double>? CreateGlobalFrameCallback(MethodInfo method, FrameSignature signature)
    {
        return signature switch
        {
            FrameSignature.NoParams => CreateGlobalFrameCallback_NoParams(method),
            FrameSignature.DeltaOnly => CreateGlobalFrameCallback_DeltaOnly(method),
            FrameSignature.NodeOnly => CreateGlobalFrameCallback_NodeOnly(method),
            FrameSignature.NodeDelta => CreateGlobalFrameCallback_NodeDelta(method),
            FrameSignature.DeltaNode => CreateGlobalFrameCallback_DeltaNode(method),
            _ => null
        };
    }

    private static Action<Window, double> CreateGlobalFrameCallback_NoParams(MethodInfo method)
    {
        var del = (Action)RiteProbe.Create(typeof(Action), method);
        return (_, _) => del();
    }

    private static Action<Window, double> CreateGlobalFrameCallback_DeltaOnly(MethodInfo method)
    {
        var del = (Action<double>)RiteProbe.Create(typeof(Action<double>), method);
        return (_, delta) => del(delta);
    }

    private static Action<Window, double>? CreateGlobalFrameCallback_NodeOnly(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var paramType = parameters[0].ParameterType;
        
        if (paramType == typeof(Window))
        {
            var del = (Action<Window>)RiteProbe.Create(typeof(Action<Window>), method);
            return (root, _) => del(root);
        }
        
        if (paramType == typeof(Viewport))
        {
            var del = (Action<Viewport>)RiteProbe.Create(typeof(Action<Viewport>), method);
            return (root, _) => del(root);
        }
        
        if (paramType == typeof(Node))
        {
            var del = (Action<Node>)RiteProbe.Create(typeof(Action<Node>), method);
            return (root, _) => del(root);
        }
        
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        LiturgyReport.AddInvalidParameterError(methodName, "OnProcess/OnPhysicsProcess (Global mode)", parameters[0].Name ?? "parameters[0]", paramType, "Window, Viewport, or Node (use PerNode mode for specific node types)");
        
        return null;
    }

    private static Action<Window, double>? CreateGlobalFrameCallback_NodeDelta(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var paramType = parameters[0].ParameterType;
        
        if (paramType == typeof(Window))
        {
            var del = (Action<Window, double>)RiteProbe.Create(typeof(Action<Window, double>), method);
            return del;
        }
        
        if (paramType == typeof(Viewport))
        {
            var del = (Action<Viewport, double>)RiteProbe.Create(typeof(Action<Viewport, double>), method);
            return (root, delta) => del(root, delta);
        }
        
        if (paramType == typeof(Node))
        {
            var del = (Action<Node, double>)RiteProbe.Create(typeof(Action<Node, double>), method);
            return (root, delta) => del(root, delta);
        }
        
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        LiturgyReport.AddInvalidParameterError(methodName, "OnProcess/OnPhysicsProcess (Global mode)", parameters[0].Name ?? "parameters[0]", paramType, "Window, Viewport, or Node (use PerNode mode for specific node types)");
        
        return null;
    }

    private static Action<Window, double>? CreateGlobalFrameCallback_DeltaNode(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var paramType = parameters[1].ParameterType;
        
        if (paramType == typeof(Window))
        {
            var del = (Action<double, Window>)RiteProbe.Create(typeof(Action<double, Window>), method);
            return (root, delta) => del(delta, root);
        }
        
        if (paramType == typeof(Viewport))
        {
            var del = (Action<double, Viewport>)RiteProbe.Create(typeof(Action<double, Viewport>), method);
            return (root, delta) => del(delta, root);
        }
        
        if (paramType == typeof(Node))
        {
            var del = (Action<double, Node>)RiteProbe.Create(typeof(Action<double, Node>), method);
            return (root, delta) => del(delta, root);
        }
        
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        LiturgyReport.AddInvalidParameterError(methodName, "OnProcess/OnPhysicsProcess (Global mode)", parameters[1].Name ?? "parameters[1]", paramType, "Window, Viewport, or Node (use PerNode mode for specific node types)");
        
        return null;
    }

    private static Action<double, Node>? CreateTargetedFrameCallback(MethodInfo method, FrameSignature signature)
    {
        var parameters = method.GetParameters();
        
        return signature switch
        {
            FrameSignature.NoParams => CreateTargetedFrameCallback_NoParams(method),
            FrameSignature.DeltaOnly => CreateTargetedFrameCallback_DeltaOnly(method),
            FrameSignature.NodeOnly => CreateTargetedFrameCallback_NodeOnly(method, parameters[0].ParameterType),
            FrameSignature.NodeDelta => CreateTargetedFrameCallback_NodeDelta(method, parameters[0].ParameterType),
            FrameSignature.DeltaNode => CreateTargetedFrameCallback_DeltaNode(method, parameters[1].ParameterType),
            _ => null
        };
    }

    private static Action<double, Node> CreateTargetedFrameCallback_NoParams(MethodInfo method)
    {
        var del = (Action)RiteProbe.Create(typeof(Action), method);
        return (_, _) => del();
    }

    private static Action<double, Node> CreateTargetedFrameCallback_DeltaOnly(MethodInfo method)
    {
        var del = (Action<double>)RiteProbe.Create(typeof(Action<double>), method);
        return (delta, _) => del(delta);
    }

    private static Action<double, Node>? CreateTargetedFrameCallback_NodeOnly(MethodInfo method, Type nodeType)
    {
        // Method signature: (Node) - we wrap as (double, Node) ignoring delta
        var nodeCallback = DelegateFactory.CreateCallback<Node>(method, nodeType);
        if (nodeCallback is null) return null;
        return (_, node) => nodeCallback(node);
    }

    private static Action<double, Node>? CreateTargetedFrameCallback_NodeDelta(MethodInfo method, Type nodeType)
    {
        // Method signature: (Node, double) - we receive (double, Node) so flip
        var callback = DelegateFactory.CreateSignalCallback<double, Node>(method, nodeType, signalParamIndex: 1, nodeParamIndex: 0);
        
        // Factory returns Action<double, Node> with signal first, which matches our needs
        return callback ?? null;
    }

    private static Action<double, Node>? CreateTargetedFrameCallback_DeltaNode(MethodInfo method, Type nodeType)
    {
        // Method signature: (double, Node) - matches our callback signature directly
        return DelegateFactory.CreateSignalCallback<double, Node>(method, nodeType, signalParamIndex: 0, nodeParamIndex: 1);
    }

    private Dictionary<ProcessCallbackEntry, int> SubscribeToExistingNodesForProcess(Node root)
    {
        var matchCounts = new Dictionary<ProcessCallbackEntry, int>();
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (IsShielded(node)) continue;
            TryAttachProcessHook(node, matchCounts);

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }

        return matchCounts;
    }

    private void OnNodeAddedForProcess(Node node)
    {
        if (IsHiddenFromRites(node)) return;
        TryAttachProcessHook(node, matchCounts: null);
    }

    private void TryAttachProcessHook(Node node, Dictionary<ProcessCallbackEntry, int>? matchCounts)
    {
        if (IsHiddenFromRites(node)) return;
        
        var path = node.GetPath().ToString();
        var needsProcess = false;
        var needsPhysicsProcess = false;

        // Check if any targeted process entries match this node
        foreach (var entry in _processTargetedEntries.Where(entry => MatchesEntry(node, path, entry)))
        {
            needsProcess = true;
            TrackEntryMatch(entry, matchCounts);
        }

        foreach (var entry in _physicsProcessTargetedEntries.Where(entry => MatchesEntry(node, path, entry)))
        {
            needsPhysicsProcess = true;
            TrackEntryMatch(entry, matchCounts);
        }

        if (!needsProcess && !needsPhysicsProcess) return;

        var hook = ProcessHook.GetOrCreate(node, needsProcess, needsPhysicsProcess);
        
        LiturgyReport.IncrementProcessHooks();
        LiturgyReport.AddProcessHookAttachment(node.GetType().Name, path, needsProcess, needsPhysicsProcess);

        // The hook stays with its node through reparenting, and entries match against the current path each frame, so subscribe once.
        if (needsProcess && !hook.ProcessRitesAttached)
        {
            hook.ProcessRitesAttached = true;
            hook.Processed += (delta, target) => OnProcessHookFired(delta, target, _processTargetedEntries);
        }

        if (needsPhysicsProcess && !hook.PhysicsProcessRitesAttached)
        {
            hook.PhysicsProcessRitesAttached = true;
            hook.PhysicsProcessed += (delta, target) => OnProcessHookFired(delta, target, _physicsProcessTargetedEntries);
        }
    }
    
    private void TrackEntryMatch(ProcessCallbackEntry entry, Dictionary<ProcessCallbackEntry, int>? matchCounts)
    {
        const int WARNING_THRESHOLD = 50;
        var methodName = $"{entry.Method.DeclaringType?.FullName}.{entry.Method.Name}";
        
        if (matchCounts is not null)
        {
            // Initial load - just track in the provided dictionary
            matchCounts.TryGetValue(entry, out var count);
            matchCounts[entry] = count + 1;
        }
        else
        {
            // Runtime (dynamic node added) - track and potentially warn
            _processEntryMatchCounts.TryGetValue(methodName, out var currentCount);
            var newCount = currentCount + 1;
            _processEntryMatchCounts[methodName] = newCount;
            
            // Check if we just crossed the threshold and haven't warned yet
            if (newCount < WARNING_THRESHOLD || _processEntryWarned.Contains(methodName)) return;

            var hasSuppression = entry.Method.GetCustomAttribute<SuppressWarnings>() is not null;
            _processEntryWarned.Add(methodName);
                
            if (!hasSuppression)
            {
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] [{entry.AttrName}(PerNode)] '{methodName}' has now matched {newCount} nodes at runtime. " +
                    $"Each creates a ProcessHook child that runs every frame. " +
                    $"Consider narrowing with a type filter or Exclude pattern. " +
                    $"Add [Rite.SuppressWarnings] to silence this.");
            }
        }
    }

    private static bool MatchesEntry(Node node, string path, ProcessCallbackEntry entry)
    {
        if (PathMatchesAnyExclude(path, entry.Excludes)) return false;
        if (entry.NodeType is not null && !entry.NodeType.IsInstanceOfType(node)) return false;
        if (entry.Targets is not null && !entry.Targets.Contains(path)) return false;
        return true;
    }

    private static void OnProcessHookFired(double delta, Node target, List<ProcessCallbackEntry> entries)
    {
        var path = target.GetPath().ToString();
        foreach (var entry in entries.Where(entry => MatchesEntry(target, path, entry))) RiteCall.Run(entry.Callback, delta, target);
    }

    public override void _Process(double delta)
    {
        foreach (var callback in _processGlobalCallbacks) RiteCall.Run(callback, _root, delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (var callback in _physicsProcessGlobalCallbacks) RiteCall.Run(callback, _root, delta);
    }
    
    private void OnProcessFrame()
    {
        var delta = GetProcessDeltaTime();
        foreach (var callback in _processWhilePausedCallbacks) RiteCall.Run(callback, _root, delta);
    }

    private void OnPhysicsFrame()
    {
        var delta = GetPhysicsProcessDeltaTime();
        foreach (var callback in _physicsProcessWhilePausedCallbacks) RiteCall.Run(callback, _root, delta);
    }
}

