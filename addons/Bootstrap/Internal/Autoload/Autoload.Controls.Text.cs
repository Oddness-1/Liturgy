using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace Bootstrap.Internal;

public partial class Autoload
{
    private void SetupTextChangedCallbacks()
    {
        var methods = ScanForMethods<OnTextChanged>();
        
        foreach (var (method, attr) in methods)
        {
            var parameters = method.GetParameters();
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            
            // Determine if this is for LineEdit or TextEdit based on parameters
            var hasLineEdit = parameters.Any(p => typeof(LineEdit).IsAssignableFrom(p.ParameterType));
            var hasTextEdit = parameters.Any(p => typeof(TextEdit).IsAssignableFrom(p.ParameterType));
            
            HashSet<string>? excludeSet = null;
            if (!string.IsNullOrEmpty(attr.Exclude))
            {
                excludeSet = attr.Exclude
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(NormalizePath)
                    .ToHashSet();
            }
            
            // TextEdit's TextChanged signal has no parameters, LineEdit's has the new text
            if (hasTextEdit && !hasLineEdit)
            {
                // TextEdit callback
                var callback = CreateTextEditChangedCallback(method);
                if (callback is null) continue;
                
                BootstrapReport.AddControlHook("OnTextChanged", methodName, attr.Target, attr.Exclude, attr.Priority, "TextEdit");
                
                if (attr.Target is null)
                {
                    _textChangedTextEditGlobalCallbacks.Add((callback, excludeSet));
                }
                else
                {
                    foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var normalized = NormalizePath(t);
                        if (!_textChangedTextEditTargetedCallbacks.ContainsKey(normalized))
                            _textChangedTextEditTargetedCallbacks[normalized] = [];
                        _textChangedTextEditTargetedCallbacks[normalized].Add((callback, excludeSet));
                    }
                }
            }
            else
            {
                // LineEdit callback (default)
                var callback = CreateLineEditTextChangedCallback(method);
                if (callback is null) continue;
                
                BootstrapReport.AddControlHook("OnTextChanged", methodName, attr.Target, attr.Exclude, attr.Priority, "LineEdit");
                
                if (attr.Target is null)
                {
                    _textChangedLineEditGlobalCallbacks.Add((callback, excludeSet));
                }
                else
                {
                    foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var normalized = NormalizePath(t);
                        if (!_textChangedLineEditTargetedCallbacks.ContainsKey(normalized))
                            _textChangedLineEditTargetedCallbacks[normalized] = [];
                        _textChangedLineEditTargetedCallbacks[normalized].Add((callback, excludeSet));
                    }
                }
            }
        }
    }
    
    private static Action<string, LineEdit>? CreateLineEditTextChangedCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        // Find parameter positions
        int? stringIdx = null;
        int? sourceIdx = null;
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;

            if (paramType == typeof(string))
                stringIdx = i;
            
            else if (typeof(LineEdit).IsAssignableFrom(paramType)) 
                sourceIdx = i;
            
            else
            {
                BootstrapReport.AddInvalidParameterError(methodName, "OnTextChanged/OnTextSubmitted", parameters[i].Name ?? "parameters[i]", paramType, "string (text) and/or LineEdit");
                return null;
            }
        }
        
        if (parameters.Length == 0)
        {
            var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
            return (_, _) => del();
        }
        
        if (stringIdx is not null && sourceIdx is null)
        {
            var del = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), method);
            return (text, _) => del(text);
        }
        
        if (stringIdx is null && sourceIdx is not null)
        {
            var del = (Action<LineEdit>)Delegate.CreateDelegate(typeof(Action<LineEdit>), method);
            return (_, source) => del(source);
        }
        
        if (stringIdx is not null && sourceIdx is not null)
        {
            if (stringIdx < sourceIdx)
            {
                var del = (Action<string, LineEdit>)Delegate.CreateDelegate(typeof(Action<string, LineEdit>), method);
                return del;
            }
            else
            {
                var del = (Action<LineEdit, string>)Delegate.CreateDelegate(typeof(Action<LineEdit, string>), method);
                return (text, source) => del(source, text);
            }
        }
        
        BootstrapReport.AddMethodError(methodName, "OnTextChanged/OnTextSubmitted", "Failed to create callback.");
        return null;
    }
    
    private static Action<TextEdit>? CreateTextEditChangedCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        if (parameters.Length == 0)
        {
            var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
            return _ => del();
        }
        
        if (parameters.Length == 1)
        {
            var paramType = parameters[0].ParameterType;
            if (typeof(TextEdit).IsAssignableFrom(paramType))
            {
                var del = (Action<TextEdit>)Delegate.CreateDelegate(typeof(Action<TextEdit>), method);
                return del;
            }
            
            BootstrapReport.AddInvalidParameterError(methodName, "OnTextChanged (TextEdit)", parameters[0].Name ?? "parameters[i]", paramType, "TextEdit only (TextEdit.TextChanged has no signal parameters)");
            return null;
        }
        
        BootstrapReport.AddMethodError(methodName, "OnTextChanged (TextEdit)", "TextEdit.TextChanged has no signal parameters. Method should have zero parameters or just TextEdit.");
        return null;
    }
    
    private void SetupTextSubmittedCallbacks()
    {
        var methods = ScanForMethods<OnTextSubmitted>();
        
        foreach (var (method, attr) in methods)
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var callback = CreateLineEditTextChangedCallback(method); // Same signature as TextChanged
            if (callback is null) continue;
            
            HashSet<string>? excludeSet = null;
            if (!string.IsNullOrEmpty(attr.Exclude))
            {
                excludeSet = attr.Exclude
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(NormalizePath)
                    .ToHashSet();
            }
            
            BootstrapReport.AddControlHook("OnTextSubmitted", methodName, attr.Target, attr.Exclude, attr.Priority, "LineEdit");

            if (attr.Target is null) _textSubmittedGlobalCallbacks.Add((callback, excludeSet));
            
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_textSubmittedTargetedCallbacks.ContainsKey(normalized))
                        _textSubmittedTargetedCallbacks[normalized] = [];
                    _textSubmittedTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
    }
    
    private void SubscribeLineEdit(LineEdit lineEdit, string path)
    {
        if (!_subscribedLineEdits.Add(lineEdit)) return;
        
        var hasTextChanged = _textChangedLineEditGlobalCallbacks.Count > 0 || _textChangedLineEditTargetedCallbacks.ContainsKey(path);
        var hasTextSubmitted = _textSubmittedGlobalCallbacks.Count > 0 || _textSubmittedTargetedCallbacks.ContainsKey(path);
        
        if (hasTextChanged) lineEdit.TextChanged += text => OnLineEditTextChanged(text, lineEdit, path);
        if (hasTextSubmitted) lineEdit.TextSubmitted += text => OnLineEditTextSubmitted(text, lineEdit, path);
    }
    
    private void SubscribeTextEdit(TextEdit textEdit, string path)
    {
        if (!_subscribedTextEdits.Add(textEdit)) return;
        
        var hasTextChanged = _textChangedTextEditGlobalCallbacks.Count > 0 || _textChangedTextEditTargetedCallbacks.ContainsKey(path);
        
        if (hasTextChanged) textEdit.TextChanged += () => OnTextEditTextChanged(textEdit, path);
    }
    
    private void OnLineEditTextChanged(string text, LineEdit source, string path)
    {
        if (_textChangedLineEditTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (excludes?.Contains(path) == true) continue;
                callback(text, source);
            }
        }
        
        foreach (var (callback, excludes) in _textChangedLineEditGlobalCallbacks)
        {
            if (excludes?.Contains(path) == true) continue;
            callback(text, source);
        }
    }
    
    private void OnLineEditTextSubmitted(string text, LineEdit source, string path)
    {
        if (_textSubmittedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (excludes?.Contains(path) == true) continue;
                callback(text, source);
            }
        }
        
        foreach (var (callback, excludes) in _textSubmittedGlobalCallbacks)
        {
            if (excludes?.Contains(path) == true) continue;
            callback(text, source);
        }
    }
    
    private void OnTextEditTextChanged(TextEdit source, string path)
    {
        if (_textChangedTextEditTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (excludes?.Contains(path) == true) continue;
                callback(source);
            }
        }
        
        foreach (var (callback, excludes) in _textChangedTextEditGlobalCallbacks)
        {
            if (excludes?.Contains(path) == true) continue;
            callback(source);
        }
    }
}