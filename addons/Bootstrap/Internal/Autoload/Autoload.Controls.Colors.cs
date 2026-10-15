using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace Bootstrap.Internal;

public partial class Autoload
{
    private void SetupColorChangedCallbacks()
    {
        var methods = ScanForMethods<OnColorChanged>();
        
        foreach (var (method, attr) in methods)
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateColorChangedCallback(method);
            if (callback is null) continue;
            
            HashSet<string>? excludeSet = null;
            if (!string.IsNullOrEmpty(attr.Exclude))
            {
                excludeSet = attr.Exclude
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(NormalizePath)
                    .ToHashSet();
            }
            
            var typeName = sourceType?.Name ?? "ColorPicker/ColorPickerButton";
            BootstrapReport.AddControlHook("OnColorChanged", methodName, attr.Target, attr.Exclude, attr.Priority, typeName);

            if (attr.Target is null) _colorChangedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_colorChangedTargetedCallbacks.ContainsKey(normalized)) _colorChangedTargetedCallbacks[normalized] = [];
                    _colorChangedTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
    }
    
    private static (Action<Color, Control>? Callback, Type? SourceType) CreateColorChangedCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        int? colorIdx = null;
        int? sourceIdx = null;
        Type? sourceType = null;
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            
            if (paramType == typeof(Color))
            {
                colorIdx = i;
            }
            else if (typeof(ColorPicker).IsAssignableFrom(paramType) || 
                     typeof(ColorPickerButton).IsAssignableFrom(paramType))
            {
                sourceIdx = i;
                sourceType = paramType;
            }
            else
            {
                BootstrapReport.AddInvalidParameterError(methodName, "OnColorChanged", parameters[i].Name ?? "parameters[i]", paramType, "Color (value) and/or ColorPicker, ColorPickerButton");
                return (null, null);
            }
        }
        
        var callback = DelegateFactory.CreateSignalCallback<Color, Control>(method, sourceType, colorIdx, sourceIdx);
        if (callback is not null) return (callback, sourceType);

        BootstrapReport.AddMethodError(methodName, "OnColorChanged", "Failed to create callback.");
        return (null, null);

    }
    
    private void SubscribeColorPicker(ColorPicker colorPicker, string path)
    {
        if (!_subscribedColorPickers.Add(colorPicker)) return;
        
        var hasColorChanged = _colorChangedGlobalCallbacks.Count > 0 || _colorChangedTargetedCallbacks.ContainsKey(path);
        
        if (hasColorChanged)
        {
            colorPicker.ColorChanged += color => OnColorPickerColorChanged(color, colorPicker, path);
        }
    }
    
    private void SubscribeColorPickerButton(ColorPickerButton colorPickerButton, string path)
    {
        if (!_subscribedColorPickerButtons.Add(colorPickerButton)) return;
        
        var hasColorChanged = _colorChangedGlobalCallbacks.Count > 0 || _colorChangedTargetedCallbacks.ContainsKey(path);
        
        if (hasColorChanged)
        {
            colorPickerButton.ColorChanged += color => OnColorPickerButtonColorChanged(color, colorPickerButton, path);
        }
    }
    
    private void OnColorPickerColorChanged(Color color, ColorPicker source, string path)
    {
        if (_colorChangedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (excludes?.Contains(path) == true) continue;
                callback(color, source);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _colorChangedGlobalCallbacks)
        {
            if (excludes?.Contains(path) == true) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(source)) continue;
            callback(color, source);
        }
    }
    
    private void OnColorPickerButtonColorChanged(Color color, ColorPickerButton source, string path)
    {
        if (_colorChangedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (excludes?.Contains(path) == true) continue;
                callback(color, source);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _colorChangedGlobalCallbacks)
        {
            if (excludes?.Contains(path) == true) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(source)) continue;
            callback(color, source);
        }
    }
}