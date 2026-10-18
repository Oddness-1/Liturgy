using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace Liturgy;

/// <summary>
/// One field or property that <see cref="Rite.AssignNode"/> keeps pointed at a node, as the running game sees it: a
/// static member, or an interface property that every implementing node in the tree holds its own copy of. Read them
/// from <see cref="Rite.Diagnostics.Assignments"/>.
/// </summary>
public sealed class AssignmentInfo
{
    private readonly Dictionary<ulong, (Node Holder, PropertyInfo Property)> _holders = [];

    internal AssignmentInfo(MemberInfo member, string path, Type memberType, bool perNode)
    {
        Member = member;
        Path = path;
        MemberType = memberType;
        PerNode = perNode;
        Name = $"{member.DeclaringType?.Name}.{member.Name}";
        FullName = $"{member.DeclaringType?.FullName}.{member.Name}";
    }

    /// <summary>
    /// <c>Type.Member</c>, short enough to read at a glance.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// <c>Namespace.Type.Member</c>.
    /// </summary>
    public string FullName { get; }

    /// <summary>
    /// The path the member follows, normalized to start at <c>/root</c>.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The member's declared type: a node type or an interface.
    /// </summary>
    public Type MemberType { get; }

    /// <summary>
    /// True for an interface property, which each implementing node holds; false for a static member.
    /// </summary>
    public bool PerNode { get; }

    /// <summary>
    /// The nodes in the tree that hold the member, for an interface property. Empty for a static member.
    /// </summary>
    public IReadOnlyCollection<Node> Holders => _holders.Values.Select(entry => entry.Holder).ToArray();

    /// <summary>
    /// The node the member holds now, or null while no fitting node is at <see cref="Path"/>. For an interface
    /// property, every holder holds this same node.
    /// </summary>
    public Node? Node { get; internal set; }

    /// <summary>
    /// How many times Liturgy has assigned the member, counting each time a node arrived at its path again.
    /// </summary>
    public int Assignments { get; internal set; }

    /// <summary>
    /// Why the member is empty, when it is. Null while it holds a node.
    /// </summary>
    public string? Problem { get; internal set; }

    internal MemberInfo Member { get; }

    internal int HolderCount => _holders.Count;

    // A holder's property is the interface's own, closed over the holder's type arguments when the interface is generic.
    internal void AddHolder(Node holder, PropertyInfo property)
    {
        _holders[holder.GetInstanceId()] = (holder, property);
        WriteTo(holder, property, Node);
    }

    // The holder is emptied as it leaves, so it never keeps a node it no longer follows.
    internal void RemoveHolder(Node holder)
    {
        if (_holders.Remove(holder.GetInstanceId(), out var entry)) WriteTo(entry.Holder, entry.Property, null);
    }

    internal void Write(Node? node)
    {
        if (!PerNode)
        {
            if (Member is PropertyInfo property) property.SetValue(null, node);
            else ((FieldInfo)Member).SetValue(null, node);
            return;
        }

        foreach (var (holder, property) in _holders.Values.ToArray()) WriteTo(holder, property, node);
    }

    private void WriteTo(Node holder, PropertyInfo property, Node? node)
    {
        if (!GodotObject.IsInstanceValid(holder)) return;

        // One holder's setter failing doesn't keep the node from the rest.
        try { property.SetValue(holder, node); }
        catch (Exception exception)
        {
            var inner = exception is TargetInvocationException { InnerException: { } thrown } ? thrown : exception;
            GD.PushWarning($"[Liturgy] [Rite.AssignNode] '{FullName}' on {holder.GetPath()}: Failed to assign: {inner.Message}");
        }
    }
}
