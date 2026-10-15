using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace Bootstrap.Internal;

public partial class Autoload
{
    private void SetupButtonCallbacks()
    {
        SetupButtonHook<OnButtonPressed, BaseButton>(
            _buttonPressedGlobalCallbacks,
            _buttonPressedTargetedCallbacks,
            attr => attr.Target,
            attr => attr.Exclude,
            attr => attr.Priority,
            "OnButtonPressed",
            ValidateButtonSourceMethod
        );
        
        SetupButtonHook<OnButtonUp, BaseButton>(
            _buttonUpGlobalCallbacks,
            _buttonUpTargetedCallbacks,
            attr => attr.Target,
            attr => attr.Exclude,
            attr => attr.Priority,
            "OnButtonUp",
            ValidateButtonSourceMethod
        );
        
        SetupButtonHook<OnButtonDown, BaseButton>(
            _buttonDownGlobalCallbacks,
            _buttonDownTargetedCallbacks,
            attr => attr.Target,
            attr => attr.Exclude,
            attr => attr.Priority,
            "OnButtonDown",
            ValidateButtonSourceMethod
        );
        
        SetupButtonToggledHook();
        
        var hasButtonCallbacks = _buttonPressedGlobalCallbacks.Count > 0
            || _buttonUpGlobalCallbacks.Count > 0
            || _buttonDownGlobalCallbacks.Count > 0
            || _buttonToggledGlobalCallbacks.Count > 0
            || _buttonPressedTargetedCallbacks.Count > 0
            || _buttonUpTargetedCallbacks.Count > 0
            || _buttonDownTargetedCallbacks.Count > 0
            || _buttonToggledTargetedCallbacks.Count > 0;
        
        if (!hasButtonCallbacks) return;
        
        SubscribeToExistingNodes<BaseButton>(SubscribeToButton);
        
        GetTree().NodeAdded += OnNodeAddedForButtons;
        GetTree().NodeRemoved += OnNodeRemovedForButtons;
    }

    private void SetupButtonHook<TAttr, TSource>(
        List<(Action<TSource> Callback, HashSet<string>? Excludes)> globalCallbacks,
        Dictionary<string, List<(Action<TSource> Callback, HashSet<string>? Excludes)>> targetedCallbacks,
        Func<TAttr, string?> getTarget,
        Func<TAttr, string?> getExclude,
        Func<TAttr, int> getPriority,
        string attrName,
        Func<MethodInfo, (bool Valid, Type? SourceType)> validateMethod) where TAttr : Attribute where TSource : class
    {
        var methods = ScanForMethods<TAttr>();
        var globalEntries = new List<(MethodInfo Method, int Priority, Type? SourceType, HashSet<string>? Excludes)>();
        var targetedEntries = new Dictionary<string, List<(MethodInfo Method, int Priority, Type? SourceType, HashSet<string>? Excludes)>>();

        foreach (var (method, attr) in methods)
        {
            var (valid, sourceType) = validateMethod(method);
            if (!valid) continue;

            var target = getTarget(attr);
            var exclude = getExclude(attr);
            var priority = getPriority(attr);
            
            HashSet<string>? excludeSet = null;
            if (!string.IsNullOrEmpty(exclude))
            {
                excludeSet = exclude
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(NormalizePath)
                    .ToHashSet();
            }

            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var typeFilter = sourceType?.Name;
            
            // Report to Markdown log
            BootstrapReport.AddControlHook(attrName, methodName, target, exclude, priority, typeFilter);

            if (target is null)
            {
                globalEntries.Add((method, priority, sourceType, excludeSet));
            }
            else
            {
                var targets = target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var t in targets)
                {
                    var normalizedTarget = NormalizePath(t);
                    if (!targetedEntries.ContainsKey(normalizedTarget))
                        targetedEntries[normalizedTarget] = [];
                    targetedEntries[normalizedTarget].Add((method, priority, sourceType, excludeSet));
                }
            }
        }

        foreach (var (method, _, sourceType, excludeSet) in globalEntries.OrderBy(e => e.Priority))
        {
            var callback = DelegateFactory.CreateCallback<TSource>(method, sourceType);
            if (callback is not null)
            {
                globalCallbacks.Add((callback, excludeSet));
            }
        }

