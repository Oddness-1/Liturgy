using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Godot;

namespace Liturgy;

/// <summary>
/// One method that carries rites, as the running game sees it: what it is, whether it's running, and what it has done
/// so far. Read them from <see cref="Rite.Diagnostics.Rites"/>.
/// </summary>
/// <remarks>
/// Counts and timings are kept only in editor runs (see <see cref="Rite.Diagnostics.IsEnabled"/>); in an exported game
/// they stay at zero.
/// </remarks>
public sealed class RiteInfo
{
    internal RiteInfo(MethodInfo method)
    {
        Method = method;
        Name = $"{method.DeclaringType?.Name}.{method.Name}";
        FullName = $"{method.DeclaringType?.FullName}.{method.Name}";
        Attributes = method.GetCustomAttributes<RiteAttribute>().ToArray();
        Description = Attributes.Select(attribute => attribute.Description).FirstOrDefault(description => !string.IsNullOrWhiteSpace(description));
        OnInterface = method.DeclaringType?.IsInterface == true;
        Kinds = string.Join(", ", Attributes.Select(KindOf).Distinct());
        Priority = Attributes.Select(PriorityOf).DefaultIfEmpty(0).Min();
    }

    /// <summary>
    /// The method the rites are declared on.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// <c>Type.Method</c>, short enough to read at a glance.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// <c>Namespace.Type.Method</c>.
    /// </summary>
    public string FullName { get; }

    /// <summary>
    /// The rites declared on the method, usually one.
    /// </summary>
    public IReadOnlyList<RiteAttribute> Attributes { get; }

    /// <summary>
    /// The rite names, e.g. <c>OnButtonPressed</c>, or <c>OnSignal("died")</c>.
    /// </summary>
    public string Kinds { get; }

    /// <summary>
    /// The first <see cref="RiteAttribute.Description"/> given on the method, if any.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// The lowest <c>Priority</c> among its rites; lower runs first.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// True for a rite declared on an interface member, which each implementing node runs for itself.
    /// </summary>
    public bool OnInterface { get; }

    /// <summary>
    /// True once Liturgy has wired the rite up. A rite that stays false was rejected; the console says why.
    /// </summary>
    public bool Running { get; internal set; }

    /// <summary>
    /// How many times the rite has run.
    /// </summary>
    public long Fires { get; private set; }

    /// <summary>
    /// How many of those runs threw.
    /// </summary>
    public long Exceptions { get; private set; }

    /// <summary>
    /// Total time spent inside the rite, in milliseconds.
    /// </summary>
    public double TotalMilliseconds => _ticks * 1000.0 / Stopwatch.Frequency;

    /// <summary>
    /// Seconds since the rite last ran, or null if it never has.
    /// </summary>
    public double? SecondsSinceLastFire => _lastFired == 0 ? null : (Stopwatch.GetTimestamp() - _lastFired) / (double)Stopwatch.Frequency;

    /// <summary>
    /// The node the rite last ran for, when it was given one and the node still exists.
    /// </summary>
    public Node? LastNode => _lastNodeId == 0 ? null : GodotObject.InstanceFromId(_lastNodeId) as Node;

    /// <summary>
    /// While true the rite is skipped, as if it weren't declared, so you can see what changes without it. Only takes
    /// effect in editor runs.
    /// </summary>
    public bool Muted { get; set; }

    /// <summary>
    /// How many times the rite has run for <paramref name="node"/> while it's been in the tree.
    /// </summary>
    public long FiresFor(Node node) => FiresFor(node.GetInstanceId());

    // Rites that run for the whole game are handed /root; counting it per node would list them on /root.
    internal static ulong RootId;

    private readonly Dictionary<ulong, long> _firesByNode = [];
    private long _ticks;
    private long _lastFired;
    private ulong _lastNodeId;

    internal long Enter(Node? node)
    {
        Fires++;
        _lastFired = Stopwatch.GetTimestamp();
        _lastNodeId = node is not null && GodotObject.IsInstanceValid(node) ? node.GetInstanceId() : 0;
        if (_lastNodeId != 0 && _lastNodeId != RootId) _firesByNode[_lastNodeId] = FiresFor(_lastNodeId) + 1;
        Rite.Diagnostics.Record(this, _lastNodeId, _lastFired);
        return _lastFired;
    }

    internal long FiresFor(ulong nodeId) => _firesByNode.GetValueOrDefault(nodeId);

    internal void Forget(ulong nodeId) => _firesByNode.Remove(nodeId);

    internal void Exit(long entered) => _ticks += Stopwatch.GetTimestamp() - entered;

    internal void Threw() => Exceptions++;

    internal void Reset()
    {
        Fires = 0;
        Exceptions = 0;
        _ticks = 0;
        _lastFired = 0;
        _lastNodeId = 0;
        _firesByNode.Clear();
    }

    /// <summary>
    /// The rite's name as written in code, e.g. <c>OnButtonPressed</c> or <c>OnSignal("died")</c>.
    /// </summary>
    public static string KindOf(RiteAttribute attribute) => attribute is Rite.OnSignal signal ? $"OnSignal(\"{signal.Signal}\")" : attribute.GetType().Name;

    internal static int PriorityOf(RiteAttribute attribute) => attribute.GetType().GetProperty("Priority")?.GetValue(attribute) is int priority ? priority : 0;

    public override string ToString() => $"{Name} [{Kinds}]";
}
