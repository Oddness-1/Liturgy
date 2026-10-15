using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Bootstrap.Internal;

public partial class Autoload
{
    // Timer callbacks
    private readonly List<(Action<Timer> Callback, HashSet<string>? Excludes)> _timeoutGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<Timer> Callback, HashSet<string>? Excludes)>> _timeoutTargetedCallbacks = new();
    
    private readonly HashSet<Timer> _subscribedTimers = [];

    private void SetupTimerCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnTimeout>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var parameters = method.GetParameters();
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            Action<Timer>? callback = null;
            
            if (parameters.Length == 0)
            {
                var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
                callback = _ => del();
            }
            else if (parameters.Length == 1 && typeof(Timer).IsAssignableFrom(parameters[0].ParameterType))
            {
                callback = DelegateFactory.CreateCallback<Timer>(method, parameters[0].ParameterType);
            }
            else
            {
                BootstrapReport.AddMethodError(methodName, "OnTimeout", "Must have zero parameters or one Timer parameter.");
                continue;
            }
            
            if (callback is null) continue;
            
            BootstrapReport.AddControlHook("OnTimeout", methodName, attr.Target, attr.Exclude, attr.Priority, "Timer");
            
            if (attr.Target is null)
            {
                _timeoutGlobalCallbacks.Add((callback, excludeSet));
            }
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_timeoutTargetedCallbacks.ContainsKey(normalized))
                        _timeoutTargetedCallbacks[normalized] = [];
                    _timeoutTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
        
        if (_timeoutGlobalCallbacks.Count == 0 && _timeoutTargetedCallbacks.Count == 0) return;
        
        SubscribeToExistingTimers(_root);
        GetTree().NodeAdded += OnNodeAddedForTimer;
        GetTree().NodeRemoved += n => { if (n is Timer t) _subscribedTimers.Remove(t); };
    }

    private void SubscribeToExistingTimers(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Timer timer) SubscribeTimer(timer);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    private void OnNodeAddedForTimer(Node node)
    {
        if (IsInternalNode(node)) return;
        if (node is Timer timer) SubscribeTimer(timer);
    }

    private void SubscribeTimer(Timer timer)
    {
        if (_subscribedTimers.Contains(timer) || IsInternalNode(timer)) return;
        
        var path = timer.GetPath().ToString();
        var hasAny = _timeoutTargetedCallbacks.ContainsKey(path) || _timeoutGlobalCallbacks.Count > 0;
        if (!hasAny) return;
        
        _subscribedTimers.Add(timer);
        timer.Timeout += () =>
        {
            if (_timeoutTargetedCallbacks.TryGetValue(path, out var targeted))
                foreach (var (cb, ex) in targeted) { if (ex?.Contains(path) != true) cb(timer); }
            foreach (var (cb, ex) in _timeoutGlobalCallbacks)
                if (ex?.Contains(path) != true) cb(timer);
        };
    }
}
