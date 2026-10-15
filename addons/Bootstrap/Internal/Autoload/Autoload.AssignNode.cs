using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace Bootstrap.Internal;

public partial class Autoload
{
    private readonly record struct AssignNodeEntry(
        MemberInfo Member,
        string Path,
        Type NodeType,
        bool IsProperty);

    private readonly List<AssignNodeEntry> _assignNodeEntries = [];

    private void SetupAssignNodeEntries()
    {
        foreach (var assembly in GetUserAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    ScanTypeForAssignNode(type);
                }
            }
            catch (ReflectionTypeLoadException) { /* Skip gracefully */ }
        }
    }

    private void ScanTypeForAssignNode(Type type)
    {
        // Scan static fields
        foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = field.GetCustomAttribute<AssignNode>();
            if (attr is null) continue;

            if (!typeof(Node).IsAssignableFrom(field.FieldType))
            {
                var memberName = $"{type.FullName}.{field.Name}";
                BootstrapReport.AddConsoleWarning(
                    $"[Bootstrap] [AssignNode] '{memberName}' must be of type Node or derived. Skipping.");
                continue;
            }

            var normalizedPath = NormalizePath(attr.Path);
            _assignNodeEntries.Add(new AssignNodeEntry(field, normalizedPath, field.FieldType, IsProperty: false));
            
            BootstrapReport.AddAssignNodeEntry($"{type.FullName}.{field.Name}", normalizedPath, field.FieldType.Name);
        }

        // Scan static properties
        foreach (var property in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = property.GetCustomAttribute<AssignNode>();
            if (attr is null) continue;

            if (!property.CanWrite)
            {
                var memberName = $"{type.FullName}.{property.Name}";
                BootstrapReport.AddConsoleWarning(
                    $"[Bootstrap] [AssignNode] '{memberName}' must have a setter. Skipping.");
                continue;
            }

            if (!typeof(Node).IsAssignableFrom(property.PropertyType))
            {
                var memberName = $"{type.FullName}.{property.Name}";
                BootstrapReport.AddConsoleWarning(
                    $"[Bootstrap] [AssignNode] '{memberName}' must be of type Node or derived. Skipping.");
                continue;
            }

            var normalizedPath = NormalizePath(attr.Path);
            _assignNodeEntries.Add(new AssignNodeEntry(property, normalizedPath, property.PropertyType, IsProperty: true));
            
            BootstrapReport.AddAssignNodeEntry($"{type.FullName}.{property.Name}", normalizedPath, property.PropertyType.Name);
        }
    }

    private void ExecuteAssignNodeEntries()
    {
        foreach (var entry in _assignNodeEntries)
        {
            var node = _root.GetNodeOrNull(entry.Path);
            var memberName = $"{entry.Member.DeclaringType?.FullName}.{entry.Member.Name}";

            if (node is null)
            {
                BootstrapReport.AddConsoleWarning(
                    $"[Bootstrap] [AssignNode] '{memberName}': Node not found at path '{entry.Path}'. Skipping.");
                continue;
            }

            if (!entry.NodeType.IsInstanceOfType(node))
            {
                BootstrapReport.AddConsoleWarning(
                    $"[Bootstrap] [AssignNode] '{memberName}': Node at '{entry.Path}' is {node.GetType().Name}, expected {entry.NodeType.Name}. Skipping.");
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
                BootstrapReport.AddConsoleWarning(
                    $"[Bootstrap] [AssignNode] '{memberName}': Failed to assign: {ex.Message}");
            }
        }
    }
}