        foreach (var (targetPath, entries) in targetedEntries)
        {
            if (!targetedCallbacks.ContainsKey(targetPath))
                targetedCallbacks[targetPath] = [];
                
            foreach (var (method, _, sourceType, excludeSet) in entries.OrderBy(e => e.Priority))
            {
                var callback = DelegateFactory.CreateCallback<TSource>(method, sourceType);
                if (callback is not null)
                {
                    targetedCallbacks[targetPath].Add((callback, excludeSet));
                }
            }
        }
    }

    private void SetupButtonToggledHook()
    {
        var methods = ScanForMethods<OnButtonToggled>();
        var globalEntries = new List<(MethodInfo Method, int Priority, int? BoolIdx, int? SourceIdx, Type? SourceType, HashSet<string>? Excludes)>();
        var targetedEntries = new Dictionary<string, List<(MethodInfo Method, int Priority, int? BoolIdx, int? SourceIdx, Type? SourceType, HashSet<string>? Excludes)>>();

        foreach (var (method, attr) in methods)
        {
            if (!ValidateButtonToggledMethod(method, out var boolIdx, out var sourceIdx, out var sourceType))
                continue;

            HashSet<string>? excludeSet = null;
            if (!string.IsNullOrEmpty(attr.Exclude))
            {
                excludeSet = attr.Exclude
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(NormalizePath)
                    .ToHashSet();
            }

            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var typeFilter = sourceType?.Name;
            
            // Report to Markdown log
            BootstrapReport.AddControlHook("OnButtonToggled", methodName, attr.Target, attr.Exclude, attr.Priority, typeFilter);

            if (attr.Target is null)
            {
                globalEntries.Add((method, attr.Priority, boolIdx, sourceIdx, sourceType, excludeSet));
            }
            else
            {
                var targets = attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var t in targets)
                {
                    var normalizedTarget = NormalizePath(t);
                    if (!targetedEntries.ContainsKey(normalizedTarget))
                        targetedEntries[normalizedTarget] = [];
                    targetedEntries[normalizedTarget].Add((method, attr.Priority, boolIdx, sourceIdx, sourceType, excludeSet));
                }
            }
        }

        foreach (var (method, _, boolIdx, sourceIdx, sourceType, excludeSet) in globalEntries.OrderBy(e => e.Priority))
        {
            var callback = DelegateFactory.CreateSignalCallback<bool, BaseButton>(method, sourceType, boolIdx, sourceIdx);
            if (callback is not null)
            {
                _buttonToggledGlobalCallbacks.Add((callback, excludeSet));
            }
        }

        foreach (var (targetPath, entries) in targetedEntries)
        {
            if (!_buttonToggledTargetedCallbacks.ContainsKey(targetPath))
                _buttonToggledTargetedCallbacks[targetPath] = [];
                
            foreach (var (method, _, boolIdx, sourceIdx, sourceType, excludeSet) in entries.OrderBy(e => e.Priority))
            {
                var callback = DelegateFactory.CreateSignalCallback<bool, BaseButton>(method, sourceType, boolIdx, sourceIdx);
                if (callback is not null)
                {
                    _buttonToggledTargetedCallbacks[targetPath].Add((callback, excludeSet));
                }
            }
        }
    }

    private static (bool Valid, Type? SourceType) ValidateButtonSourceMethod(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";

        if (parameters.Length > 1)
        {
            BootstrapReport.AddMethodError(methodName, "OnButtonPressed/Up/Down", "Must have zero or one parameter.");
            return (false, null);
        }

        if (parameters.Length != 1) return (true, null);

        var paramType = parameters[0].ParameterType;
        if (typeof(BaseButton).IsAssignableFrom(paramType)) return (true, paramType);

        BootstrapReport.AddInvalidParameterError(methodName, "OnButtonPressed/Up/Down", parameters[0].Name ?? "parameters[0]", paramType, "BaseButton (Button, CheckBox, CheckButton, etc.)");
        return (false, null);

    }

    private static bool ValidateButtonToggledMethod(MethodInfo method, out int? boolIdx, out int? sourceIdx, out Type? sourceType)
    {
        boolIdx = null;
        sourceIdx = null;
        sourceType = null;
        
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";

        if (parameters.Length > 2)
        {
            BootstrapReport.AddMethodError(methodName, "OnButtonToggled", "Must have at most two parameters.");
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            
            if (param.ParameterType == typeof(bool))
            {
                if (boolIdx is not null)
                {
                    BootstrapReport.AddMethodError(methodName, "OnButtonToggled", "Has multiple bool parameters.");
                    return false;
                }
                boolIdx = i;
            }
            else if (typeof(BaseButton).IsAssignableFrom(param.ParameterType))
            {
                if (sourceIdx is not null)
                {
                    BootstrapReport.AddMethodError(methodName, "OnButtonToggled", "Has multiple BaseButton parameters.");
                    return false;
                }
                sourceIdx = i;
                sourceType = param.ParameterType;
            }
            else
            {
                BootstrapReport.AddInvalidParameterError(methodName, "OnButtonToggled", param.Name ?? "param", param.ParameterType, "bool (pressed) and/or BaseButton (Button, CheckBox, CheckButton, etc.)");
                return false;
            }
        }

        return true;
    }

    private void SubscribeToExistingNodes<TNode>(Action<TNode> subscriber) where TNode : Node
    {
        var stack = new Stack<Node>();
        stack.Push(_root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            
            if (node is TNode typedNode)
            {
                subscriber(typedNode);
            }

            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }
    }

    private void OnNodeAddedForButtons(Node node)
    {
        if (IsInternalNode(node)) return;
        if (node is BaseButton button) SubscribeToButton(button);
    }

    private void OnNodeRemovedForButtons(Node node)
    {
        if (node is BaseButton button) _subscribedButtons.Remove(button);
    }

    private void SubscribeToButton(BaseButton button)
    {
        if (!_subscribedButtons.Add(button)) return;

        var path = button.GetPath().ToString();
        if (_buttonPressedGlobalCallbacks.Count > 0 || _buttonPressedTargetedCallbacks.ContainsKey(path))
            button.Pressed += () => OnButtonPressedSignal(button);
        
        if (_buttonUpGlobalCallbacks.Count > 0 || _buttonUpTargetedCallbacks.ContainsKey(path))
            button.ButtonUp += () => OnButtonUpSignal(button);
        
        if (_buttonDownGlobalCallbacks.Count > 0 || _buttonDownTargetedCallbacks.ContainsKey(path))
            button.ButtonDown += () => OnButtonDownSignal(button);
        
        if (_buttonToggledGlobalCallbacks.Count > 0 || _buttonToggledTargetedCallbacks.ContainsKey(path))
            button.Toggled += pressed => OnButtonToggledSignal(pressed, button);
    }

    private void OnButtonPressedSignal(BaseButton button)
    {
        var path = button.GetPath().ToString();

        if (_buttonPressedTargetedCallbacks.TryGetValue(path, out var targetedCallbacks))
        {
            foreach (var (callback, excludes) in targetedCallbacks)
            {
                if (excludes?.Contains(path) == true) continue;
                callback(button);
            }
        }

        foreach (var (callback, excludes) in _buttonPressedGlobalCallbacks)
        {
            if (excludes?.Contains(path) == true) continue;
            callback(button);
        }
    }

    private void OnButtonUpSignal(BaseButton button)
    {
        var path = button.GetPath().ToString();

        if (_buttonUpTargetedCallbacks.TryGetValue(path, out var targetedCallbacks))
        {
            foreach (var (callback, excludes) in targetedCallbacks)
            {
                if (excludes?.Contains(path) == true) continue;
                callback(button);
            }
        }

        foreach (var (callback, excludes) in _buttonUpGlobalCallbacks)
        {
            if (excludes?.Contains(path) == true) continue;
            callback(button);
        }
    }

    private void OnButtonDownSignal(BaseButton button)
    {
        var path = button.GetPath().ToString();

        if (_buttonDownTargetedCallbacks.TryGetValue(path, out var targetedCallbacks))
        {
            foreach (var (callback, excludes) in targetedCallbacks)
            {
                if (excludes?.Contains(path) == true) continue;
                callback(button);
            }
        }

        foreach (var (callback, excludes) in _buttonDownGlobalCallbacks)
        {
            if (excludes?.Contains(path) == true) continue;
            callback(button);
        }
    }

    private void OnButtonToggledSignal(bool pressed, BaseButton button)
    {
        var path = button.GetPath().ToString();

        if (_buttonToggledTargetedCallbacks.TryGetValue(path, out var targetedCallbacks))
        {
            foreach (var (callback, excludes) in targetedCallbacks)
            {
                if (excludes?.Contains(path) == true) continue;
                callback(pressed, button);
            }
        }

        foreach (var (callback, excludes) in _buttonToggledGlobalCallbacks)
        {
            if (excludes?.Contains(path) == true) continue;
            callback(pressed, button);
        }
    }
}
