using System;
using System.Collections.Generic;
using Godot;
// ReSharper disable InconsistentNaming
using static Liturgy.Rite;

namespace Liturgy.Internal;

/// <summary>
/// Internal helper node attached to targets for per-node process hooks.
/// Attached as InternalMode.Front child, fires immediately after parent processes.
/// </summary>
internal partial class ProcessHook : Node
{
    // Static rites run every frame the target isn't paused, whether or not the target processes itself.
    public event Action<double, Node>? Processed;
    public event Action<double, Node>? PhysicsProcessed;

    // Interface rites run as the node's own processing would: only while it processes and isn't paused.
    public event Action<double>? InstanceProcessed;
    public event Action<double>? InstancePhysicsProcessed;

    /// <summary>
    /// Set once the node's interface rites are subscribed, so a node re-entering the tree doesn't subscribe twice.
    /// </summary>
    public bool InterfaceRitesAttached { get; set; }

    /// <summary>
    /// Which static rites are subscribed, so a node re-entering the tree doesn't subscribe them twice.
    /// </summary>
    public bool ProcessRitesAttached { get; set; }
    public bool PhysicsProcessRitesAttached { get; set; }

    // Hooks created but not yet added to their target (the add is deferred), by target id, so a second request finds them.
    private static readonly Dictionary<ulong, ProcessHook> _pending = [];

    private Node _target = null!;
    private bool _wantsProcess;
    private bool _wantsPhysicsProcess;
    private string? _targetPath;

    /// <summary>
    /// The target's path, kept until it's renamed or moved, so rites don't build it every frame.
    /// </summary>
    public string TargetPath => _targetPath ??= _target.GetPath().ToString();

    /// <summary>
    /// Creates and attaches a ProcessHook to the target node.
    /// </summary>
    public static ProcessHook Create(Node target, bool process, bool physicsProcess)
    {
        var hook = new ProcessHook
        {
            Name = "__LiturgyProcessHook",
            ProcessMode = ProcessModeEnum.Always
        };
        hook._target = target;
        hook.Want(process, physicsProcess);

        var id = target.GetInstanceId();
        _pending[id] = hook;

        // Deferred to avoid "Parent node is busy setting up children" errors during the initial tree traversal in _Ready().
        // A target freed before then never gets the hook, so the hook is freed instead of leaking.
        Callable.From(() =>
        {
            _pending.Remove(id);
            if (IsInstanceValid(target)) target.AddChild(hook, false, InternalMode.Front);
            else hook.Free();
        }).CallDeferred();

        return hook;
    }

    /// <summary>
    /// Finds an existing ProcessHook on the target, including one still waiting to be added, or null if none exists.
    /// </summary>
    public static ProcessHook? Find(Node target)
    {
        if (_pending.TryGetValue(target.GetInstanceId(), out var pending)) return pending;

        // Internal nodes require explicit includeInternal: true
        for (var i = 0; i < target.GetChildCount(includeInternal: true); i++)
        {
            var child = target.GetChild(i, includeInternal: true);
            if (child is ProcessHook hook) return hook;
        }
        return null;
    }

    /// <summary>
    /// Gets existing or creates new ProcessHook for the target.
    /// </summary>
    public static ProcessHook GetOrCreate(Node target, bool process, bool physicsProcess)
    {
        var existing = Find(target);
        if (existing is null) return Create(target, process, physicsProcess);

        existing.Want(process, physicsProcess);
        return existing;
    }

    // Only ever turns processing on: what one rite needs stays on when another asks for something else.
    private void Want(bool process, bool physicsProcess)
    {
        _wantsProcess |= process;
        _wantsPhysicsProcess |= physicsProcess;
        if (IsNodeReady()) Apply();
    }

    private void Apply()
    {
        SetProcess(_wantsProcess);
        SetPhysicsProcess(_wantsPhysicsProcess);
    }

    // Godot turns on both kinds of processing at ready, since this script overrides both; only the wanted ones stay on.
    public override void _Ready() => Apply();

    public override void _EnterTree() => _targetPath = null;

    public override void _Notification(int what)
    {
        if (what == NotificationPathRenamed) _targetPath = null;
    }

    public override void _Process(double delta)
    {
        // This hook always processes, so it follows its target's pause mode by checking CanProcess.
        if (!_target.CanProcess()) return;
        Processed?.Invoke(delta, _target);
        if (_target.IsProcessing()) InstanceProcessed?.Invoke(delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_target.CanProcess()) return;
        PhysicsProcessed?.Invoke(delta, _target);
        if (_target.IsPhysicsProcessing()) InstancePhysicsProcessed?.Invoke(delta);
    }
}
