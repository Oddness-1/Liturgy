using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    // Popup hide callbacks
    private readonly List<(Action<Popup> Callback, HashSet<string>? Excludes, Type? SourceType)> _popupHideGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Popup> Callback, HashSet<string>? Excludes)>> _popupHideTargetedCallbacks = new();
    
    // Close requested callbacks
    private readonly List<(Action<Window> Callback, HashSet<string>? Excludes, Type? SourceType)> _closeRequestedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Window> Callback, HashSet<string>? Excludes)>> _closeRequestedTargetedCallbacks = new();
    
    // Dialog confirmed/canceled callbacks
    private readonly List<(Action<AcceptDialog> Callback, HashSet<string>? Excludes, Type? SourceType)> _confirmedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<AcceptDialog> Callback, HashSet<string>? Excludes)>> _confirmedTargetedCallbacks = new();
    
    private readonly List<(Action<AcceptDialog> Callback, HashSet<string>? Excludes, Type? SourceType)> _canceledGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<AcceptDialog> Callback, HashSet<string>? Excludes)>> _canceledTargetedCallbacks = new();
    
    private readonly HashSet<Popup> _subscribedPopups = [];
    private readonly HashSet<Window> _subscribedWindowsForClose = [];
    private readonly HashSet<AcceptDialog> _subscribedDialogs = [];

    private void SetupPopupCallbacks()
    {
        SetupPopupHideCallbacks();
        SetupCloseRequestedCallbacks();
        SetupConfirmedCallbacks();
        SetupCanceledCallbacks();
        
        var hasCallbacks = _popupHideGlobalCallbacks.Count > 0 || _popupHideTargetedCallbacks.Count > 0 ||
                           _closeRequestedGlobalCallbacks.Count > 0 || _closeRequestedTargetedCallbacks.Count > 0 ||
                           _confirmedGlobalCallbacks.Count > 0 || _confirmedTargetedCallbacks.Count > 0 ||
                           _canceledGlobalCallbacks.Count > 0 || _canceledTargetedCallbacks.Count > 0;
        
        if (!hasCallbacks) return;
        
        SubscribeToExistingPopups(_root);
        GetTree().NodeAdded += OnNodeAddedForPopup;
        GetTree().NodeRemoved += OnNodeRemovedForPopup;
    }

    private void SetupPopupHideCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnPopupHide>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreatePopupCallback(method, "OnPopupHide");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnPopupHide", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name ?? "Popup");
            
            if (attr.Target is null)
                _popupHideGlobalCallbacks.Add((callback, excludeSet, sourceType));
            else
                AddTargetedPopupCallbacks(_popupHideTargetedCallbacks, attr.Target, callback, excludeSet);
        }
    }

    private void SetupCloseRequestedCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnCloseRequested>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateWindowCallback(method, "OnCloseRequested");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnCloseRequested", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name ?? "Window");
            
            if (attr.Target is null)
                _closeRequestedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            else
                AddTargetedWindowCallbacks(_closeRequestedTargetedCallbacks, attr.Target, callback, excludeSet);
        }
    }

    private void SetupConfirmedCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnConfirmed>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateDialogCallback(method, "OnConfirmed");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnConfirmed", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name ?? "AcceptDialog");
            
            if (attr.Target is null)
                _confirmedGlobalCallbacks.Add((callback, excludeSet, sourceType));
            else
                AddTargetedDialogCallbacks(_confirmedTargetedCallbacks, attr.Target, callback, excludeSet);
        }
    }

    private void SetupCanceledCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnCanceled>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, sourceType) = CreateDialogCallback(method, "OnCanceled");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnCanceled", methodName, attr.Target, attr.Exclude, attr.Priority, sourceType?.Name ?? "AcceptDialog");
            
            if (attr.Target is null)
                _canceledGlobalCallbacks.Add((callback, excludeSet, sourceType));
            else
                AddTargetedDialogCallbacks(_canceledTargetedCallbacks, attr.Target, callback, excludeSet);
        }
    }

    private static (Action<Popup>? Callback, Type? SourceType) CreatePopupCallback(System.Reflection.MethodInfo method, string hookName)
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
        if (!typeof(Popup).IsAssignableFrom(paramType))
        {
            LiturgyReport.AddInvalidParameterError(methodName, hookName, parameters[0].Name ?? "arg0", paramType, "Popup subtypes");
            return (null, null);
        }
        
        var callback = DelegateFactory.CreateCallback<Popup>(method, paramType);
        return (callback, paramType);
    }

    private static (Action<Window>? Callback, Type? SourceType) CreateWindowCallback(System.Reflection.MethodInfo method, string hookName)
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
        if (!typeof(Window).IsAssignableFrom(paramType))
        {
            LiturgyReport.AddInvalidParameterError(methodName, hookName, parameters[0].Name ?? "arg0", paramType, "Window subtypes");
            return (null, null);
        }
        
        var callback = DelegateFactory.CreateCallback<Window>(method, paramType);
        return (callback, paramType);
    }

    private static (Action<AcceptDialog>? Callback, Type? SourceType) CreateDialogCallback(System.Reflection.MethodInfo method, string hookName)
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
        if (!typeof(AcceptDialog).IsAssignableFrom(paramType))
        {
            LiturgyReport.AddInvalidParameterError(methodName, hookName, parameters[0].Name ?? "arg0", paramType, "AcceptDialog subtypes");
            return (null, null);
        }
        
        var callback = DelegateFactory.CreateCallback<AcceptDialog>(method, paramType);
        return (callback, paramType);
    }

    private static void AddTargetedPopupCallbacks(Dictionary<string, List<(Action<Popup>, HashSet<string>?)>> dict, string target, Action<Popup> callback, HashSet<string>? excludeSet)
    {
        foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var n = NormalizePath(t);
            if (!dict.ContainsKey(n)) dict[n] = [];
            dict[n].Add((callback, excludeSet));
        }
    }

    private static void AddTargetedWindowCallbacks(Dictionary<string, List<(Action<Window>, HashSet<string>?)>> dict, string target, Action<Window> callback, HashSet<string>? excludeSet)
    {
        foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var n = NormalizePath(t);
            if (!dict.ContainsKey(n)) dict[n] = [];
            dict[n].Add((callback, excludeSet));
        }
    }

    private static void AddTargetedDialogCallbacks(Dictionary<string, List<(Action<AcceptDialog>, HashSet<string>?)>> dict, string target, Action<AcceptDialog> callback, HashSet<string>? excludeSet)
    {
        foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var n = NormalizePath(t);
            if (!dict.ContainsKey(n)) dict[n] = [];
            dict[n].Add((callback, excludeSet));
        }
    }

    private void SubscribeToExistingPopups(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (IsShielded(node)) continue;
            SubscribePopupNode(node);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    private void OnNodeAddedForPopup(Node node)
    {
        if (IsHiddenFromRites(node)) return;
        SubscribePopupNode(node);
    }

    private void OnNodeRemovedForPopup(Node node)
    {
        if (node is Popup p) _subscribedPopups.Remove(p);
        if (node is Window w) _subscribedWindowsForClose.Remove(w);
        if (node is AcceptDialog d) _subscribedDialogs.Remove(d);
    }

    private void SubscribePopupNode(Node node)
    {
        if (node is AcceptDialog dialog) SubscribeDialog(dialog);
        else if (node is Popup popup) SubscribePopup(popup);
        if (node is Window window) SubscribeWindowForClose(window);
    }

    private void SubscribePopup(Popup popup)
    {
        if (_subscribedPopups.Contains(popup) || IsHiddenFromRites(popup)) return;
        
        var path = popup.GetPath().ToString();
        var hasAny = _popupHideTargetedCallbacks.ContainsKey(path) || _popupHideGlobalCallbacks.Count > 0;
        if (!hasAny) return;
        
        _subscribedPopups.Add(popup);
        Listen(popup, Popup.SignalName.PopupHide, Callable.From(() =>
        {
            if (_popupHideTargetedCallbacks.TryGetValue(path, out var targeted))
            {
                foreach (var (callback, excludes) in targeted)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    RiteCall.Run(callback, popup);
                }
            }
            
            foreach (var (callback, excludes, sourceType) in _popupHideGlobalCallbacks)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                if (sourceType is not null && !sourceType.IsInstanceOfType(popup)) continue;
                RiteCall.Run(callback, popup);
            }
        }));
    }

    private void SubscribeWindowForClose(Window window)
    {
        if (_subscribedWindowsForClose.Contains(window) || IsHiddenFromRites(window)) return;
        
        var path = window.GetPath().ToString();
        var hasAny = _closeRequestedTargetedCallbacks.ContainsKey(path) || _closeRequestedGlobalCallbacks.Count > 0;
        if (!hasAny) return;
        
        _subscribedWindowsForClose.Add(window);
        Listen(window, Window.SignalName.CloseRequested, Callable.From(() =>
        {
            if (_closeRequestedTargetedCallbacks.TryGetValue(path, out var targeted))
            {
                foreach (var (callback, excludes) in targeted)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    RiteCall.Run(callback, window);
                }
            }
            
            foreach (var (callback, excludes, sourceType) in _closeRequestedGlobalCallbacks)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                if (sourceType is not null && !sourceType.IsInstanceOfType(window)) continue;
                RiteCall.Run(callback, window);
            }
        }));
    }

    private void SubscribeDialog(AcceptDialog dialog)
    {
        if (_subscribedDialogs.Contains(dialog) || IsHiddenFromRites(dialog)) return;
        
        var path = dialog.GetPath().ToString();
        var hasConfirmed = _confirmedTargetedCallbacks.ContainsKey(path) || _confirmedGlobalCallbacks.Count > 0;
        var hasCanceled = _canceledTargetedCallbacks.ContainsKey(path) || _canceledGlobalCallbacks.Count > 0;
        if (!hasConfirmed && !hasCanceled) return;
        
        _subscribedDialogs.Add(dialog);
        
        if (hasConfirmed)
        {
            Listen(dialog, AcceptDialog.SignalName.Confirmed, Callable.From(() =>
            {
                if (_confirmedTargetedCallbacks.TryGetValue(path, out var targeted))
                {
                    foreach (var (callback, excludes) in targeted)
                    {
                        if (PathMatchesAnyExclude(path, excludes)) continue;
                        RiteCall.Run(callback, dialog);
                    }
                }
                
                foreach (var (callback, excludes, sourceType) in _confirmedGlobalCallbacks)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    if (sourceType is not null && !sourceType.IsInstanceOfType(dialog)) continue;
                    RiteCall.Run(callback, dialog);
                }
            }));
        }
        
        if (hasCanceled)
        {
            Listen(dialog, AcceptDialog.SignalName.Canceled, Callable.From(() =>
            {
                if (_canceledTargetedCallbacks.TryGetValue(path, out var targeted))
                {
                    foreach (var (callback, excludes) in targeted)
                    {
                        if (PathMatchesAnyExclude(path, excludes)) continue;
                        RiteCall.Run(callback, dialog);
                    }
                }
                
                foreach (var (callback, excludes, sourceType) in _canceledGlobalCallbacks)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    if (sourceType is not null && !sourceType.IsInstanceOfType(dialog)) continue;
                    RiteCall.Run(callback, dialog);
                }
            }));
        }
    }
}

