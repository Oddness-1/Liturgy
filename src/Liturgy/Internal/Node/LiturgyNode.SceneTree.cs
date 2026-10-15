using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    private void SetupSceneTreeCallbacks()
    {
        SetupNodeHook<OnNodeAdded>(_nodeAddedCallbacks, attr => attr.Priority, "OnNodeAdded");
        SetupNodeHook<OnNodeRemoved>(_nodeRemovedCallbacks, attr => attr.Priority, "OnNodeRemoved");
        SetupTreeChangedHook();

        if (_nodeAddedCallbacks.Count > 0) GetTree().NodeAdded += OnNodeAddedCallback;
        if (_nodeRemovedCallbacks.Count > 0) GetTree().NodeRemoved += OnNodeRemovedCallback;
        if (_treeChangedCallbacks.Count > 0) GetTree().TreeChanged += OnTreeChangedCallback;
    }

    private void SetupNodeHook<TAttr>(
        List<Action<Node>> callbacks,
        Func<TAttr, int> getPriority,
        string attrName) where TAttr : Attribute
    {
        var methods = ScanForMethods<TAttr>();
        var validEntries = new List<(MethodInfo Method, int Priority)>();

        foreach (var (method, attr) in methods)
        {
            if (!ValidateNodeParamMethod(method, attrName, out var nodeType))
                continue;

            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var priority = getPriority(attr);
            var typeFilter = nodeType?.Name;

            switch (attrName)
            {
                // Report to Markdown log
                case "OnNodeAdded": LiturgyReport.AddOnNodeAddedHook(methodName, priority, typeFilter); break;
                case "OnNodeRemoved": LiturgyReport.AddOnNodeRemovedHook(methodName, priority, typeFilter); break;
            }

            validEntries.Add((method, priority));
        }

        foreach (var (method, _) in validEntries.OrderBy(e => e.Priority))
        {
            if (!ValidateNodeParamMethod(method, attrName, out var nodeType)) continue;
                
            var callback = CreateNodeCallback(method, nodeType);
            if (callback is not null) callbacks.Add(callback);
        }
    }

    private static bool ValidateNodeParamMethod(MethodInfo method, string attrName, out Type? nodeType)
    {
        nodeType = null;
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";

        switch (parameters.Length)
        {
            case > 1:
                LiturgyReport.AddMethodError(methodName, attrName, "Must have zero or one parameter.");
                return false;
            
            case 1:
            {
                var paramType = parameters[0].ParameterType;
                if (!typeof(Node).IsAssignableFrom(paramType))
                {
                    LiturgyReport.AddInvalidParameterError(methodName, attrName, parameters[0].Name ?? "parameters[0]", paramType, "Node (or derived type)");
                    return false;
                }
                nodeType = paramType;
                break;
            }
        }

        return true;
    }

    private static Action<Node>? CreateNodeCallback(MethodInfo method, Type? nodeType) => DelegateFactory.CreateCallback<Node>(method, nodeType);

    private void SetupTreeChangedHook()
    {
        var methods = ScanForMethods<OnTreeChanged>();
        var validEntries = new List<(MethodInfo Method, int Priority)>();

        foreach (var (method, attr) in methods)
        {
            var parameters = method.GetParameters();
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";

            if (parameters.Length > 0)
            {
                LiturgyReport.AddMethodError(methodName, "OnTreeChanged", "Must be parameterless.");
                continue;
            }

            // Report to Markdown log
            LiturgyReport.AddOnTreeChangedHook(methodName, attr.Priority);
            validEntries.Add((method, attr.Priority));
        }

        foreach (var (method, _) in validEntries.OrderBy(e => e.Priority))
        {
            var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
            _treeChangedCallbacks.Add(del);
        }
    }

    private void OnNodeAddedCallback(Node node)
    {
        if (IsInternalNode(node)) return;
        foreach (var callback in _nodeAddedCallbacks) callback(node);
    }

    private void OnNodeRemovedCallback(Node node)
    {
        if (IsInternalNode(node)) return;
        foreach (var callback in _nodeRemovedCallbacks) callback(node);
    }

    private void OnTreeChangedCallback()
    {
        foreach (var callback in _treeChangedCallbacks) callback();
    }
}
