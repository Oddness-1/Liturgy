using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when scrolling ends in a <see cref="Godot.ScrollContainer">ScrollContainer</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, or <see cref="Godot.ScrollContainer">ScrollContainer</see>.
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which containers invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnScrollEnded]
/// private static void AnyScrollEnded() { }
/// </code>
/// <code>
/// // ScrollContainer source
/// [OnScrollEnded]
/// private static void AnyScrollEnded(ScrollContainer container) { }
/// </code>
/// <code>
/// // Targeted to specific container
/// [OnScrollEnded(Target = "UI/ItemList")]
/// private static void ItemListScrollEnded() { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnScrollEnded : Attribute
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
