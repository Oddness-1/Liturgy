using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when a <see cref="Godot.Container">Container</see> needs to sort its children.
/// </summary>
/// <remarks>
/// Parameters are optional: none, or <see cref="Godot.Container">Container</see> subtypes (filters by type).
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which containers invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnSortChildren]
/// private static void AnySortChildren() { }
/// </code>
/// <code>
/// // Any container
/// [OnSortChildren]
/// private static void AnySortChildren(Container container) { }
/// </code>
/// <code>
/// // Specific type (filters to VBoxContainer)
/// [OnSortChildren]
/// private static void VBoxSortChildren(VBoxContainer vbox) { }
/// </code>
/// <code>
/// // Targeted to specific container
/// [OnSortChildren(Target = "UI/ItemGrid")]
/// private static void ItemGridSortChildren() { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnSortChildren : Attribute
{
    /// <summary>
    /// Comma-separated node paths. Fires only for these containers (null = all).
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Comma-separated node paths to skip.
    /// </summary>
    public string? Exclude { get; set; }

    /// <summary>
    /// Execution order (lower runs first). Default is 0.
    /// </summary>
    public int Priority { get; set; }
}
