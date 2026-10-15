using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    // Audio finished callbacks
    private readonly List<(Action<AudioStreamPlayer> Callback, HashSet<string>? Excludes)> _audioFinishedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<AudioStreamPlayer> Callback, HashSet<string>? Excludes)>> _audioFinishedTargetedCallbacks = new();
    
    private readonly List<(Action<AudioStreamPlayer2D> Callback, HashSet<string>? Excludes)> _audio2DFinishedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<AudioStreamPlayer2D> Callback, HashSet<string>? Excludes)>> _audio2DFinishedTargetedCallbacks = new();
    
    private readonly List<(Action<AudioStreamPlayer3D> Callback, HashSet<string>? Excludes)> _audio3DFinishedGlobalCallbacks = [];
    private readonly Dictionary<string, List<(Action<AudioStreamPlayer3D> Callback, HashSet<string>? Excludes)>> _audio3DFinishedTargetedCallbacks = new();
    
    private readonly HashSet<AudioStreamPlayer> _subscribedAudioPlayers = [];
    private readonly HashSet<AudioStreamPlayer2D> _subscribedAudioPlayers2D = [];
    private readonly HashSet<AudioStreamPlayer3D> _subscribedAudioPlayers3D = [];

    private void SetupAudioCallbacks()
    {
        foreach (var (method, attr) in ScanForMethods<OnAudioFinished>().OrderBy(m => m.Attribute.Priority))
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var parameters = method.GetParameters();
            var excludeSet = ParseExcludeSet(attr.Exclude);
            
            if (parameters.Length == 0)
            {
                // No parameters - register for all three types
                var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
                LiturgyReport.AddControlHook("OnAudioFinished", methodName, attr.Target, attr.Exclude, attr.Priority, null);
                
                AddAudioCallback(_audioFinishedGlobalCallbacks, _audioFinishedTargetedCallbacks, (Action<AudioStreamPlayer>)(_ => del()), excludeSet, attr.Target);
                AddAudioCallback(_audio2DFinishedGlobalCallbacks, _audio2DFinishedTargetedCallbacks, (Action<AudioStreamPlayer2D>)(_ => del()), excludeSet, attr.Target);
                AddAudioCallback(_audio3DFinishedGlobalCallbacks, _audio3DFinishedTargetedCallbacks, (Action<AudioStreamPlayer3D>)(_ => del()), excludeSet, attr.Target);
                continue;
            }
            
            if (parameters.Length > 1)
            {
                LiturgyReport.AddMethodError(methodName, "OnAudioFinished", "Must have zero or one parameter.");
                continue;
            }
            
            var paramType = parameters[0].ParameterType;
            
            if (typeof(AudioStreamPlayer).IsAssignableFrom(paramType) && paramType != typeof(AudioStreamPlayer2D) && paramType != typeof(AudioStreamPlayer3D))
            {
                var callback = DelegateFactory.CreateCallback<AudioStreamPlayer>(method, paramType);
                if (callback is not null)
                {
                    LiturgyReport.AddControlHook("OnAudioFinished", methodName, attr.Target, attr.Exclude, attr.Priority, "AudioStreamPlayer");
                    AddAudioCallback(_audioFinishedGlobalCallbacks, _audioFinishedTargetedCallbacks, callback, excludeSet, attr.Target);
                }
            }
            else if (typeof(AudioStreamPlayer2D).IsAssignableFrom(paramType))
            {
                var callback = DelegateFactory.CreateCallback<AudioStreamPlayer2D>(method, paramType);
                if (callback is not null)
                {
                    LiturgyReport.AddControlHook("OnAudioFinished", methodName, attr.Target, attr.Exclude, attr.Priority, "AudioStreamPlayer2D");
                    AddAudioCallback(_audio2DFinishedGlobalCallbacks, _audio2DFinishedTargetedCallbacks, callback, excludeSet, attr.Target);
                }
            }
            else if (typeof(AudioStreamPlayer3D).IsAssignableFrom(paramType))
            {
                var callback = DelegateFactory.CreateCallback<AudioStreamPlayer3D>(method, paramType);
                if (callback is not null)
                {
                    LiturgyReport.AddControlHook("OnAudioFinished", methodName, attr.Target, attr.Exclude, attr.Priority, "AudioStreamPlayer3D");
                    AddAudioCallback(_audio3DFinishedGlobalCallbacks, _audio3DFinishedTargetedCallbacks, callback, excludeSet, attr.Target);
                }
            }
            else
            {
                LiturgyReport.AddInvalidParameterError(methodName, "OnAudioFinished", parameters[0].Name ?? "arg0", paramType, 
                    "AudioStreamPlayer, AudioStreamPlayer2D, or AudioStreamPlayer3D");
            }
        }
        
        var hasCallbacks = _audioFinishedGlobalCallbacks.Count > 0 || _audioFinishedTargetedCallbacks.Count > 0 ||
                           _audio2DFinishedGlobalCallbacks.Count > 0 || _audio2DFinishedTargetedCallbacks.Count > 0 ||
                           _audio3DFinishedGlobalCallbacks.Count > 0 || _audio3DFinishedTargetedCallbacks.Count > 0;
        
        if (!hasCallbacks) return;
        
        SubscribeToExistingAudioPlayers(_root);
        GetTree().NodeAdded += OnNodeAddedForAudio;
        GetTree().NodeRemoved += OnNodeRemovedForAudio;
    }

    private static void AddAudioCallback<T>(
        List<(Action<T> Callback, HashSet<string>? Excludes)> globalList,
        Dictionary<string, List<(Action<T> Callback, HashSet<string>? Excludes)>> targetedDict,
        Action<T> callback, HashSet<string>? excludeSet, string? target)
    {
        if (target is null)
        {
            globalList.Add((callback, excludeSet));
        }
        else
        {
            foreach (var t in target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var n = NormalizePath(t);
                if (!targetedDict.ContainsKey(n)) targetedDict[n] = [];
                targetedDict[n].Add((callback, excludeSet));
            }
        }
    }

    private void SubscribeToExistingAudioPlayers(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            SubscribeAudioNode(node);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    private void OnNodeAddedForAudio(Node node)
    {
        if (IsInternalNode(node)) return;
        SubscribeAudioNode(node);
    }

    private void OnNodeRemovedForAudio(Node node)
    {
        switch (node)
        {
            case AudioStreamPlayer asp: _subscribedAudioPlayers.Remove(asp); break;
            case AudioStreamPlayer2D asp2d: _subscribedAudioPlayers2D.Remove(asp2d); break;
            case AudioStreamPlayer3D asp3d: _subscribedAudioPlayers3D.Remove(asp3d); break;
        }
    }

    private void SubscribeAudioNode(Node node)
    {
        switch (node)
        {
            case AudioStreamPlayer asp: SubscribeAudioPlayer(asp); break;
            case AudioStreamPlayer2D asp2d: SubscribeAudioPlayer2D(asp2d); break;
            case AudioStreamPlayer3D asp3d: SubscribeAudioPlayer3D(asp3d); break;
        }
    }

    private void SubscribeAudioPlayer(AudioStreamPlayer player)
    {
        if (_subscribedAudioPlayers.Contains(player) || IsInternalNode(player)) return;
        
        var path = player.GetPath().ToString();
        var hasAny = _audioFinishedTargetedCallbacks.ContainsKey(path) || _audioFinishedGlobalCallbacks.Count > 0;
        if (!hasAny) return;
        
        _subscribedAudioPlayers.Add(player);
        player.Finished += () =>
        {
            if (_audioFinishedTargetedCallbacks.TryGetValue(path, out var targeted))
            {
                foreach (var (callback, excludes) in targeted)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    callback(player);
                }
            }

            foreach (var (callback, excludes) in _audioFinishedGlobalCallbacks)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(player);
            }
        };
    }

    private void SubscribeAudioPlayer2D(AudioStreamPlayer2D player)
    {
        if (_subscribedAudioPlayers2D.Contains(player) || IsInternalNode(player)) return;
        
        var path = player.GetPath().ToString();
        var hasAny = _audio2DFinishedTargetedCallbacks.ContainsKey(path) || _audio2DFinishedGlobalCallbacks.Count > 0;
        if (!hasAny) return;
        
        _subscribedAudioPlayers2D.Add(player);
        player.Finished += () =>
        {
            if (_audio2DFinishedTargetedCallbacks.TryGetValue(path, out var targeted))
            {
                foreach (var (callback, excludes) in targeted)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    callback(player);
                }
            }

            foreach (var (callback, excludes) in _audio2DFinishedGlobalCallbacks)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(player);
            }
        };
    }

    private void SubscribeAudioPlayer3D(AudioStreamPlayer3D player)
    {
        if (_subscribedAudioPlayers3D.Contains(player) || IsInternalNode(player)) return;
        
        var path = player.GetPath().ToString();
        var hasAny = _audio3DFinishedTargetedCallbacks.ContainsKey(path) || _audio3DFinishedGlobalCallbacks.Count > 0;
        if (!hasAny) return;
        
        _subscribedAudioPlayers3D.Add(player);
        player.Finished += () =>
        {
            if (_audio3DFinishedTargetedCallbacks.TryGetValue(path, out var targeted))
            {
                foreach (var (callback, excludes) in targeted)
                {
                    if (PathMatchesAnyExclude(path, excludes)) continue;
                    callback(player);
                }
            }

            foreach (var (callback, excludes) in _audio3DFinishedGlobalCallbacks)
            {
                if (PathMatchesAnyExclude(path, excludes)) continue;
                callback(player);
            }
        };
    }
}

