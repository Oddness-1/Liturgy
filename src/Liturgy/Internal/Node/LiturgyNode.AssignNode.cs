using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    private readonly record struct AssignNodeEntry(
        MemberInfo Member,
        string Path,
        Type NodeType,
        bool IsProperty);

    private readonly List<AssignNodeEntry> _assignNodeEntries = [];

    private void SetupAssignNodeEntries()
    {
        foreach (var type in GetUserTypes()) ScanTypeForAssignNode(type);
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

        // Scan static fields
        foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = field.GetCustomAttribute<AssignNode>();
            if (attr is null) continue;

            if (!typeof(Node).IsAssignableFrom(field.FieldType))
            {
                var memberName = $"{type.FullName}.{field.Name}";
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] [Rite.AssignNode] '{memberName}' must be of type Node or derived. Skipping.");
                continue;
            }

            var normalizedPath = NormalizePath(attr.Path);
            _assignNodeEntries.Add(new AssignNodeEntry(field, normalizedPath, field.FieldType, IsProperty: false));
            
            LiturgyReport.AddAssignNodeEntry($"{type.FullName}.{field.Name}", normalizedPath, field.FieldType.Name);
        }

        // Scan static properties
        foreach (var property in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = property.GetCustomAttribute<AssignNode>();
            if (attr is null) continue;

            if (!property.CanWrite)
            {
                var memberName = $"{type.FullName}.{property.Name}";
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] [Rite.AssignNode] '{memberName}' must have a setter. Skipping.");
                continue;
            }

            if (!typeof(Node).IsAssignableFrom(property.PropertyType))
            {
                var memberName = $"{type.FullName}.{property.Name}";
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] [Rite.AssignNode] '{memberName}' must be of type Node or derived. Skipping.");
                continue;
            }

            var normalizedPath = NormalizePath(attr.Path);
            _assignNodeEntries.Add(new AssignNodeEntry(property, normalizedPath, property.PropertyType, IsProperty: true));
            
            LiturgyReport.AddAssignNodeEntry($"{type.FullName}.{property.Name}", normalizedPath, property.PropertyType.Name);
        }
    }

    private void ExecuteAssignNodeEntries()
    {
        foreach (var entry in _assignNodeEntries)
        {
            var node = ResolveNodePath(entry.Path);
            var memberName = $"{entry.Member.DeclaringType?.FullName}.{entry.Member.Name}";

            if (node is null)
            {
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] [Rite.AssignNode] '{memberName}': Node not found at path '{entry.Path}'. Skipping.");
                continue;
            }

            if (!entry.NodeType.IsInstanceOfType(node))
            {
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] [Rite.AssignNode] '{memberName}': Node at '{entry.Path}' is {node.GetType().Name}, expected {entry.NodeType.Name}. Skipping.");
                continue;
            }

            try
            {
                if (entry.IsProperty)
                    ((PropertyInfo)entry.Member).SetValue(null, node);
                else
                    ((FieldInfo)entry.Member).SetValue(null, node);
            }
            catch (Exception ex)
            {
                LiturgyReport.AddConsoleWarning(
                    $"[Liturgy] [Rite.AssignNode] '{memberName}': Failed to assign: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Resolves a node path, supporting unique name syntax (%).
    /// For paths like "Headspace/%Actions", resolves "Headspace" from /root/,
    /// then calls GetNode("%Actions") from that context.
    /// </summary>
    private Node? ResolveNodePath(string path)
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
            LiturgyReport.AddConsoleWarning(
                $"[Liturgy] [Rite.AssignNode] Context node not found at '{normalizedContext}' for unique name lookup '{uniqueNamePath}'.");
            return null;
        }
        
        // Now resolve the unique name from the context node
        return contextNode.GetNodeOrNull(uniqueNamePath);
    }
}

