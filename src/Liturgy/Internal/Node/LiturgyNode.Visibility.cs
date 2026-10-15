using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    // Visibility callbacks for CanvasItem (2D)
    private readonly List<(Action<CanvasItem> Callback, HashSet<string>? Excludes, Type? SourceType)> _visibilityChangedCanvasItemGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<CanvasItem> Callback, HashSet<string>? Excludes)>> _visibilityChangedCanvasItemTargetedCallbacks = new();
    
    // Visibility callbacks for Node3D
    private readonly List<(Action<Node3D> Callback, HashSet<string>? Excludes, Type? SourceType)> _visibilityChangedNode3DGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Node3D> Callback, HashSet<string>? Excludes)>> _visibilityChangedNode3DTargetedCallbacks = new();
    
    // Track subscribed nodes
    private readonly HashSet<CanvasItem> _subscribedCanvasItemsForVisibility = [];
    private readonly HashSet<Node3D> _subscribedNode3DsForVisibility = [];

    private void SetupVisibilityCallbacks()
    {
        var methods = ScanForMethods<OnVisibilityChanged>();
        
        foreach (var (method, attr) in methods.OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var parameters = method.GetParameters();
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            if (parameters.Length == 0)
            {
                // No parameters - register for both types
                var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
                LiturgyReport.AddControlHook("OnVisibilityChanged", methodName, attr.Target, attr.Exclude, attr.Priority, null);
                
                AddVisibilityCallback(_visibilityChangedCanvasItemGlobalCallbacks, _visibilityChangedCanvasItemTargetedCallbacks, 
                    (Action<CanvasItem>)(_ => del()), excludeSet, null, attr.Target);
                AddVisibilityCallback(_visibilityChangedNode3DGlobalCallbacks, _visibilityChangedNode3DTargetedCallbacks,
                    (Action<Node3D>)(_ => del()), excludeSet, null, attr.Target);
            }
            else if (parameters.Length == 1)
            {
                var paramType = parameters[0].ParameterType;
                
                if (typeof(CanvasItem).IsAssignableFrom(paramType))
                {
                    var callback = DelegateFactory.CreateCallback<CanvasItem>(method, paramType);
                    if (callback is not null)
                    {
                        LiturgyReport.AddControlHook("OnVisibilityChanged", methodName, attr.Target, attr.Exclude, attr.Priority, paramType.Name);
                        AddVisibilityCallback(_visibilityChangedCanvasItemGlobalCallbacks, _visibilityChangedCanvasItemTargetedCallbacks, 
                            callback, excludeSet, paramType, attr.Target);
                    }
                }
                else if (typeof(Node3D).IsAssignableFrom(paramType))
                {
                    var callback = DelegateFactory.CreateCallback<Node3D>(method, paramType);
                    if (callback is not null)
                    {
                        LiturgyReport.AddControlHook("OnVisibilityChanged", methodName, attr.Target, attr.Exclude, attr.Priority, paramType.Name);
                        AddVisibilityCallback(_visibilityChangedNode3DGlobalCallbacks, _visibilityChangedNode3DTargetedCallbacks,
                            callback, excludeSet, paramType, attr.Target);
                    }
                }
                else
                {
                    LiturgyReport.AddInvalidParameterError(methodName, "OnVisibilityChanged", parameters[0].Name ?? "arg0", paramType, 
                        "CanvasItem or Node3D subtypes");
                }
            }
            else
            {
                LiturgyReport.AddMethodError(methodName, "OnVisibilityChanged", "Must have zero or one parameter.");
            }
        }
        
        var hasCallbacks = _visibilityChangedCanvasItemGlobalCallbacks.Count > 0 || 
                           _visibilityChangedCanvasItemTargetedCallbacks.Count > 0 ||
                           _visibilityChangedNode3DGlobalCallbacks.Count > 0 || 
                           _visibilityChangedNode3DTargetedCallbacks.Count > 0;
        
        if (!hasCallbacks) return;
        
        SubscribeToExistingNodesForVisibility(_root);
        GetTree().NodeAdded += OnNodeAddedForVisibility;
        GetTree().NodeRemoved += OnNodeRemovedForVisibility;
    }

    private static void AddVisibilityCallback<T>(
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

    private void SubscribeToExistingNodesForVisibility(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            SubscribeVisibilityNode(node);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    private void OnNodeAddedForVisibility(Node node)
    {
        if (IsInternalNode(node)) return;
        SubscribeVisibilityNode(node);
    }

    private void OnNodeRemovedForVisibility(Node node)
    {
        switch (node)
        {
            case CanvasItem ci: _subscribedCanvasItemsForVisibility.Remove(ci); break;
            case Node3D n3d: _subscribedNode3DsForVisibility.Remove(n3d); break;
        }
    }

    private void SubscribeVisibilityNode(Node node)
    {
        switch (node)
        {
            case CanvasItem canvasItem:
                SubscribeCanvasItemForVisibility(canvasItem);
                break;
            case Node3D node3D:
                SubscribeNode3DForVisibility(node3D);
                break;
        }
    }

    private void SubscribeCanvasItemForVisibility(CanvasItem canvasItem)
    {
        if (_subscribedCanvasItemsForVisibility.Contains(canvasItem)) return;
        if (IsInternalNode(canvasItem)) return;
        
        var path = canvasItem.GetPath().ToString();
        var hasTargeted = _visibilityChangedCanvasItemTargetedCallbacks.ContainsKey(path);
        var hasGlobal = _visibilityChangedCanvasItemGlobalCallbacks.Count > 0;
        
        if (!hasTargeted && !hasGlobal) return;
        
        _subscribedCanvasItemsForVisibility.Add(canvasItem);
        canvasItem.VisibilityChanged += () => OnCanvasItemVisibilityChanged(canvasItem, path);
    }

    private void SubscribeNode3DForVisibility(Node3D node3D)
    {
        if (_subscribedNode3DsForVisibility.Contains(node3D)) return;
        if (IsInternalNode(node3D)) return;
        
        var path = node3D.GetPath().ToString();
        var hasTargeted = _visibilityChangedNode3DTargetedCallbacks.ContainsKey(path);
        var hasGlobal = _visibilityChangedNode3DGlobalCallbacks.Count > 0;
        
        if (!hasTargeted && !hasGlobal) return;
        
        _subscribedNode3DsForVisibility.Add(node3D);
        node3D.VisibilityChanged += () => OnNode3DVisibilityChanged(node3D, path);
    }

    private void OnCanvasItemVisibilityChanged(CanvasItem canvasItem, string path)
    {
        if (_visibilityChangedCanvasItemTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(canvasItem);
            }
        }
    
        foreach (var (callback, excludes, sourceType) in _visibilityChangedCanvasItemGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(canvasItem)) continue;
            callback(canvasItem);
        }
    }

    private void OnNode3DVisibilityChanged(Node3D node3D, string path)
    {
        if (_visibilityChangedNode3DTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(node3D);
            }
        }
    
        foreach (var (callback, excludes, sourceType) in _visibilityChangedNode3DGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(node3D)) continue;
            callback(node3D);
        }
    }
}

