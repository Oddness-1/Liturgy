using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    // Drag started/ended callbacks for Slider
    private readonly List<(Action<Slider> Callback, HashSet<string>? Excludes, Type? SourceType)> _dragStartedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Slider> Callback, HashSet<string>? Excludes)>> _dragStartedTargetedCallbacks = new();
    
    private readonly List<(Action<bool, Slider> Callback, HashSet<string>? Excludes, Type? SourceType)> _dragEndedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<bool, Slider> Callback, HashSet<string>? Excludes)>> _dragEndedTargetedCallbacks = new();
    
    // Scroll started/ended callbacks for ScrollContainer
    private readonly List<(Action<ScrollContainer> Callback, HashSet<string>? Excludes)> _scrollStartedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<ScrollContainer> Callback, HashSet<string>? Excludes)>> _scrollStartedTargetedCallbacks = new();
    
    private readonly List<(Action<ScrollContainer> Callback, HashSet<string>? Excludes)> _scrollEndedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<ScrollContainer> Callback, HashSet<string>? Excludes)>> _scrollEndedTargetedCallbacks = new();
    
    // Sort children callbacks for Container
    private readonly List<(Action<Container> Callback, HashSet<string>? Excludes, Type? SourceType)> _sortChildrenGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Container> Callback, HashSet<string>? Excludes)>> _sortChildrenTargetedCallbacks = new();
    
    private readonly HashSet<Slider> _subscribedSliders = [];
    private readonly HashSet<ScrollContainer> _subscribedScrollContainers = [];
    private readonly HashSet<Container> _subscribedContainers = [];

    private void SetupMiscControlCallbacks()
    {
        SetupDragStartedCallbacks();
        SetupDragEndedCallbacks();
        SetupScrollStartedCallbacks();
        SetupScrollEndedCallbacks();
        SetupSortChildrenCallbacks();
        
        var hasCallbacks = _dragStartedGlobalCallbacks.Count > 0 || _dragStartedTargetedCallbacks.Count > 0 ||
                           _dragEndedGlobalCallbacks.Count > 0 || _dragEndedTargetedCallbacks.Count > 0 ||
                           _scrollStartedGlobalCallbacks.Count > 0 || _scrollStartedTargetedCallbacks.Count > 0 ||
                           _scrollEndedGlobalCallbacks.Count > 0 || _scrollEndedTargetedCallbacks.Count > 0 ||
                           _sortChildrenGlobalCallbacks.Count > 0 || _sortChildrenTargetedCallbacks.Count > 0;
        
        if (!hasCallbacks) return;
        
        SubscribeToExistingMiscControls(_root);
        GetTree().NodeAdded += OnNodeAddedForMiscControls;
        GetTree().NodeRemoved += OnNodeRemovedForMiscControls;
    }

    private void SetupDragStartedCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnDragStarted>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateSliderCallback(method, "OnDragStarted");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnDragStarted", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name ?? "Slider");
            
            if (attr.Target is null)
                _dragStartedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            else
                AddTargetedSliderCallbacks(_dragStartedTargetedCallbacks, attr.Target, callback, excludeSet);
        }
    }

    private void SetupDragEndedCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnDragEnded>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateDragEndedCallback(method);
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnDragEnded", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name ?? "Slider");
            
            if (attr.Target is null)
                _dragEndedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            else
                AddTargetedDragEndedCallbacks(_dragEndedTargetedCallbacks, attr.Target, callback, excludeSet);
        }
    }

    private void SetupScrollStartedCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnScrollStarted>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var callback = CreateScrollContainerCallback(method, "OnScrollStarted");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnScrollStarted", methodName, attr.Target, attr.Exclude, attr.Priority, "ScrollContainer");
            
            if (attr.Target is null)
                _scrollStartedGlobalCallbacks.Add((callback, excludeSet));
            else
                AddTargetedScrollCallbacks(_scrollStartedTargetedCallbacks, attr.Target, callback, excludeSet);
        }
    }

    private void SetupScrollEndedCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnScrollEnded>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var callback = CreateScrollContainerCallback(method, "OnScrollEnded");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnScrollEnded", methodName, attr.Target, attr.Exclude, attr.Priority, "ScrollContainer");
            
            if (attr.Target is null)
                _scrollEndedGlobalCallbacks.Add((callback, excludeSet));
            else
                AddTargetedScrollCallbacks(_scrollEndedTargetedCallbacks, attr.Target, callback, excludeSet);
        }
    }

    private void SetupSortChildrenCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnSortChildren>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateContainerCallback(method, "OnSortChildren");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnSortChildren", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name ?? "Container");
            
            if (attr.Target is null)
                _sortChildrenGlobalCallbacks.Add((callback, excludeSet, sourceType));
            else
                AddTargetedContainerCallbacks(_sortChildrenTargetedCallbacks, attr.Target, callback, excludeSet);
        }
    }

    private static (Action<Slider>? Callback, Type? SourceType) CreateSliderCallback(MethodInfo method, string hookName)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        if (parameters.Length == 0)
        {
            var del = (Action)RiteProbe.Create(typeof(Action), method);
            return (_ => del(), null);
        }
        
        if (parameters.Length > 1)
        {
            LiturgyReport.AddMethodError(methodName, hookName, "Must have zero or one parameter.");
            return (null, null);
        }
        
        var paramType = parameters[0].ParameterType;
        if (!typeof(Slider).IsAssignableFrom(paramType))
        {
            LiturgyReport.AddInvalidParameterError(methodName, hookName, parameters[0].Name ?? "arg0", paramType, "Slider subtypes");
            return (null, null);
        }
        
        var callback = DelegateFactory.CreateCallback<Slider>(method, paramType);
        return (callback, paramType);
    }

    private static (Action<bool, Slider>? Callback, Type? SourceType) CreateDragEndedCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        if (parameters.Length == 0)
        {
            var del = (Action)RiteProbe.Create(typeof(Action), method);
            return ((_, _) => del(), null);
        }
        
        if (parameters.Length > 2)
        {
            LiturgyReport.AddMethodError(methodName, "OnDragEnded", "Must have at most two parameters.");
            return (null, null);
        }
        
        int? boolIdx = null;
        int? sliderIdx = null;
        Type? sliderType = null;
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            
            if (paramType == typeof(bool))
                boolIdx = i;
            else if (typeof(Slider).IsAssignableFrom(paramType))
            {
                sliderIdx = i;
                sliderType = paramType;
            }
            else
            {
                LiturgyReport.AddInvalidParameterError(methodName, "OnDragEnded", parameters[i].Name ?? $"arg{i}", paramType,
                    "bool (value_changed) and/or Slider subtypes");
                return (null, null);
            }
        }
        
        if (parameters.Length == 1)
        {
            if (boolIdx is not null)
            {
                var del = (Action<bool>)RiteProbe.Create(typeof(Action<bool>), method);
                return ((b, _) => del(b), null);
            }
            else
            {
                var del = DelegateFactory.CreateCallback<Slider>(method, sliderType!);
                if (del is null) return (null, null);
                return ((_, s) => del(s), sliderType);
            }
        }
        
        // Two parameters
        if (boolIdx == 0)
        {
            var del = DelegateFactory.CreateSignalCallback<bool, Slider>(method, sliderType!, 0, 1);
            return (del, sliderType);
        }
        else
        {
            var del = DelegateFactory.CreateSignalCallback<bool, Slider>(method, sliderType!, 1, 0);
            if (del is null) return (null, null);
            return ((b, s) => del(b, s), sliderType);
        }
    }

    private static Action<ScrollContainer>? CreateScrollContainerCallback(MethodInfo method, string hookName)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        if (parameters.Length == 0)
        {
            var del = (Action)RiteProbe.Create(typeof(Action), method);
            return _ => del();
        }
        
        if (parameters.Length > 1)
        {
            LiturgyReport.AddMethodError(methodName, hookName, "Must have zero or one parameter.");
            return null;
        }
        
        var paramType = parameters[0].ParameterType;
        if (paramType != typeof(ScrollContainer))
        {
            LiturgyReport.AddInvalidParameterError(methodName, hookName, parameters[0].Name ?? "arg0", paramType, "ScrollContainer");
            return null;
        }
        
        return (Action<ScrollContainer>)RiteProbe.Create(typeof(Action<ScrollContainer>), method);
    }

    private static (Action<Container>? Callback, Type? SourceType) CreateContainerCallback(MethodInfo method, string hookName)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        if (parameters.Length == 0)
        {
            var del = (Action)RiteProbe.Create(typeof(Action), method);
            return (_ => del(), null);
        }
        
        if (parameters.Length > 1)
        {
            LiturgyReport.AddMethodError(methodName, hookName, "Must have zero or one parameter.");
            return (null, null);
        }
        
        var paramType = parameters[0].ParameterType;
        if (!typeof(Container).IsAssignableFrom(paramType))
        {
            LiturgyReport.AddInvalidParameterError(methodName, hookName, parameters[0].Name ?? "arg0", paramType, "Container subtypes");
            return (null, null);
        }
        
        var callback = DelegateFactory.CreateCallback<Container>(method, paramType);
        return (callback, paramType);
    }

    private static void AddTargetedSliderCallbacks(Dictionary<string, List<(Action<Slider>, HashSet<string>?)>> dict, string target, Action<Slider> callback, HashSet<string>? excludeSet)
    {
        foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var n = NormalizePath(t);
            if (!dict.ContainsKey(n)) dict[n] = [];
            dict[n].Add((callback, excludeSet));
        }
    }

    private static void AddTargetedDragEndedCallbacks(Dictionary<string, List<(Action<bool, Slider>, HashSet<string>?)>> dict, string target, Action<bool, Slider> callback, HashSet<string>? excludeSet)
    {
        foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var n = NormalizePath(t);
            if (!dict.ContainsKey(n)) dict[n] = [];
            dict[n].Add((callback, excludeSet));
        }
    }

    private static void AddTargetedScrollCallbacks(Dictionary<string, List<(Action<ScrollContainer>, HashSet<string>?)>> dict, string target, Action<ScrollContainer> callback, HashSet<string>? excludeSet)
    {
        foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var n = NormalizePath(t);
            if (!dict.ContainsKey(n)) dict[n] = [];
            dict[n].Add((callback, excludeSet));
        }
    }

    private static void AddTargetedContainerCallbacks(Dictionary<string, List<(Action<Container>, HashSet<string>?)>> dict, string target, Action<Container> callback, HashSet<string>? excludeSet)
    {
        foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var n = NormalizePath(t);
            if (!dict.ContainsKey(n)) dict[n] = [];
            dict[n].Add((callback, excludeSet));
        }
    }

    private void SubscribeToExistingMiscControls(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (IsShielded(node)) continue;
            SubscribeMiscControlNode(node);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    private void OnNodeAddedForMiscControls(Node node)
    {
        if (IsHiddenFromRites(node)) return;
        SubscribeMiscControlNode(node);
    }

    private void OnNodeRemovedForMiscControls(Node node)
    {
        // A ScrollContainer is also a Container, with rites of both kinds, so these aren't one switch.
        if (node is Slider slider) _subscribedSliders.Remove(slider);
        if (node is ScrollContainer scroll) _subscribedScrollContainers.Remove(scroll);
        if (node is Container container) _subscribedContainers.Remove(container);
    }

    private void SubscribeMiscControlNode(Node node)
    {
        if (node is Slider slider) SubscribeSlider(slider);
        if (node is ScrollContainer scroll) SubscribeScrollContainer(scroll);
        if (node is Container container) SubscribeContainer(container);
    }

    private void SubscribeSlider(Slider slider)
    {
        if (_subscribedSliders.Contains(slider) || IsHiddenFromRites(slider)) return;
        
        var path = slider.GetPath().ToString();
        var hasDragStarted = _dragStartedTargetedCallbacks.ContainsKey(path) || _dragStartedGlobalCallbacks.Count > 0;
        var hasDragEnded = _dragEndedTargetedCallbacks.ContainsKey(path) || _dragEndedGlobalCallbacks.Count > 0;
        if (!hasDragStarted && !hasDragEnded) return;
        
        _subscribedSliders.Add(slider);
        
        if (hasDragStarted)
        {
            Listen(slider, Slider.SignalName.DragStarted, Callable.From(() =>
            {
                if (_dragStartedTargetedCallbacks.TryGetValue(path, out var targeted))
                {
                    foreach (var (callback, excludes) in targeted)
                    {
                        if (PathMatchesAnyExclude(path, excludes)) continue;
                        RiteCall.Run(callback, slider);
                    }
                }
                
                foreach (var (callback, excludes, sourceType) in _dragStartedGlobalCallbacks)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    if (sourceType is not null && !sourceType.IsInstanceOfType(slider)) continue;
                    RiteCall.Run(callback, slider);
                }
            }));
        }
        
        if (hasDragEnded)
        {
            Listen(slider, Slider.SignalName.DragEnded, Callable.From((bool valueChanged) =>
            {
                if (_dragEndedTargetedCallbacks.TryGetValue(path, out var targeted))
                {
                    foreach (var (callback, excludes) in targeted)
                    {
                        if (PathMatchesAnyExclude(path, excludes)) continue;
                        RiteCall.Run(callback, valueChanged, slider);
                    }
                }
                
                foreach (var (callback, excludes, sourceType) in _dragEndedGlobalCallbacks)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    if (sourceType is not null && !sourceType.IsInstanceOfType(slider)) continue;
                    RiteCall.Run(callback, valueChanged, slider);
                }
            }));
        }
    }

    private void SubscribeScrollContainer(ScrollContainer scroll)
    {
        if (_subscribedScrollContainers.Contains(scroll) || IsHiddenFromRites(scroll)) return;
    
        var path = scroll.GetPath().ToString();
        var hasStarted = _scrollStartedTargetedCallbacks.ContainsKey(path) || _scrollStartedGlobalCallbacks.Count > 0;
        var hasEnded = _scrollEndedTargetedCallbacks.ContainsKey(path) || _scrollEndedGlobalCallbacks.Count > 0;
        if (!hasStarted && !hasEnded) return;
    
        _subscribedScrollContainers.Add(scroll);
    
        if (hasStarted)
        {
            Listen(scroll, ScrollContainer.SignalName.ScrollStarted, Callable.From(() =>
            {
                if (_scrollStartedTargetedCallbacks.TryGetValue(path, out var targeted))
                {
                    foreach (var (callback, excludes) in targeted)
                    {
                        if (PathMatchesAnyExclude(path, excludes)) continue;
                        RiteCall.Run(callback, scroll);
                    }
                }
            
                foreach (var (callback, excludes) in _scrollStartedGlobalCallbacks)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    RiteCall.Run(callback, scroll);
                }
            }));
        }
    
        if (hasEnded)
        {
            Listen(scroll, ScrollContainer.SignalName.ScrollEnded, Callable.From(() =>
            {
                if (_scrollEndedTargetedCallbacks.TryGetValue(path, out var targeted))
                {
                    foreach (var (callback, excludes) in targeted)
                    {
                        if (PathMatchesAnyExclude(path, excludes)) continue;
                        RiteCall.Run(callback, scroll);
                    }
                }
            
                foreach (var (callback, excludes) in _scrollEndedGlobalCallbacks)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    RiteCall.Run(callback, scroll);
                }
            }));
        }
    }

    private void SubscribeContainer(Container container)
    {
        if (_subscribedContainers.Contains(container) || IsHiddenFromRites(container)) return;
    
        var path = container.GetPath().ToString();
        var hasAny = _sortChildrenTargetedCallbacks.ContainsKey(path) || _sortChildrenGlobalCallbacks.Count > 0;
        if (!hasAny) return;
    
        _subscribedContainers.Add(container);
        Listen(container, Container.SignalName.SortChildren, Callable.From(() =>
        {
            if (_sortChildrenTargetedCallbacks.TryGetValue(path, out var targeted))
            {
                foreach (var (callback, excludes) in targeted)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    RiteCall.Run(callback, container);
                }
            }
        
            foreach (var (callback, excludes, sourceType) in _sortChildrenGlobalCallbacks)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                if (sourceType is not null && !sourceType.IsInstanceOfType(container)) continue;
                RiteCall.Run(callback, container);
            }
        }));
    }
}

