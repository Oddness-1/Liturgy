using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    // Body entered/exited callbacks - Area2D
    private readonly List<(Action<Node2D, Area2D> Callback, HashSet<string>? Excludes, Type? BodyType)> _bodyEnteredArea2DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Node2D, Area2D> Callback, HashSet<string>? Excludes)>> _bodyEnteredArea2DTargetedCallbacks = new();
    private readonly List<(Action<Node2D, Area2D> Callback, HashSet<string>? Excludes, Type? BodyType)> _bodyExitedArea2DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Node2D, Area2D> Callback, HashSet<string>? Excludes)>> _bodyExitedArea2DTargetedCallbacks = new();
    
    // Body entered/exited callbacks - Area3D
    private readonly List<(Action<Node3D, Area3D> Callback, HashSet<string>? Excludes, Type? BodyType)> _bodyEnteredArea3DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Node3D, Area3D> Callback, HashSet<string>? Excludes)>> _bodyEnteredArea3DTargetedCallbacks = new();
    private readonly List<(Action<Node3D, Area3D> Callback, HashSet<string>? Excludes, Type? BodyType)> _bodyExitedArea3DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Node3D, Area3D> Callback, HashSet<string>? Excludes)>> _bodyExitedArea3DTargetedCallbacks = new();
    
    // Area entered/exited callbacks - Area2D
    private readonly List<(Action<Area2D, Area2D> Callback, HashSet<string>? Excludes, Type? AreaType)> _areaEnteredArea2DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Area2D, Area2D> Callback, HashSet<string>? Excludes)>> _areaEnteredArea2DTargetedCallbacks = new();
    private readonly List<(Action<Area2D, Area2D> Callback, HashSet<string>? Excludes, Type? AreaType)> _areaExitedArea2DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Area2D, Area2D> Callback, HashSet<string>? Excludes)>> _areaExitedArea2DTargetedCallbacks = new();
    
    // Area entered/exited callbacks - Area3D
    private readonly List<(Action<Area3D, Area3D> Callback, HashSet<string>? Excludes, Type? AreaType)> _areaEnteredArea3DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Area3D, Area3D> Callback, HashSet<string>? Excludes)>> _areaEnteredArea3DTargetedCallbacks = new();
    private readonly List<(Action<Area3D, Area3D> Callback, HashSet<string>? Excludes, Type? AreaType)> _areaExitedArea3DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Area3D, Area3D> Callback, HashSet<string>? Excludes)>> _areaExitedArea3DTargetedCallbacks = new();
    
    private readonly HashSet<Area2D> _subscribedArea2Ds = [];
    private readonly HashSet<Area3D> _subscribedArea3Ds = [];

    private void SetupPhysicsCallbacks()
    {
        SetupBodyEnteredCallbacks();
        SetupBodyExitedCallbacks();
        SetupAreaEnteredCallbacks();
        SetupAreaExitedCallbacks();
        
        var hasCallbacks = 
            _bodyEnteredArea2DGlobalCallbacks.Count > 0 || _bodyEnteredArea2DTargetedCallbacks.Count > 0 ||
            _bodyEnteredArea3DGlobalCallbacks.Count > 0 || _bodyEnteredArea3DTargetedCallbacks.Count > 0 ||
            _bodyExitedArea2DGlobalCallbacks.Count > 0 || _bodyExitedArea2DTargetedCallbacks.Count > 0 ||
            _bodyExitedArea3DGlobalCallbacks.Count > 0 || _bodyExitedArea3DTargetedCallbacks.Count > 0 ||
            _areaEnteredArea2DGlobalCallbacks.Count > 0 || _areaEnteredArea2DTargetedCallbacks.Count > 0 ||
            _areaEnteredArea3DGlobalCallbacks.Count > 0 || _areaEnteredArea3DTargetedCallbacks.Count > 0 ||
            _areaExitedArea2DGlobalCallbacks.Count > 0 || _areaExitedArea2DTargetedCallbacks.Count > 0 ||
            _areaExitedArea3DGlobalCallbacks.Count > 0 || _areaExitedArea3DTargetedCallbacks.Count > 0;
        
        if (!hasCallbacks) return;
        
        SubscribeToExistingNodesForPhysics(_root);
        GetTree().NodeAdded += OnNodeAddedForPhysics;
        GetTree().NodeRemoved += OnNodeRemovedForPhysics;
    }

    private void SetupBodyEnteredCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnBodyEntered>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var parameters = method.GetParameters();
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            if (parameters.Length == 0)
            {
                var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
                LiturgyReport.AddControlHook("OnBodyEntered", methodName, attr.Target, attr.Exclude, attr.Priority, null);
                AddPhysicsCallback(_bodyEnteredArea2DGlobalCallbacks, _bodyEnteredArea2DTargetedCallbacks, (Action<Node2D, Area2D>)((_, _) => del()), excludeSet, null, attr.Target);
                AddPhysicsCallback(_bodyEnteredArea3DGlobalCallbacks, _bodyEnteredArea3DTargetedCallbacks, (Action<Node3D, Area3D>)((_, _) => del()), excludeSet, null, attr.Target);
                continue;
            }
            
            if (parameters.Length > 2)
            {
                LiturgyReport.AddMethodError(methodName, "OnBodyEntered", "Must have at most two parameters.");
                continue;
            }
            
            // Determine 2D vs 3D from parameter types
            var has2D = parameters.Any(p => typeof(Node2D).IsAssignableFrom(p.ParameterType));
            var has3D = parameters.Any(p => typeof(Node3D).IsAssignableFrom(p.ParameterType));
            
            // Guard against mixing 2D and 3D types
            if (has2D && has3D)
            {
                LiturgyReport.AddMethodError(methodName, "OnBodyEntered", "Cannot mix 2D and 3D types. Use either Node2D/Area2D or Node3D/Area3D parameters.");
                continue;
            }
            
            if (has2D)
            {
                var callback = CreateBody2DCallback(method, parameters);
                if (callback is not null)
                {
                    var bodyType = parameters.FirstOrDefault(p => typeof(Node2D).IsAssignableFrom(p.ParameterType) && !typeof(Area2D).IsAssignableFrom(p.ParameterType))?.ParameterType;
                    LiturgyReport.AddControlHook("OnBodyEntered", methodName, attr.Target, attr.Exclude, attr.Priority, bodyType?.Name ?? "Area2D");
                    AddPhysicsCallback(_bodyEnteredArea2DGlobalCallbacks, _bodyEnteredArea2DTargetedCallbacks, callback, excludeSet, bodyType, attr.Target);
                }
            }
            else if (has3D)
            {
                var callback = CreateBody3DCallback(method, parameters);
                if (callback is not null)
                {
                    var bodyType = parameters.FirstOrDefault(p => typeof(Node3D).IsAssignableFrom(p.ParameterType) && !typeof(Area3D).IsAssignableFrom(p.ParameterType))?.ParameterType;
                    LiturgyReport.AddControlHook("OnBodyEntered", methodName, attr.Target, attr.Exclude, attr.Priority, bodyType?.Name ?? "Area3D");
                    AddPhysicsCallback(_bodyEnteredArea3DGlobalCallbacks, _bodyEnteredArea3DTargetedCallbacks, callback, excludeSet, bodyType, attr.Target);
                }
            }
            else
            {
                LiturgyReport.AddMethodError(methodName, "OnBodyEntered", "Parameters must include Node2D/Node3D or Area2D/Area3D types.");
            }
        }
    }

    private void SetupBodyExitedCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnBodyExited>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var parameters = method.GetParameters();
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            if (parameters.Length == 0)
            {
                var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
                LiturgyReport.AddControlHook("OnBodyExited", methodName, attr.Target, attr.Exclude, attr.Priority, null);
                AddPhysicsCallback(_bodyExitedArea2DGlobalCallbacks, _bodyExitedArea2DTargetedCallbacks, (Action<Node2D, Area2D>)((_, _) => del()), excludeSet, null, attr.Target);
                AddPhysicsCallback(_bodyExitedArea3DGlobalCallbacks, _bodyExitedArea3DTargetedCallbacks, (Action<Node3D, Area3D>)((_, _) => del()), excludeSet, null, attr.Target);
                continue;
            }
            
            if (parameters.Length > 2) { LiturgyReport.AddMethodError(methodName, "OnBodyExited", "Must have at most two parameters."); continue; }
            
            var has2D = parameters.Any(p => typeof(Node2D).IsAssignableFrom(p.ParameterType));
            var has3D = parameters.Any(p => typeof(Node3D).IsAssignableFrom(p.ParameterType));
            
            // Guard against mixing 2D and 3D types
            if (has2D && has3D)
            {
                LiturgyReport.AddMethodError(methodName, "OnBodyExited", "Cannot mix 2D and 3D types. Use either Node2D/Area2D or Node3D/Area3D parameters.");
                continue;
            }
            
            if (has2D)
            {
                var callback = CreateBody2DCallback(method, parameters);
                if (callback is not null)
                {
                    var bodyType = parameters.FirstOrDefault(p => typeof(Node2D).IsAssignableFrom(p.ParameterType) && !typeof(Area2D).IsAssignableFrom(p.ParameterType))?.ParameterType;
                    LiturgyReport.AddControlHook("OnBodyExited", methodName, attr.Target, attr.Exclude, attr.Priority, bodyType?.Name ?? "Area2D");
                    AddPhysicsCallback(_bodyExitedArea2DGlobalCallbacks, _bodyExitedArea2DTargetedCallbacks, callback, excludeSet, bodyType, attr.Target);
                }
            }
            else if (has3D)
            {
                var callback = CreateBody3DCallback(method, parameters);
                if (callback is not null)
                {
                    var bodyType = parameters.FirstOrDefault(p => typeof(Node3D).IsAssignableFrom(p.ParameterType) && !typeof(Area3D).IsAssignableFrom(p.ParameterType))?.ParameterType;
                    LiturgyReport.AddControlHook("OnBodyExited", methodName, attr.Target, attr.Exclude, attr.Priority, bodyType?.Name ?? "Area3D");
                    AddPhysicsCallback(_bodyExitedArea3DGlobalCallbacks, _bodyExitedArea3DTargetedCallbacks, callback, excludeSet, bodyType, attr.Target);
                }
            }
            else
            {
                LiturgyReport.AddMethodError(methodName, "OnBodyExited", "Parameters must include Node2D/Node3D or Area2D/Area3D types.");
            }
        }
    }

    private void SetupAreaEnteredCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnAreaEntered>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var parameters = method.GetParameters();
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            if (parameters.Length == 0)
            {
                var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
                LiturgyReport.AddControlHook("OnAreaEntered", methodName, attr.Target, attr.Exclude, attr.Priority, null);
                AddPhysicsCallback(_areaEnteredArea2DGlobalCallbacks, _areaEnteredArea2DTargetedCallbacks, (Action<Area2D, Area2D>)((_, _) => del()), excludeSet, null, attr.Target);
                AddPhysicsCallback(_areaEnteredArea3DGlobalCallbacks, _areaEnteredArea3DTargetedCallbacks, (Action<Area3D, Area3D>)((_, _) => del()), excludeSet, null, attr.Target);
                continue;
            }
            
            if (parameters.Length > 2) { LiturgyReport.AddMethodError(methodName, "OnAreaEntered", "Must have at most two parameters."); continue; }
            
            var has2D = parameters.Any(p => typeof(Area2D).IsAssignableFrom(p.ParameterType));
            var has3D = parameters.Any(p => typeof(Area3D).IsAssignableFrom(p.ParameterType));
            
            if (has2D && !has3D)
            {
                var callback = CreateArea2DCallback(method, parameters);
                if (callback is not null)
                {
                    LiturgyReport.AddControlHook("OnAreaEntered", methodName, attr.Target, attr.Exclude, attr.Priority, "Area2D");
                    AddPhysicsCallback(_areaEnteredArea2DGlobalCallbacks, _areaEnteredArea2DTargetedCallbacks, callback, excludeSet, null, attr.Target);
                }
            }
            else if (has3D && !has2D)
            {
                var callback = CreateArea3DCallback(method, parameters);
                if (callback is not null)
                {
                    LiturgyReport.AddControlHook("OnAreaEntered", methodName, attr.Target, attr.Exclude, attr.Priority, "Area3D");
                    AddPhysicsCallback(_areaEnteredArea3DGlobalCallbacks, _areaEnteredArea3DTargetedCallbacks, callback, excludeSet, null, attr.Target);
                }
            }
            else
            {
                LiturgyReport.AddMethodError(methodName, "OnAreaEntered", "Parameters must be Area2D or Area3D types (not mixed).");
            }
        }
    }

    private void SetupAreaExitedCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnAreaExited>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var parameters = method.GetParameters();
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            if (parameters.Length == 0)
            {
                var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
                LiturgyReport.AddControlHook("OnAreaExited", methodName, attr.Target, attr.Exclude, attr.Priority, null);
                AddPhysicsCallback(_areaExitedArea2DGlobalCallbacks, _areaExitedArea2DTargetedCallbacks, (Action<Area2D, Area2D>)((_, _) => del()), excludeSet, null, attr.Target);
                AddPhysicsCallback(_areaExitedArea3DGlobalCallbacks, _areaExitedArea3DTargetedCallbacks, (Action<Area3D, Area3D>)((_, _) => del()), excludeSet, null, attr.Target);
                continue;
            }
            
            if (parameters.Length > 2) { LiturgyReport.AddMethodError(methodName, "OnAreaExited", "Must have at most two parameters."); continue; }
            
            var has2D = parameters.Any(p => typeof(Area2D).IsAssignableFrom(p.ParameterType));
            var has3D = parameters.Any(p => typeof(Area3D).IsAssignableFrom(p.ParameterType));
            
            if (has2D && !has3D)
            {
                var callback = CreateArea2DCallback(method, parameters);
                if (callback is not null)
                {
                    LiturgyReport.AddControlHook("OnAreaExited", methodName, attr.Target, attr.Exclude, attr.Priority, "Area2D");
                    AddPhysicsCallback(_areaExitedArea2DGlobalCallbacks, _areaExitedArea2DTargetedCallbacks, callback, excludeSet, null, attr.Target);
                }
            }
            else if (has3D && !has2D)
            {
                var callback = CreateArea3DCallback(method, parameters);
                if (callback is not null)
                {
                    LiturgyReport.AddControlHook("OnAreaExited", methodName, attr.Target, attr.Exclude, attr.Priority, "Area3D");
                    AddPhysicsCallback(_areaExitedArea3DGlobalCallbacks, _areaExitedArea3DTargetedCallbacks, callback, excludeSet, null, attr.Target);
                }
            }
            else
            {
                LiturgyReport.AddMethodError(methodName, "OnAreaExited", "Parameters must be Area2D or Area3D types (not mixed).");
            }
        }
    }

    private static Action<Node2D, Area2D>? CreateBody2DCallback(MethodInfo method, ParameterInfo[] parameters)
    {
        if (parameters.Length == 1)
        {
            var p = parameters[0].ParameterType;
            if (typeof(Area2D).IsAssignableFrom(p))
            {
                var del = DelegateFactory.CreateCallback<Area2D>(method, p);
                return del is null ? null : (_, src) => del(src);
            }
            var delBody = DelegateFactory.CreateCallback<Node2D>(method, p);
            return delBody is null ? null : (body, _) => delBody(body);
        }
        
        // Two params - figure out order
        var bodyIdx = Array.FindIndex(parameters, p => typeof(Node2D).IsAssignableFrom(p.ParameterType) && !typeof(Area2D).IsAssignableFrom(p.ParameterType));
        var srcIdx = Array.FindIndex(parameters, p => typeof(Area2D).IsAssignableFrom(p.ParameterType));
        
        if (bodyIdx == 0 && srcIdx == 1)
        {
            var del = DelegateFactory.CreateSignalCallback<Node2D, Area2D>(method, parameters[1].ParameterType, 0, 1);
            return del;
        }
        if (srcIdx == 0 && bodyIdx == 1)
        {
            var del = DelegateFactory.CreateSignalCallback<Node2D, Area2D>(method, parameters[0].ParameterType, 1, 0);
            return del is null ? null : (body, src) => del(body, src);
        }
        return null;
    }

    private static Action<Node3D, Area3D>? CreateBody3DCallback(MethodInfo method, ParameterInfo[] parameters)
    {
        if (parameters.Length == 1)
        {
            var p = parameters[0].ParameterType;
            if (typeof(Area3D).IsAssignableFrom(p))
            {
                var del = DelegateFactory.CreateCallback<Area3D>(method, p);
                return del is null ? null : (_, src) => del(src);
            }
            var delBody = DelegateFactory.CreateCallback<Node3D>(method, p);
            return delBody is null ? null : (body, _) => delBody(body);
        }
        
        var bodyIdx = Array.FindIndex(parameters, p => typeof(Node3D).IsAssignableFrom(p.ParameterType) && !typeof(Area3D).IsAssignableFrom(p.ParameterType));
        var srcIdx = Array.FindIndex(parameters, p => typeof(Area3D).IsAssignableFrom(p.ParameterType));
        
        if (bodyIdx == 0 && srcIdx == 1)
            return DelegateFactory.CreateSignalCallback<Node3D, Area3D>(method, parameters[1].ParameterType, 0, 1);
        if (srcIdx == 0 && bodyIdx == 1)
        {
            var del = DelegateFactory.CreateSignalCallback<Node3D, Area3D>(method, parameters[0].ParameterType, 1, 0);
            return del is null ? null : (body, src) => del(body, src);
        }
        return null;
    }

    private static Action<Area2D, Area2D>? CreateArea2DCallback(MethodInfo method, ParameterInfo[] parameters)
    {
        if (parameters.Length == 1)
        {
            var del = DelegateFactory.CreateCallback<Area2D>(method, parameters[0].ParameterType);
            return del is null ? null : (entering, _) => del(entering);
        }
        return DelegateFactory.CreateSignalCallback<Area2D, Area2D>(method, parameters[1].ParameterType, 0, 1);
    }

    private static Action<Area3D, Area3D>? CreateArea3DCallback(MethodInfo method, ParameterInfo[] parameters)
    {
        if (parameters.Length == 1)
        {
            var del = DelegateFactory.CreateCallback<Area3D>(method, parameters[0].ParameterType);
            return del is null ? null : (entering, _) => del(entering);
        }
        return DelegateFactory.CreateSignalCallback<Area3D, Area3D>(method, parameters[1].ParameterType, 0, 1);
    }

    private static void AddPhysicsCallback<TS, TSrc>(
        List<(Action<TS, TSrc> Callback, HashSet<string>? Excludes, Type? SignalType)> global,
        Dictionary<string, List<(Action<TS, TSrc> Callback, HashSet<string>? Excludes)>> targeted,
        Action<TS, TSrc> callback, HashSet<string>? excludeSet, Type? signalType, string? target)
    {
        if (target is null) { global.Add((callback, excludeSet, signalType)); return; }
        foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var n = NormalizePath(t);
            if (!targeted.ContainsKey(n)) targeted[n] = [];
            targeted[n].Add((callback, excludeSet));
        }
    }

    private void SubscribeToExistingNodesForPhysics(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Area2D a2) SubscribeArea2D(a2);
            else if (node is Area3D a3) SubscribeArea3D(a3);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    private void OnNodeAddedForPhysics(Node node)
    {
        if (IsInternalNode(node)) return;
        if (node is Area2D a2) SubscribeArea2D(a2);
        else if (node is Area3D a3) SubscribeArea3D(a3);
    }

    private void OnNodeRemovedForPhysics(Node node)
    {
        if (node is Area2D a2) _subscribedArea2Ds.Remove(a2);
        else if (node is Area3D a3) _subscribedArea3Ds.Remove(a3);
    }

    private void SubscribeArea2D(Area2D area)
    {
        if (_subscribedArea2Ds.Contains(area) || IsInternalNode(area)) return;
        var path = area.GetPath().ToString();
        var hasAny = _bodyEnteredArea2DTargetedCallbacks.ContainsKey(path) || _bodyExitedArea2DTargetedCallbacks.ContainsKey(path) ||
                     _areaEnteredArea2DTargetedCallbacks.ContainsKey(path) || _areaExitedArea2DTargetedCallbacks.ContainsKey(path) ||
                     _bodyEnteredArea2DGlobalCallbacks.Count > 0 || _bodyExitedArea2DGlobalCallbacks.Count > 0 ||
                     _areaEnteredArea2DGlobalCallbacks.Count > 0 || _areaExitedArea2DGlobalCallbacks.Count > 0;
        if (!hasAny) return;
        
        _subscribedArea2Ds.Add(area);
        Listen(area, Area2D.SignalName.BodyEntered, Callable.From((Node2D body) => FireBodyEntered2D(area, path, body)));
        Listen(area, Area2D.SignalName.BodyExited, Callable.From((Node2D body) => FireBodyExited2D(area, path, body)));
        Listen(area, Area2D.SignalName.AreaEntered, Callable.From((Area2D entering) => FireAreaEntered2D(area, path, entering)));
        Listen(area, Area2D.SignalName.AreaExited, Callable.From((Area2D exiting) => FireAreaExited2D(area, path, exiting)));
    }

    private void SubscribeArea3D(Area3D area)
    {
        if (_subscribedArea3Ds.Contains(area) || IsInternalNode(area)) return;
        var path = area.GetPath().ToString();
        var hasAny = _bodyEnteredArea3DTargetedCallbacks.ContainsKey(path) || _bodyExitedArea3DTargetedCallbacks.ContainsKey(path) ||
                     _areaEnteredArea3DTargetedCallbacks.ContainsKey(path) || _areaExitedArea3DTargetedCallbacks.ContainsKey(path) ||
                     _bodyEnteredArea3DGlobalCallbacks.Count > 0 || _bodyExitedArea3DGlobalCallbacks.Count > 0 ||
                     _areaEnteredArea3DGlobalCallbacks.Count > 0 || _areaExitedArea3DGlobalCallbacks.Count > 0;
        if (!hasAny) return;
        
        _subscribedArea3Ds.Add(area);
        Listen(area, Area3D.SignalName.BodyEntered, Callable.From((Node3D body) => FireBodyEntered3D(area, path, body)));
        Listen(area, Area3D.SignalName.BodyExited, Callable.From((Node3D body) => FireBodyExited3D(area, path, body)));
        Listen(area, Area3D.SignalName.AreaEntered, Callable.From((Area3D entering) => FireAreaEntered3D(area, path, entering)));
        Listen(area, Area3D.SignalName.AreaExited, Callable.From((Area3D exiting) => FireAreaExited3D(area, path, exiting)));
    }

    private void FireBodyEntered2D(Area2D src, string path, Node2D body)
    {
        if (_bodyEnteredArea2DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(body, src);
            }
        }
        
        foreach (var (callback, excludes, bodyType) in _bodyEnteredArea2DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (bodyType is not null && !bodyType.IsInstanceOfType(body)) continue;
            callback(body, src);
        }
    }

    private void FireBodyExited2D(Area2D src, string path, Node2D body)
    {
        if (_bodyExitedArea2DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(body, src);
            }
        }
        
        foreach (var (callback, excludes, bodyType) in _bodyExitedArea2DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (bodyType is not null && !bodyType.IsInstanceOfType(body)) continue;
            callback(body, src);
        }
    }

    private void FireAreaEntered2D(Area2D src, string path, Area2D entering)
    {
        if (_areaEnteredArea2DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(entering, src);
            }
        }
        
        foreach (var (callback, excludes, areaType) in _areaEnteredArea2DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (areaType is not null && !areaType.IsInstanceOfType(entering)) continue;
            callback(entering, src);
        }
    }

    private void FireAreaExited2D(Area2D src, string path, Area2D exiting)
    {
        if (_areaExitedArea2DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(exiting, src);
            }
        }
        
        foreach (var (callback, excludes, areaType) in _areaExitedArea2DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (areaType is not null && !areaType.IsInstanceOfType(exiting)) continue;
            callback(exiting, src);
        }
    }

    private void FireBodyEntered3D(Area3D src, string path, Node3D body)
    {
        if (_bodyEnteredArea3DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(body, src);
            }
        }
        
        foreach (var (callback, excludes, bodyType) in _bodyEnteredArea3DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (bodyType is not null && !bodyType.IsInstanceOfType(body)) continue;
            callback(body, src);
        }
    }

    private void FireBodyExited3D(Area3D src, string path, Node3D body)
    {
        if (_bodyExitedArea3DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(body, src);
            }
        }
        
        foreach (var (callback, excludes, bodyType) in _bodyExitedArea3DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (bodyType is not null && !bodyType.IsInstanceOfType(body)) continue;
            callback(body, src);
        }
    }

    private void FireAreaEntered3D(Area3D src, string path, Area3D entering)
    {
        if (_areaEnteredArea3DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(entering, src);
            }
        }
        
        foreach (var (callback, excludes, areaType) in _areaEnteredArea3DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (areaType is not null && !areaType.IsInstanceOfType(entering)) continue;
            callback(entering, src);
        }
    }

    private void FireAreaExited3D(Area3D src, string path, Area3D exiting)
    {
        if (_areaExitedArea3DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(exiting, src);
            }
        }
        
        foreach (var (callback, excludes, areaType) in _areaExitedArea3DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (areaType is not null && !areaType.IsInstanceOfType(exiting)) continue;
            callback(exiting, src);
        }
    }
}

