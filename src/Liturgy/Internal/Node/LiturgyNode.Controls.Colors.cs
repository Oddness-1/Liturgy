using System;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    private void SetupColorChangedCallbacks()
    {
        var methods = ScanForMethods<OnColorChanged>();
        
        foreach (var (method, attr) in methods)
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateColorChangedCallback(method);
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            var typeName = sourceType?.Name ?? "ColorPicker/ColorPickerButton";
            LiturgyReport.AddControlHook("OnColorChanged", methodName, attr.Target, attr.Exclude, attr.Priority, typeName);

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
                LiturgyReport.AddInvalidParameterError(methodName, "OnColorChanged", parameters[i].Name ?? "parameters[i]", paramType, "Color (value) and/or ColorPicker, ColorPickerButton");
                return (null, null);
            }
        }
        
        var callback = DelegateFactory.CreateSignalCallback<Color, Control>(method, sourceType, colorIdx, sourceIdx);
        if (callback is not null) return (callback, sourceType);

        LiturgyReport.AddMethodError(methodName, "OnColorChanged", "Failed to create callback.");
        return (null, null);

    }
    
    private void SubscribeColorPicker(ColorPicker colorPicker, string path)
    {
        if (!_subscribedColorPickers.Add(colorPicker)) return;
        
        var hasColorChanged = _colorChangedGlobalCallbacks.Count > 0 || _colorChangedTargetedCallbacks.ContainsKey(path);
        
        if (hasColorChanged)
        {
            Listen(colorPicker, ColorPicker.SignalName.ColorChanged, Callable.From((Color color) => OnColorPickerColorChanged(color, colorPicker, path)));
        }
    }
    
    private void SubscribeColorPickerButton(ColorPickerButton colorPickerButton, string path)
    {
        if (!_subscribedColorPickerButtons.Add(colorPickerButton)) return;
        
        var hasColorChanged = _colorChangedGlobalCallbacks.Count > 0 || _colorChangedTargetedCallbacks.ContainsKey(path);
        
        if (hasColorChanged)
        {
            Listen(colorPickerButton, ColorPickerButton.SignalName.ColorChanged, Callable.From((Color color) => OnColorPickerButtonColorChanged(color, colorPickerButton, path)));
        }
    }
    
    private void OnColorPickerColorChanged(Color color, ColorPicker source, string path)
    {
        if (_colorChangedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(color, source);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _colorChangedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
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
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(color, source);
            }
        }
        
        foreach (var (callback, excludes, sourceType) in _colorChangedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            if (sourceType is not null && !sourceType.IsInstanceOfType(source)) continue;
            callback(color, source);
        }
    }
}
