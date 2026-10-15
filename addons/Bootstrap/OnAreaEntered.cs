using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when an area enters an <see cref="Godot.Area2D">Area2D</see> or <see cref="Godot.Area3D">Area3D</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, an <c>Area2D</c> or <c>Area3D</c> (the entering area), the source area, or both in any order.
/// Use typed parameters to filter by dimension (2D vs 3D).
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which areas invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnAreaEntered]
/// private static void AnyAreaEntered() { }
/// </code>
/// <code>
/// // Entering area only (2D)
/// [OnAreaEntered]
/// private static void AreaEntered2D(Area2D area) { }
/// </code>
/// <code>
/// // Entering area only (3D)
/// [OnAreaEntered]
/// private static void AreaEntered3D(Area3D area) { }
/// </code>
/// <code>
/// // Both areas (source filters to Area2D)
/// [OnAreaEntered]
/// private static void AreaEntered(Area2D entering, Area2D source) { }
/// </code>
/// <code>
/// // Targeted to specific area
/// [OnAreaEntered(Target = "Level/TriggerZone")]
/// private static void TriggerZoneAreaEntered(Area2D entering) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnAreaEntered : Attribute
{
    /// <summary>
    /// Comma-separated node paths. Fires only for these areas (null = all).
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
