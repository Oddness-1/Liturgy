using System;
using System.Reflection;
using Godot;

namespace Liturgy;

/// <summary>
/// One static field or property that <see cref="Rite.AssignNode"/> keeps pointed at a node, as the running game sees
/// it. Read them from <see cref="Rite.Diagnostics.Assignments"/>.
/// </summary>
public sealed class AssignmentInfo
{
    internal AssignmentInfo(MemberInfo member, string path, Type memberType)
    {
        Member = member;
        Path = path;
        MemberType = memberType;
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
    /// The node the member holds now, or null while no fitting node is at <see cref="Path"/>.
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

    internal void Write(Node? node)
    {
        if (Member is PropertyInfo property) property.SetValue(null, node);
        else ((FieldInfo)Member).SetValue(null, node);
    }
}
