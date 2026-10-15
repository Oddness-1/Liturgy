using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    // Animation finished callbacks
    private readonly List<(Action<StringName, AnimationPlayer> Callback, HashSet<string>? Excludes)> _animationFinishedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<StringName, AnimationPlayer> Callback, HashSet<string>? Excludes)>> _animationFinishedTargetedCallbacks = new();
    
    // Animation started callbacks
    private readonly List<(Action<StringName, AnimationPlayer> Callback, HashSet<string>? Excludes)> _animationStartedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<StringName, AnimationPlayer> Callback, HashSet<string>? Excludes)>> _animationStartedTargetedCallbacks = new();
    
    // Track subscribed animation players
    private readonly HashSet<AnimationPlayer> _subscribedAnimationPlayers = [];

    private void SetupAnimationCallbacks()
    {
        SetupAnimationFinishedCallbacks();
        SetupAnimationStartedCallbacks();
        
        var hasCallbacks = _animationFinishedGlobalCallbacks.Count > 0 || _animationFinishedTargetedCallbacks.Count > 0 ||
                           _animationStartedGlobalCallbacks.Count > 0 || _animationStartedTargetedCallbacks.Count > 0;
        
        if (!hasCallbacks) return;
        
        SubscribeToExistingNodesForAnimation(_root);
        GetTree().NodeAdded += OnNodeAddedForAnimation;
        GetTree().NodeRemoved += OnNodeRemovedForAnimation;
    }

    private void SetupAnimationFinishedCallbacks()
    {
        var methods = ScanForMethods<OnAnimationFinished>();
        
        foreach (var (method, attr) in methods.OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, _) = CreateAnimationCallback(method, "OnAnimationFinished");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnAnimationFinished", methodName, attr.Target, attr.Exclude, attr.Priority, "AnimationPlayer");
            
            if (attr.Target is null)
            {
                _animationFinishedGlobalCallbacks.Add((callback, excludeSet));
            }
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_animationFinishedTargetedCallbacks.ContainsKey(normalized))
                        _animationFinishedTargetedCallbacks[normalized] = [];
                    _animationFinishedTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
    }

    private void SetupAnimationStartedCallbacks()
    {
        var methods = ScanForMethods<OnAnimationStarted>();
        
        foreach (var (method, attr) in methods.OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var (callback, _) = CreateAnimationCallback(method, "OnAnimationStarted");
            if (callback is null) continue;
            
            var excludeSet = ParseExcludeSet(attr.Exclude);
            LiturgyReport.AddControlHook("OnAnimationStarted", methodName, attr.Target, attr.Exclude, attr.Priority, "AnimationPlayer");
            
            if (attr.Target is null)
            {
                _animationStartedGlobalCallbacks.Add((callback, excludeSet));
            }
            else
            {
                foreach (var t in attr.Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizePath(t);
                    if (!_animationStartedTargetedCallbacks.ContainsKey(normalized))
                        _animationStartedTargetedCallbacks[normalized] = [];
                    _animationStartedTargetedCallbacks[normalized].Add((callback, excludeSet));
                }
            }
        }
    }

    private static (Action<StringName, AnimationPlayer>? Callback, bool HasAnimName) CreateAnimationCallback(MethodInfo method, string hookName)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        if (parameters.Length == 0)
        {
            var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
            return ((_, _) => del(), false);
        }
        
        if (parameters.Length > 2)
        {
            LiturgyReport.AddMethodError(methodName, hookName, "Must have at most two parameters.");
            return (null, false);
        }
        
        int? animNameIdx = null;
        int? sourceIdx = null;
        
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            
            if (paramType == typeof(StringName) || paramType == typeof(string))
            {
                animNameIdx = i;
            }
            else if (typeof(AnimationPlayer).IsAssignableFrom(paramType))
            {
                sourceIdx = i;
            }
            else
            {
                LiturgyReport.AddInvalidParameterError(methodName, hookName, parameters[i].Name ?? $"arg{i}", paramType,
                    "StringName/string (animation name) and/or AnimationPlayer");
                return (null, false);
            }
        }
        
        // Create appropriate delegate based on parameter configuration
        if (parameters.Length == 1)
        {
            if (animNameIdx is not null)
            {
                if (parameters[0].ParameterType == typeof(string))
                {
                    var del = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), method);
                    return ((animName, _) => del(animName), true);
                }
                else
                {
                    var del = (Action<StringName>)Delegate.CreateDelegate(typeof(Action<StringName>), method);
                    return ((animName, _) => del(animName), true);
                }
            }
            else
            {
                var del = (Action<AnimationPlayer>)Delegate.CreateDelegate(typeof(Action<AnimationPlayer>), method);
                return ((_, player) => del(player), false);
            }
        }
        
        // Two parameters
        if (animNameIdx == 0)
        {
            // (StringName, AnimationPlayer) or (string, AnimationPlayer)
            if (parameters[0].ParameterType == typeof(string))
            {
                var del = (Action<string, AnimationPlayer>)Delegate.CreateDelegate(typeof(Action<string, AnimationPlayer>), method);
                return ((animName, player) => del(animName, player), true);
            }
            else
            {
                var del = (Action<StringName, AnimationPlayer>)Delegate.CreateDelegate(typeof(Action<StringName, AnimationPlayer>), method);
                return (del, true);
            }
        }
        else
        {
            // (AnimationPlayer, StringName) or (AnimationPlayer, string)
            if (parameters[1].ParameterType == typeof(string))
            {
                var del = (Action<AnimationPlayer, string>)Delegate.CreateDelegate(typeof(Action<AnimationPlayer, string>), method);
                return ((animName, player) => del(player, animName), true);
            }
            else
            {
                var del = (Action<AnimationPlayer, StringName>)Delegate.CreateDelegate(typeof(Action<AnimationPlayer, StringName>), method);
                return ((animName, player) => del(player, animName), true);
            }
        }
    }

    private void SubscribeToExistingNodesForAnimation(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is AnimationPlayer player)
                SubscribeAnimationPlayer(player);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    private void OnNodeAddedForAnimation(Node node)
    {
        if (IsInternalNode(node)) return;
        if (node is AnimationPlayer player)
            SubscribeAnimationPlayer(player);
    }

    private void OnNodeRemovedForAnimation(Node node)
    {
        if (node is AnimationPlayer player)
            _subscribedAnimationPlayers.Remove(player);
    }

    private void SubscribeAnimationPlayer(AnimationPlayer player)
    {
        if (_subscribedAnimationPlayers.Contains(player)) return;
        if (IsInternalNode(player)) return;
        
        var path = player.GetPath().ToString();
        var hasTargeted = _animationFinishedTargetedCallbacks.ContainsKey(path) ||
                          _animationStartedTargetedCallbacks.ContainsKey(path);
        var hasGlobal = _animationFinishedGlobalCallbacks.Count > 0 ||
                        _animationStartedGlobalCallbacks.Count > 0;
        
        if (!hasTargeted && !hasGlobal) return;
        
        _subscribedAnimationPlayers.Add(player);
        
        player.AnimationFinished += animName => OnAnimationFinished(player, path, animName);
        player.AnimationStarted += animName => OnAnimationStarted(player, path, animName);
    }

    private void OnAnimationFinished(AnimationPlayer player, string path, StringName animName)
    {
        if (_animationFinishedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(animName, player);
            }
        }
        
        foreach (var (callback, excludes) in _animationFinishedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            callback(animName, player);
        }
    }

    private void OnAnimationStarted(AnimationPlayer player, string path, StringName animName)
    {
        if (_animationStartedTargetedCallbacks.TryGetValue(path, out var targeted))
        {
            foreach (var (callback, excludes) in targeted)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(animName, player);
            }
        }
        
        foreach (var (callback, excludes) in _animationStartedGlobalCallbacks)
        {
            if (PathMatchesAnyExclude(path, excludes)) continue;
            callback(animName, player);
        }
    }
}

