using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    // Focus callbacks
    private readonly List<(Action<Control> Callback, HashSet<string>? Excludes, Type? SourceType)> _focusEnteredGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Control> Callback, HashSet<string>? Excludes)>> _focusEnteredTargetedCallbacks = new();
    
    private readonly List<(Action<Control> Callback, HashSet<string>? Excludes, Type? SourceType)> _focusExitedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Control> Callback, HashSet<string>? Excludes)>> _focusExitedTargetedCallbacks = new();
    
    // Mouse enter/exit callbacks for Controls
    private readonly List<(Action<Control> Callback, HashSet<string>? Excludes, Type? SourceType)> _mouseEnteredControlGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Control> Callback, HashSet<string>? Excludes)>> _mouseEnteredControlTargetedCallbacks = new();
    
    private readonly List<(Action<Control> Callback, HashSet<string>? Excludes, Type? SourceType)> _mouseExitedControlGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Control> Callback, HashSet<string>? Excludes)>> _mouseExitedControlTargetedCallbacks = new();
    
    // Mouse enter/exit callbacks for CollisionObject2D
    private readonly List<(Action<CollisionObject2D> Callback, HashSet<string>? Excludes, Type? SourceType)> _mouseEnteredCollision2DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<CollisionObject2D> Callback, HashSet<string>? Excludes)>> _mouseEnteredCollision2DTargetedCallbacks = new();
    
    private readonly List<(Action<CollisionObject2D> Callback, HashSet<string>? Excludes, Type? SourceType)> _mouseExitedCollision2DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<CollisionObject2D> Callback, HashSet<string>? Excludes)>> _mouseExitedCollision2DTargetedCallbacks = new();
    
    // Mouse enter/exit callbacks for CollisionObject3D
    private readonly List<(Action<CollisionObject3D> Callback, HashSet<string>? Excludes, Type? SourceType)> _mouseEnteredCollision3DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<CollisionObject3D> Callback, HashSet<string>? Excludes)>> _mouseEnteredCollision3DTargetedCallbacks = new();
    
    private readonly List<(Action<CollisionObject3D> Callback, HashSet<string>? Excludes, Type? SourceType)> _mouseExitedCollision3DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<CollisionObject3D> Callback, HashSet<string>? Excludes)>> _mouseExitedCollision3DTargetedCallbacks = new();
    
    // Resized callbacks
    private readonly List<(Action<Control> Callback, HashSet<string>? Excludes, Type? SourceType)> _resizedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Control> Callback, HashSet<string>? Excludes)>> _resizedTargetedCallbacks = new();
    
    // GuiInput callbacks
    private readonly List<(Action<InputEvent, Control> Callback, HashSet<string>? Excludes, Type? EventType, Type? SourceType)> _guiInputGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<InputEvent, Control> Callback, HashSet<string>? Excludes, Type? EventType)>> _guiInputTargetedCallbacks = new();
    
    // Minimum size changed callbacks
    private readonly List<(Action<Control> Callback, HashSet<string>? Excludes, Type? SourceType)> _minimumSizeChangedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Control> Callback, HashSet<string>? Excludes)>> _minimumSizeChangedTargetedCallbacks = new();
    
    // Track subscribed nodes
    private readonly HashSet<Control> _subscribedControlsForFocus = [];
    private readonly HashSet<CollisionObject2D> _subscribedCollision2DForMouse = [];
    private readonly HashSet<CollisionObject3D> _subscribedCollision3DForMouse = [];

    private void SetupFocusAndMouseCallbacks()
    {
        SetupFocusEnteredCallbacks();
        SetupFocusExitedCallbacks();
        SetupMouseEnteredCallbacks();
        SetupMouseExitedCallbacks();
        SetupResizedCallbacks();
        SetupGuiInputCallbacks();
        SetupMinimumSizeChangedCallbacks();
        
        var hasCallbacks = 
            _focusEnteredGlobalCallbacks.Count > 0 || _focusEnteredTargetedCallbacks.Count > 0 ||
            _focusExitedGlobalCallbacks.Count > 0 || _focusExitedTargetedCallbacks.Count > 0 ||
            _mouseEnteredControlGlobalCallbacks.Count > 0 || _mouseEnteredControlTargetedCallbacks.Count > 0 ||
            _mouseExitedControlGlobalCallbacks.Count > 0 || _mouseExitedControlTargetedCallbacks.Count > 0 ||
            _mouseEnteredCollision2DGlobalCallbacks.Count > 0 || _mouseEnteredCollision2DTargetedCallbacks.Count > 0 ||
            _mouseExitedCollision2DGlobalCallbacks.Count > 0 || _mouseExitedCollision2DTargetedCallbacks.Count > 0 ||
            _mouseEnteredCollision3DGlobalCallbacks.Count > 0 || _mouseEnteredCollision3DTargetedCallbacks.Count > 0 ||
            _mouseExitedCollision3DGlobalCallbacks.Count > 0 || _mouseExitedCollision3DTargetedCallbacks.Count > 0 ||
            _resizedGlobalCallbacks.Count > 0 || _resizedTargetedCallbacks.Count > 0 ||
            _guiInputGlobalCallbacks.Count > 0 || _guiInputTargetedCallbacks.Count > 0 ||
            _minimumSizeChangedGlobalCallbacks.Count > 0 || _minimumSizeChangedTargetedCallbacks.Count > 0;
        
        if (!hasCallbacks) return;
        
        SubscribeToExistingNodesForFocusAndMouse(_root);
        GetTree().NodeAdded += OnNodeAddedForFocusAndMouse;
        GetTree().NodeRemoved += OnNodeRemovedForFocusAndMouse;
    }

    private void SetupFocusEnteredCallbacks()
    {
        var methods = ScanForMethods<OnFocusEntered>();
        
        foreach (var (method, attr) in methods.OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateControlCallback(method, "OnFocusEntered");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnFocusEntered", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name);
            
            if (attr.Target is null)
            {
                _focusEnteredGlobalCallbacks.Add((callback, excludeSet, sourceType));
            }
            else
            {
                AddTargetedCallbacks(_focusEnteredTargetedCallbacks, attr.Target, callback, excludeSet);
            }
        }
    }

    private void SetupFocusExitedCallbacks()
    {
        var methods = ScanForMethods<OnFocusExited>();
        
        foreach (var (method, attr) in methods.OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateControlCallback(method, "OnFocusExited");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnFocusExited", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name);
            
            if (attr.Target is null)
            {
                _focusExitedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            }
            else
            {
                AddTargetedCallbacks(_focusExitedTargetedCallbacks, attr.Target, callback, excludeSet);
            }
        }
    }

    private void SetupMouseEnteredCallbacks()
    {
        var methods = ScanForMethods<OnMouseEntered>();
        
        foreach (var (method, attr) in methods.OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var parameters = method.GetParameters();
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            if (parameters.Length == 0)
            {
                // No parameters - register for all three types
                var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
                LiturgyReport.AddControlHook("OnMouseEntered", methodName, attr.Target, attr.Exclude, attr.Priority, null);
                
                AddMouseCallback(_mouseEnteredControlGlobalCallbacks, _mouseEnteredControlTargetedCallbacks, 
                    (Action<Control>)(_ => del()), excludeSet, null, attr.Target);
                AddMouseCallback(_mouseEnteredCollision2DGlobalCallbacks, _mouseEnteredCollision2DTargetedCallbacks,
                    (Action<CollisionObject2D>)(_ => del()), excludeSet, null, attr.Target);
                AddMouseCallback(_mouseEnteredCollision3DGlobalCallbacks, _mouseEnteredCollision3DTargetedCallbacks,
                    (Action<CollisionObject3D>)(_ => del()), excludeSet, null, attr.Target);
            }
            else if (parameters.Length == 1)
            {
                var paramType = parameters[0].ParameterType;
                
                if (typeof(Control).IsAssignableFrom(paramType))
                {
                    var callback = DelegateFactory.CreateCallback<Control>(method, paramType);
                    if (callback is not null)
                    {
                        LiturgyReport.AddControlHook("OnMouseEntered", methodName, attr.Target, attr.Exclude, attr.Priority, paramType.Name);
                        AddMouseCallback(_mouseEnteredControlGlobalCallbacks, _mouseEnteredControlTargetedCallbacks, callback, excludeSet, paramType, attr.Target);
                    }
                }
                else if (typeof(CollisionObject2D).IsAssignableFrom(paramType))
                {
                    var callback = DelegateFactory.CreateCallback<CollisionObject2D>(method, paramType);
                    if (callback is not null)
                    {
                        LiturgyReport.AddControlHook("OnMouseEntered", methodName, attr.Target, attr.Exclude, attr.Priority, paramType.Name);
                        AddMouseCallback(_mouseEnteredCollision2DGlobalCallbacks, _mouseEnteredCollision2DTargetedCallbacks, callback, excludeSet, paramType, attr.Target);
                    }
                }
                else if (typeof(CollisionObject3D).IsAssignableFrom(paramType))
                {
                    var callback = DelegateFactory.CreateCallback<CollisionObject3D>(method, paramType);
                    if (callback is not null)
                    {
                        LiturgyReport.AddControlHook("OnMouseEntered", methodName, attr.Target, attr.Exclude, attr.Priority, paramType.Name);
                        AddMouseCallback(_mouseEnteredCollision3DGlobalCallbacks, _mouseEnteredCollision3DTargetedCallbacks, callback, excludeSet, paramType, attr.Target);
                    }
                }
                else
                {
                    LiturgyReport.AddInvalidParameterError(methodName, "OnMouseEntered", parameters[0].Name ?? "arg0", paramType, 
                        "Control, CollisionObject2D, or CollisionObject3D subtypes");
                }
            }
            else
            {
                LiturgyReport.AddMethodError(methodName, "OnMouseEntered", "Must have zero or one parameter.");
            }
        }
    }

    private void SetupMouseExitedCallbacks()
    {
        var methods = ScanForMethods<OnMouseExited>();
        
        foreach (var (method, attr) in methods.OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var parameters = method.GetParameters();
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            if (parameters.Length == 0)
            {
                var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
                LiturgyReport.AddControlHook("OnMouseExited", methodName, attr.Target, attr.Exclude, attr.Priority, null);
                
                AddMouseCallback(_mouseExitedControlGlobalCallbacks, _mouseExitedControlTargetedCallbacks, 
                    (Action<Control>)(_ => del()), excludeSet, null, attr.Target);
                AddMouseCallback(_mouseExitedCollision2DGlobalCallbacks, _mouseExitedCollision2DTargetedCallbacks,
                    (Action<CollisionObject2D>)(_ => del()), excludeSet, null, attr.Target);
                AddMouseCallback(_mouseExitedCollision3DGlobalCallbacks, _mouseExitedCollision3DTargetedCallbacks,
                    (Action<CollisionObject3D>)(_ => del()), excludeSet, null, attr.Target);
            }
            else if (parameters.Length == 1)
            {
                var paramType = parameters[0].ParameterType;
                
                if (typeof(Control).IsAssignableFrom(paramType))
                {
                    var callback = DelegateFactory.CreateCallback<Control>(method, paramType);
                    if (callback is not null)
                    {
                        LiturgyReport.AddControlHook("OnMouseExited", methodName, attr.Target, attr.Exclude, attr.Priority, paramType.Name);
                        AddMouseCallback(_mouseExitedControlGlobalCallbacks, _mouseExitedControlTargetedCallbacks, callback, excludeSet, paramType, attr.Target);
                    }
                }
                else if (typeof(CollisionObject2D).IsAssignableFrom(paramType))
                {
                    var callback = DelegateFactory.CreateCallback<CollisionObject2D>(method, paramType);
                    if (callback is not null)
                    {
                        LiturgyReport.AddControlHook("OnMouseExited", methodName, attr.Target, attr.Exclude, attr.Priority, paramType.Name);
                        AddMouseCallback(_mouseExitedCollision2DGlobalCallbacks, _mouseExitedCollision2DTargetedCallbacks, callback, excludeSet, paramType, attr.Target);
                    }
                }
                else if (typeof(CollisionObject3D).IsAssignableFrom(paramType))
                {
                    var callback = DelegateFactory.CreateCallback<CollisionObject3D>(method, paramType);
                    if (callback is not null)
                    {
                        LiturgyReport.AddControlHook("OnMouseExited", methodName, attr.Target, attr.Exclude, attr.Priority, paramType.Name);
                        AddMouseCallback(_mouseExitedCollision3DGlobalCallbacks, _mouseExitedCollision3DTargetedCallbacks, callback, excludeSet, paramType, attr.Target);
                    }
                }
                else
                {
                    LiturgyReport.AddInvalidParameterError(methodName, "OnMouseExited", parameters[0].Name ?? "arg0", paramType, 
                        "Control, CollisionObject2D, or CollisionObject3D subtypes");
                }
            }
            else
            {
                LiturgyReport.AddMethodError(methodName, "OnMouseExited", "Must have zero or one parameter.");
            }
        }
    }

    private static void AddMouseCallback<T>(
        List<(Action<T> Callback, HashSet<string>? Excludes, Type? SourceType)> globalList,
        Dictionary<string, List<(Action<T> Callback, HashSet<string>? Excludes)>> targetedDict,
        Action<T> callback, HashSet<string>? excludeSet, Type? sourceType, string? target)
    {
        if (target is null)
        {
            globalList.Add((callback, excludeSet, sourceType));
        }
        else
        {
            foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = NormalizePath(t);
                if (!targetedDict.ContainsKey(normalized))
                    targetedDict[normalized] = [];
                targetedDict[normalized].Add((callback, excludeSet));
            }
        }
    }

    private void SetupResizedCallbacks()
    {
        var methods = ScanForMethods<OnResized>();
        
        foreach (var (method, attr) in methods.OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateControlCallback(method, "OnResized");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnResized", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name);
            
            if (attr.Target is null)
            {
                _resizedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            }
            else
            {
                AddTargetedCallbacks(_resizedTargetedCallbacks, attr.Target, callback, excludeSet);
            }
        }
    }

    private void SetupGuiInputCallbacks()
    {
        var methods = ScanForMethods<OnGuiInput>();
        
        foreach (var (method, attr) in methods.OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, eventType, sourceType) = CreateGuiInputCallback(method);
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            var typeFilter = eventType?.Name ?? sourceType?.Name;
            LiturgyReport.AddControlHook("OnGuiInput", methodName, attr.Target, attr.Exclude, attr.Priority, typeFilter);
            
            if (attr.Target is null)
            {
                _guiInputGlobalCallbacks.Add((callback, excludeSet, eventType, sourceType));
            }
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_guiInputTargetedCallbacks.ContainsKey(normalized))
                        _guiInputTargetedCallbacks[normalized] = [];
                    _guiInputTargetedCallbacks[normalized].Add((callback, excludeSet, eventType));
                }
            }
        }
    }

    private void SetupMinimumSizeChangedCallbacks()
    {
        var methods = ScanForMethods<OnMinimumSizeChanged>();
        
        foreach (var (method, attr) in methods.OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateControlCallback(method, "OnMinimumSizeChanged");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnMinimumSizeChanged", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name);
            
            if (attr.Target is null)
            {
                _minimumSizeChangedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            }
            else
            {
                AddTargetedCallbacks(_minimumSizeChangedTargetedCallbacks, attr.Target, callback, excludeSet);
            }
        }
    }

    private static (Action<Control>? Callback, Type? SourceType) CreateControlCallback(MethodInfo method, string hookName)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        if (parameters.Length == 0)
        {
            var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
            return (_ => del(), null);
        }
        
        if (parameters.Length > 1)
        {
            LiturgyReport.AddMethodError(methodName, hookName, "Must have zero or one parameter.");
            return (null, null);
        }
        
        var paramType = parameters[0].ParameterType;
        if (!typeof(Control).IsAssignableFrom(paramType))
        {
            LiturgyReport.AddInvalidParameterError(methodName, hookName, parameters[0].Name ?? "arg0", paramType, "Control subtypes");
            return (null, null);
        }
        
        var callback = DelegateFactory.CreateCallback<Control>(method, paramType);
        return (callback, paramType);
    }

    private static (Action<InputEvent, Control>? Callback, Type? EventType, Type? SourceType) CreateGuiInputCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        if (parameters.Length == 0)
        {
            var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
            return ((_, _) => del(), null, null);
        }
        
        if (parameters.Length > 2)
        {
            LiturgyReport.AddMethodError(methodName, "OnGuiInput", "Must have at most two parameters.");
            return (null, null, null);
        }
        
        Type? eventType = null;
        Type? sourceType = null;
        int? eventIdx = null;
        int? sourceIdx = null;
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            
            if (typeof(InputEvent).IsAssignableFrom(paramType))
            {
                eventType = paramType;
                eventIdx = i;
            }
            else if (typeof(Control).IsAssignableFrom(paramType))
            {
                sourceType = paramType;
                sourceIdx = i;
            }
            else
            {
                LiturgyReport.AddInvalidParameterError(methodName, "OnGuiInput", parameters[i].Name ?? $"arg{i}", paramType,
                    "InputEvent subtypes and/or Control subtypes");
                return (null, null, null);
            }
        }
        
        // Create appropriate delegate based on parameter configuration
        if (parameters.Length == 1)
        {
            if (eventIdx is not null)
            {
                var callback = DelegateFactory.CreateCallback<InputEvent>(method, eventType!);
                if (callback is null) return (null, null, null);
                return ((e, _) => callback(e), eventType, null);
            }
            else
            {
                var callback = DelegateFactory.CreateCallback<Control>(method, sourceType!);
                if (callback is null) return (null, null, null);
                return ((_, c) => callback(c), null, sourceType);
            }
        }
        
        // Two parameters
        var fullCallback = DelegateFactory.CreateSignalCallback<InputEvent, Control>(method, sourceType!, 
            signalParamIndex: eventIdx!.Value, nodeParamIndex: sourceIdx!.Value);
        if (fullCallback is null) return (null, null, null);
        return (fullCallback, eventType, sourceType);
    }

    private static HashSet<string>? ParseExcludeSet(string? exclude)
    {
        if (string.IsNullOrEmpty(exclude)) return null;
        return exclude
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeExcludePattern)
            .ToHashSet();
    }

    private static void AddTargetedCallbacks(
        Dictionary<string, List<(Action<Control> Callback, HashSet<string>? Excludes)>> dict, 
        string target, Action<Control> callback, HashSet<string>? excludeSet)
    {
        foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = NormalizePath(t);
            if (!dict.ContainsKey(normalized))
                dict[normalized] = [];
            dict[normalized].Add((callback, excludeSet));
        }
    }

    private void SubscribeToExistingNodesForFocusAndMouse(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            SubscribeFocusAndMouseNode(node);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    private void OnNodeAddedForFocusAndMouse(Node node)
    {
        if (IsInternalNode(node)) return;
        SubscribeFocusAndMouseNode(node);
    }

    private void OnNodeRemovedForFocusAndMouse(Node node)
    {
        switch (node)
        {
            case Control c: _subscribedControlsForFocus.Remove(c); break;
            case CollisionObject2D c2d: _subscribedCollision2DForMouse.Remove(c2d); break;
            case CollisionObject3D c3d: _subscribedCollision3DForMouse.Remove(c3d); break;
        }
    }

    private void SubscribeFocusAndMouseNode(Node node)
    {
        switch (node)
        {
            case Control control:
                SubscribeControlForFocusAndMouse(control);
                break;
            case CollisionObject2D collision2D:
                SubscribeCollision2DForMouse(collision2D);
                break;
            case CollisionObject3D collision3D:
                SubscribeCollision3DForMouse(collision3D);
                break;
        }
    }

    private void SubscribeControlForFocusAndMouse(Control control)
    {
        if (_subscribedControlsForFocus.Contains(control)) return;
        if (IsInternalNode(control)) return;
        
        var path = control.GetPath().ToString();
        var hasTargeted = _focusEnteredTargetedCallbacks.ContainsKey(path) ||
                          _focusExitedTargetedCallbacks.ContainsKey(path) ||
                          _mouseEnteredControlTargetedCallbacks.ContainsKey(path) ||
                          _mouseExitedControlTargetedCallbacks.ContainsKey(path) ||
                          _resizedTargetedCallbacks.ContainsKey(path) ||
                          _guiInputTargetedCallbacks.ContainsKey(path) ||
                          _minimumSizeChangedTargetedCallbacks.ContainsKey(path);
        
        var hasGlobal = _focusEnteredGlobalCallbacks.Count > 0 ||
                        _focusExitedGlobalCallbacks.Count > 0 ||
                        _mouseEnteredControlGlobalCallbacks.Count > 0 ||
                        _mouseExitedControlGlobalCallbacks.Count > 0 ||
                        _resizedGlobalCallbacks.Count > 0 ||
                        _guiInputGlobalCallbacks.Count > 0 ||
                        _minimumSizeChangedGlobalCallbacks.Count > 0;
        
        if (!hasTargeted && !hasGlobal) return;
        
        _subscribedControlsForFocus.Add(control);
        
        Listen(control, Control.SignalName.FocusEntered, Callable.From(() => OnControlFocusEntered(control, path)));
        Listen(control, Control.SignalName.FocusExited, Callable.From(() => OnControlFocusExited(control, path)));
        Listen(control, Control.SignalName.MouseEntered, Callable.From(() => OnControlMouseEntered(control, path)));
        Listen(control, Control.SignalName.MouseExited, Callable.From(() => OnControlMouseExited(control, path)));
        Listen(control, Control.SignalName.Resized, Callable.From(() => OnControlResized(control, path)));
        Listen(control, Control.SignalName.GuiInput, Callable.From((InputEvent e) => OnControlGuiInput(e, control, path)));
        Listen(control, Control.SignalName.MinimumSizeChanged, Callable.From(() => OnControlMinimumSizeChanged(control, path)));
    }

    private void SubscribeCollision2DForMouse(CollisionObject2D collision)
    {
        if (_subscribedCollision2DForMouse.Contains(collision)) return;
        if (IsInternalNode(collision)) return;
        
        var path = collision.GetPath().ToString();
        var hasTargeted = _mouseEnteredCollision2DTargetedCallbacks.ContainsKey(path) ||
                          _mouseExitedCollision2DTargetedCallbacks.ContainsKey(path);
        var hasGlobal = _mouseEnteredCollision2DGlobalCallbacks.Count > 0 ||
                        _mouseExitedCollision2DGlobalCallbacks.Count > 0;
        
        if (!hasTargeted && !hasGlobal) return;
        
        _subscribedCollision2DForMouse.Add(collision);
        
        Listen(collision, CollisionObject2D.SignalName.MouseEntered, Callable.From(() => OnCollision2DMouseEntered(collision, path)));
        Listen(collision, CollisionObject2D.SignalName.MouseExited, Callable.From(() => OnCollision2DMouseExited(collision, path)));
    }

    private void SubscribeCollision3DForMouse(CollisionObject3D collision)
    {
        if (_subscribedCollision3DForMouse.Contains(collision)) return;
        if (IsInternalNode(collision)) return;
        
        var path = collision.GetPath().ToString();
        var hasTargeted = _mouseEnteredCollision3DTargetedCallbacks.ContainsKey(path) ||
                          _mouseExitedCollision3DTargetedCallbacks.ContainsKey(path);
        var hasGlobal = _mouseEnteredCollision3DGlobalCallbacks.Count > 0 ||
                        _mouseExitedCollision3DGlobalCallbacks.Count > 0;
        
        if (!hasTargeted && !hasGlobal) return;
        
        _subscribedCollision3DForMouse.Add(collision);
        
        Listen(collision, CollisionObject3D.SignalName.MouseEntered, Callable.From(() => OnCollision3DMouseEntered(collision, path)));
        Listen(collision, CollisionObject3D.SignalName.MouseExited, Callable.From(() => OnCollision3DMouseExited(collision, path)));
    }

    private void OnControlFocusEntered(Control control, string path)
    {
        // Targeted callbacks
        if (_focusEnteredTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                RiteCall.Run(callback, control);
            }
        }
        
        // Global callbacks
        foreach (var (callback, excludes, sourceType) in _focusEnteredGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(control)) continue;
            RiteCall.Run(callback, control);
        }
    }

    private void OnControlFocusExited(Control control, string path)
    {
        if (_focusExitedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                RiteCall.Run(callback, control);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _focusExitedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(control)) continue;
            RiteCall.Run(callback, control);
        }
    }

    private void OnControlMouseEntered(Control control, string path)
    {
        if (_mouseEnteredControlTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                RiteCall.Run(callback, control);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _mouseEnteredControlGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(control)) continue;
            RiteCall.Run(callback, control);
        }
    }

    private void OnControlMouseExited(Control control, string path)
    {
        if (_mouseExitedControlTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                RiteCall.Run(callback, control);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _mouseExitedControlGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(control)) continue;
            RiteCall.Run(callback, control);
        }
    }

    private void OnControlResized(Control control, string path)
    {
        if (_resizedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                RiteCall.Run(callback, control);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _resizedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(control)) continue;
            RiteCall.Run(callback, control);
        }
    }

    private void OnControlGuiInput(InputEvent @event, Control control, string path)
    {
        if (_guiInputTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes, eventType) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                if (eventType is not null && !eventType.IsInstanceOfType(@event)) continue;
                RiteCall.Run(callback, @event, control);
            }
        }
        
        foreach (var (callback, excludes, eventType, sourceType) in _guiInputGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (eventType is not null && !eventType.IsInstanceOfType(@event)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(control)) continue;
            RiteCall.Run(callback, @event, control);
        }
    }

    private void OnControlMinimumSizeChanged(Control control, string path)
    {
        if (_minimumSizeChangedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                RiteCall.Run(callback, control);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _minimumSizeChangedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(control)) continue;
            RiteCall.Run(callback, control);
        }
    }

    private void OnCollision2DMouseEntered(CollisionObject2D collision, string path)
    {
        if (_mouseEnteredCollision2DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                RiteCall.Run(callback, collision);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _mouseEnteredCollision2DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(collision)) continue;
            RiteCall.Run(callback, collision);
        }
    }

    private void OnCollision2DMouseExited(CollisionObject2D collision, string path)
    {
        if (_mouseExitedCollision2DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                RiteCall.Run(callback, collision);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _mouseExitedCollision2DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(collision)) continue;
            RiteCall.Run(callback, collision);
        }
    }

    private void OnCollision3DMouseEntered(CollisionObject3D collision, string path)
    {
        if (_mouseEnteredCollision3DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                RiteCall.Run(callback, collision);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _mouseEnteredCollision3DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(collision)) continue;
            RiteCall.Run(callback, collision);
        }
    }

    private void OnCollision3DMouseExited(CollisionObject3D collision, string path)
    {
        if (_mouseExitedCollision3DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                RiteCall.Run(callback, collision);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _mouseExitedCollision3DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(collision)) continue;
            RiteCall.Run(callback, collision);
        }
    }
}

