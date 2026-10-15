using System;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    private void SetupFileDialogCallbacks()
    {
        // FileSelected
        foreach (var (method, attr) in ScanForMethods<OnFileSelected>())
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var callback = CreateFileDialogCallback(method);
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            LiturgyReport.AddControlHook("OnFileSelected", methodName, attr.Target, attr.Exclude, attr.Priority, "FileDialog");
            
            if (attr.Target is null)
            {
                _fileSelectedGlobalCallbacks.Add((callback, excludeSet));
            }
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_fileSelectedTargetedCallbacks.ContainsKey(normalized))
                        _fileSelectedTargetedCallbacks[normalized] = [];
                    _fileSelectedTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
        
        // DirSelected
        foreach (var (method, attr) in ScanForMethods<OnDirSelected>())
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var callback = CreateFileDialogCallback(method);
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            LiturgyReport.AddControlHook("OnDirSelected", methodName, attr.Target, attr.Exclude, attr.Priority, "FileDialog");
            
            if (attr.Target is null)
            {
                _dirSelectedGlobalCallbacks.Add((callback, excludeSet));
            }
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_dirSelectedTargetedCallbacks.ContainsKey(normalized))
                        _dirSelectedTargetedCallbacks[normalized] = [];
                    _dirSelectedTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
    }
    
    private static Action<string, FileDialog>? CreateFileDialogCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        int? stringIdx = null;
        int? sourceIdx = null;
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            
            if (paramType == typeof(string))
            {
                stringIdx = i;
            }
            else if (typeof(FileDialog).IsAssignableFrom(paramType))
            {
                sourceIdx = i;
            }
            else
            {
                LiturgyReport.AddInvalidParameterError(methodName, "OnFileSelected/OnDirSelected", parameters[i].Name ?? "parameters[i]", paramType, "string (path) and/or FileDialog");
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
            return (path, _) => del(path);
        }
        
        if (stringIdx is null && sourceIdx is not null)
        {
            var del = (Action<FileDialog>)Delegate.CreateDelegate(typeof(Action<FileDialog>), method);
            return (_, source) => del(source);
        }
        
        if (stringIdx is not null && sourceIdx is not null)
        {
            if (stringIdx < sourceIdx)
            {
                var del = (Action<string, FileDialog>)Delegate.CreateDelegate(typeof(Action<string, FileDialog>), method);
                return del;
            }
            else
            {
                var del = (Action<FileDialog, string>)Delegate.CreateDelegate(typeof(Action<FileDialog, string>), method);
                return (path, source) => del(source, path);
            }
        }
        
        LiturgyReport.AddMethodError(methodName, "OnFileSelected/OnDirSelected", "Failed to create callback.");
        return null;
    }
    
    private void SubscribeFileDialog(FileDialog fileDialog, string path)
    {
        if (!_subscribedFileDialogs.Add(fileDialog)) return;
        
        var hasFileSelected = _fileSelectedGlobalCallbacks.Count > 0 || _fileSelectedTargetedCallbacks.ContainsKey(path);
        var hasDirSelected = _dirSelectedGlobalCallbacks.Count > 0 || _dirSelectedTargetedCallbacks.ContainsKey(path);
        
        if (hasFileSelected) fileDialog.FileSelected += filePath => OnFileDialogFileSelected(filePath, fileDialog, path);
        if (hasDirSelected) fileDialog.DirSelected += dirPath => OnFileDialogDirSelected(dirPath, fileDialog, path);
    }
    
    private void OnFileDialogFileSelected(string filePath, FileDialog source, string path)
    {
        if (_fileSelectedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(filePath, source);
            }
        }
        
        foreach (var (callback, excludes) in _fileSelectedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            callback(filePath, source);
        }
    }
    
    private void OnFileDialogDirSelected(string dirPath, FileDialog source, string path)
    {
        if (_dirSelectedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(dirPath, source);
            }
        }
        
        foreach (var (callback, excludes) in _dirSelectedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            callback(dirPath, source);
        }
    }
}
