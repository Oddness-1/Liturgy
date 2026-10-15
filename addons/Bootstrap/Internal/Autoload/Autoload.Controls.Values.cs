using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Range = Godot.Range;

namespace Bootstrap.Internal;

public partial class Autoload
{
    private void SetupValueChangedCallbacks()
    {
        var methods = ScanForMethods<OnValueChanged>();
        
        foreach (var (method, attr) in methods)
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateValueChangedCallback(method);
            if (callback is null) continue;
            
            HashSet<string>? excludeSet = null;
            if (!string.IsNullOrEmpty(attr.Exclude))
            {
                excludeSet = attr.Exclude
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(NormalizePath)
                    .ToHashSet();
            }
            
            BootstrapReport.AddControlHook("OnValueChanged", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name ?? "Range");
            
            if (attr.Target is null)
            {
                _valueChangedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            }
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_valueChangedTargetedCallbacks.ContainsKey(normalized))
                        _valueChangedTargetedCallbacks[normalized] = [];
                    _valueChangedTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
    }
    
    private static (Action<double, Range>? Callback, Type? SourceType) CreateValueChangedCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        int? doubleIdx = null;
        int? sourceIdx = null;
        Type? sourceType = null;
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            
            if (paramType == typeof(double))
            {
                doubleIdx = i;
            }
            else if (typeof(Range).IsAssignableFrom(paramType))
            {
                sourceIdx = i;
                sourceType = paramType;
            }
            else
            {
                BootstrapReport.AddInvalidParameterError(methodName, "OnValueChanged", parameters[i].Name ?? "parameters[i]", paramType, "double (value) and/or Range (Slider, SpinBox, ScrollBar, ProgressBar)");
                return (null, null);
            }
        }
        
        var callback = DelegateFactory.CreateSignalCallback<double, Range>(method, sourceType, doubleIdx, sourceIdx);
        
        if (callback is not null) return (callback, sourceType);

        BootstrapReport.AddMethodError(methodName, "OnValueChanged", "Failed to create callback.");
        return (null, null);

    }
    
    private void SubscribeRange(Range range, string path)
    {
        if (!_subscribedRanges.Add(range)) return;
        
        var hasValueChanged = _valueChangedGlobalCallbacks.Count > 0 || _valueChangedTargetedCallbacks.ContainsKey(path);
        
        if (hasValueChanged) range.ValueChanged += value => OnRangeValueChanged(value, range, path);
    }
    
    private void OnRangeValueChanged(double value, Range source, string path)
    {
        if (_valueChangedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (excludes?.Contains(path) == true) continue;
                callback(value, source);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _valueChangedGlobalCallbacks)
        {
            if (excludes?.Contains(path) == true) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(source)) continue;
            callback(value, source);
        }
    }
}