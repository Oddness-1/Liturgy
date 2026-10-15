using System;
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
    
    private Node _target = null!;
    
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
        hook.SetProcess(process);
        hook.SetPhysicsProcess(physicsProcess);
        
        // Use CallDeferred to avoid "Parent node is busy setting up children" errors
        // during initial scene tree traversal in _Ready()
        target.CallDeferred(Node.MethodName.AddChild, hook, false, (int)InternalMode.Front);
        
        return hook;
    }
    
    /// <summary>
    /// Finds an existing ProcessHook on the target, or null if none exists.
    /// </summary>
    public static ProcessHook? Find(Node target)
    {
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

        // Enable additional processing if needed
        if (process && !existing.IsProcessing())
            existing.SetProcess(true);
        if (physicsProcess && !existing.IsPhysicsProcessing())
            existing.SetPhysicsProcess(true);
        return existing;

    }
    
    public override void _Process(double delta)
    {
        // This hook always processes, so it follows its target's pause mode by checking CanProcess.
        if (!_target.IsProcessing() || !_target.CanProcess()) return;
        Processed?.Invoke(delta, _target);
        InstanceProcessed?.Invoke(delta);
    }
    
    public override void _PhysicsProcess(double delta)
    {
        if (!_target.IsPhysicsProcessing() || !_target.CanProcess()) return;
        PhysicsProcessed?.Invoke(delta, _target);
        InstancePhysicsProcessed?.Invoke(delta);
    }
}

