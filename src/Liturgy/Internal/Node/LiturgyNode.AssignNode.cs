using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    private readonly List<AssignmentInfo> _assignments = [];
    private int _waitingAssignments;
    private bool _followingAssignments;

    // Interface properties by interface (generic ones by their definition), and what each node type holds of them.
    private readonly Dictionary<Type, List<AssignmentInfo>> _perNodeAssignments = [];
    private readonly Dictionary<Type, (AssignmentInfo Assignment, PropertyInfo Property)[]?> _perNodeAssignmentsByType = [];

    // Static members follow their paths from /root's ready, as documented; interface properties from Liturgy's start,
    // so a node in the first scene is filled before its own _Ready.
    private readonly HashSet<AssignmentInfo> _assignmentsLive = [];

    public IReadOnlyList<AssignmentInfo> Assignments => _assignments;

    private void SetupAssignNodeEntries()
    {
        foreach (var type in GetUserTypes()) ScanTypeForAssignNode(type);
        if (_perNodeAssignments.Count == 0) return;

        foreach (var assignment in _assignments.Where(assignment => assignment.PerNode))
        {
            _assignmentsLive.Add(assignment);
            TryAssign(assignment, warn: false);
        }

        FollowAssignments();
        GetTree().NodeRemoved += OnNodeRemovedForAssignments;

        var stack = new Stack<Node>();
        stack.Push(_root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            AddHolder(node);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    private void ScanTypeForAssignNode(Type type)
    {
        // A static member of an open generic class has no single value to assign.
        if (IsOpenGenericClass(type))
        {
            const BindingFlags STATIC = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var member in type.GetMembers(STATIC).Where(member => member.IsDefined(typeof(AssignNode))))
                GD.PushError($"[Liturgy] [Rite.AssignNode] '{type.FullName}.{member.Name}' is in a generic class, which has no single value to assign. Skipping.");
            return;
        }

        foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.GetCustomAttribute<AssignNode>() is { } attribute) AddAssignment(type, field, field.FieldType, attribute, perNode: false);
        }

        // Instance properties count only on an interface, where each implementing node holds its own; an instance
        // member of a class is LIT008 at compile time.
        var properties = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | (type.IsInterface ? BindingFlags.Instance : 0);
        foreach (var property in type.GetProperties(properties))
        {
            if (property.GetCustomAttribute<AssignNode>() is not { } attribute) continue;

            if (!property.CanWrite)
            {
                LiturgyReport.AddConsoleWarning($"[Liturgy] [Rite.AssignNode] '{type.FullName}.{property.Name}' must have a setter. Skipping.");
                continue;
            }

            AddAssignment(type, property, property.PropertyType, attribute, perNode: !property.GetMethod!.IsStatic);
        }
    }

    private void AddAssignment(Type type, MemberInfo member, Type memberType, AssignNode attribute, bool perNode)
    {
        // An interface is checked against the node found, like any other type.
        if (!typeof(Node).IsAssignableFrom(memberType) && !memberType.IsInterface)
        {
            LiturgyReport.AddConsoleWarning($"[Liturgy] [Rite.AssignNode] '{type.FullName}.{member.Name}' must be a Node type or an interface. Skipping.");
            return;
        }

        // Every holder holds the same node, so the type can't vary with a generic interface's arguments.
        if (memberType.ContainsGenericParameters)
        {
            LiturgyReport.AddConsoleWarning($"[Liturgy] [Rite.AssignNode] '{type.FullName}.{member.Name}' can't have a type that uses the interface's type parameters. Skipping.");
            return;
        }

        var path = IsPattern(attribute.Path) ? NormalizeExcludePattern(attribute.Path) : NormalizePath(attribute.Path);
        var assignment = new AssignmentInfo(member, path, memberType, perNode) { Problem = "Waiting for /root to be ready." };
        _assignments.Add(assignment);
        _waitingAssignments++;
        LiturgyReport.AddAssignNodeEntry($"{type.FullName}.{member.Name}", path, memberType.Name);

        if (!perNode) return;

        var owner = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        if (!_perNodeAssignments.TryGetValue(owner, out var assignments))
        {
            assignments = [];
            _perNodeAssignments[owner] = assignments;
        }

        assignments.Add(assignment);
    }

    private void ExecuteAssignNodeEntries()
    {
        foreach (var assignment in _assignments)
        {
            _assignmentsLive.Add(assignment);
            if (assignment.Node is null) TryAssign(assignment, warn: true);
        }

        // Each member follows its path, so a node that arrives there later, such as after a scene change, is assigned.
        if (_assignments.Count > 0) FollowAssignments();
    }

    private void FollowAssignments()
    {
        if (_followingAssignments) return;
        _followingAssignments = true;
        GetTree().NodeAdded += OnNodeAddedForAssignments;
    }

    private void OnNodeAddedForAssignments(Node node)
    {
        if (_waitingAssignments > 0)
        {
            var name = node.Name.ToString();
            foreach (var assignment in _assignments)
            {
                if (assignment.Node is not null || !_assignmentsLive.Contains(assignment)) continue;

                if (IsPattern(assignment.Path))
                {
                    if (MatchesPattern(assignment, node)) Assign(assignment, node, warn: false);
                }
                else if (LastSegment(assignment.Path) == name) TryAssign(assignment, warn: false);
            }
        }

        // After the node may have been assigned somewhere, so a node that holds itself gets itself.
        if (_perNodeAssignments.Count > 0) AddHolder(node);
    }

    private void AddHolder(Node node)
    {
        if (PerNodeAssignmentsFor(node.GetType()) is not { } held || IsHiddenFromRites(node)) return;
        foreach (var (assignment, property) in held) assignment.AddHolder(node, property);
    }

    private void OnNodeRemovedForAssignments(Node node)
    {
        if (PerNodeAssignmentsFor(node.GetType()) is not { } held) return;
        foreach (var (assignment, _) in held) assignment.RemoveHolder(node);
    }

    private (AssignmentInfo Assignment, PropertyInfo Property)[]? PerNodeAssignmentsFor(Type type)
    {
        if (_perNodeAssignmentsByType.TryGetValue(type, out var cached)) return cached;

        List<(AssignmentInfo, PropertyInfo)>? held = null;
        foreach (var implemented in type.GetInterfaces())
        {
            var definition = implemented.IsGenericType ? implemented.GetGenericTypeDefinition() : implemented;
            if (!_perNodeAssignments.TryGetValue(definition, out var assignments)) continue;

            foreach (var assignment in assignments)
            {
                // A generic interface's property, closed over this implementation's type arguments.
                const BindingFlags DECLARED = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                var property = implemented.IsGenericType ? implemented.GetProperty(assignment.Member.Name, DECLARED) : (PropertyInfo)assignment.Member;
                if (property is null) continue;

                held ??= [];
                held.Add((assignment, property));
            }
        }

        var result = held?.ToArray();
        _perNodeAssignmentsByType[type] = result;
        return result;
    }

    private void TryAssign(AssignmentInfo assignment, bool warn)
    {
        if (IsPattern(assignment.Path))
        {
            if (FirstMatch(assignment) is { } match) Assign(assignment, match, warn);
            else
            {
                Wait(assignment, $"No {assignment.MemberType.Name} matches the pattern yet.");
                if (warn) LiturgyReport.AddConsoleWarning($"[Liturgy] [Rite.AssignNode] '{assignment.FullName}': No {assignment.MemberType.Name} matches '{assignment.Path}'. It's assigned when one arrives.");
            }

            return;
        }

        var node = ResolveNodePath(assignment.Path, warn);

        if (node is null)
        {
            Wait(assignment, "No node at the path yet.");
            if (warn) LiturgyReport.AddConsoleWarning($"[Liturgy] [Rite.AssignNode] '{assignment.FullName}': Node not found at path '{assignment.Path}'. It's assigned when one arrives there.");
            return;
        }

        if (!assignment.MemberType.IsInstanceOfType(node))
        {
            Wait(assignment, $"The node at the path is {node.GetType().Name}, not {assignment.MemberType.Name}.");
            if (warn) LiturgyReport.AddConsoleWarning($"[Liturgy] [Rite.AssignNode] '{assignment.FullName}': Node at '{assignment.Path}' is {node.GetType().Name}, expected {assignment.MemberType.Name}. Skipping.");
            return;
        }

        Assign(assignment, node, warn);
    }

    private void Assign(AssignmentInfo assignment, Node node, bool warn)
    {
        try
        {
            assignment.Write(node);
        }
        catch (Exception exception)
        {
            Wait(assignment, $"Assigning it failed: {exception.Message}");
            if (warn) LiturgyReport.AddConsoleWarning($"[Liturgy] [Rite.AssignNode] '{assignment.FullName}': Failed to assign: {exception.Message}");
            return;
        }

        if (assignment.Problem is not null) _waitingAssignments--;
        assignment.Node = node;
        assignment.Problem = null;
        assignment.Assignments++;
        node.TreeExiting += Left;

        return;

        // The member is emptied rather than left holding a node that's gone, and waits for the next one at its path.
        // A pattern may already match another node in the tree, which is looked for once this one is out.
        void Left()
        {
            node.TreeExiting -= Left;
            if (assignment.Node != node) return;

            assignment.Node = null;
            try { assignment.Write(null); }
            catch (Exception exception) { GD.PushWarning($"[Liturgy] [Rite.AssignNode] '{assignment.FullName}': Failed to clear: {exception.Message}"); }
            Wait(assignment, "Its node left the tree; it's assigned again when one arrives at the path.");

            if (IsPattern(assignment.Path)) Callable.From(() => { if (assignment.Node is null) TryAssign(assignment, warn: false); }).CallDeferred();
        }
    }

    private static bool IsPattern(string path) => path.Contains('*');

    // The first fitting node in tree order, outside Liturgy's own nodes and anything hidden from rites.
    private Node? FirstMatch(AssignmentInfo assignment)
    {
        var stack = new Stack<Node>();
        stack.Push(_root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (IsShielded(node)) continue;
            if (node != _root && MatchesPattern(assignment, node)) return node;

            var children = node.GetChildren();
            for (var i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
        }

        return null;
    }

    // A last segment written %Name also needs the node to have a scene-unique name, as % means in a node path.
    private static bool MatchesPattern(AssignmentInfo assignment, Node node)
    {
        if (!assignment.MemberType.IsInstanceOfType(node) || !node.IsInsideTree() || IsShielded(node)) return false;

        var pattern = assignment.Path;
        var last = pattern[(pattern.LastIndexOf('/') + 1)..];
        if (last.StartsWith('%'))
        {
            if (!node.UniqueNameInOwner) return false;
            pattern = pattern[..(pattern.Length - last.Length)] + last[1..];
        }

        return PathMatchesAnyExclude(node.GetPath().ToString(), [pattern]);
    }

    private void Wait(AssignmentInfo assignment, string problem)
    {
        if (assignment.Problem is null) _waitingAssignments++;
        assignment.Problem = problem;
    }

    private static string LastSegment(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return name.StartsWith('%') ? name[1..] : name;
    }

    /// <summary>
    /// Resolves a node path, supporting unique name syntax (%).
    /// For paths like "Headspace/%Actions", resolves "Headspace" from /root/,
    /// then calls GetNode("%Actions") from that context.
    /// </summary>
    private Node? ResolveNodePath(string path, bool warn)
    {
        var percentIndex = path.IndexOf('%');

        // No % in path - standard resolution
        if (percentIndex < 0)
            return _root.GetNodeOrNull(path);

        // Path starts with % - resolve from /root/ directly
        if (percentIndex == 0)
            return _root.GetNodeOrNull(path);

        // Split on the % - everything before is the context path
        // Handle both "Headspace/%Actions" and "/root/Headspace/%Actions"
        var contextPath = path[..(percentIndex - 1)]; // -1 to remove trailing /
        var uniqueNamePath = path[percentIndex..];     // Includes the %

        // Normalize and resolve the context node
        var normalizedContext = NormalizePath(contextPath);
        var contextNode = _root.GetNodeOrNull(normalizedContext);

        if (contextNode is null)
        {
            if (warn)
            {
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] [Rite.AssignNode] Context node not found at '{normalizedContext}' for unique name lookup '{uniqueNamePath}'.");
            }

            return null;
        }

        // Now resolve the unique name from the context node
        return contextNode.GetNodeOrNull(uniqueNamePath);
    }
}
