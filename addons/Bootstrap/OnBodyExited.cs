using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when a body exits an <see cref="Godot.Area2D">Area2D</see>, <see cref="Godot.Area3D">Area3D</see>,
/// <see cref="Godot.RigidBody2D">RigidBody2D</see>, or <see cref="Godot.RigidBody3D">RigidBody3D</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, a <see cref="Godot.Node">Node</see> (the exiting body), the source node, or both in any order.
/// Use typed parameters to filter by source type (e.g., <c>Area2D</c> vs <c>Area3D</c>).
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which nodes invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnBodyExited]
/// private static void AnyBodyExited() { }
/// </code>
/// <code>
/// // Body only (2D)
/// [OnBodyExited]
/// private static void BodyExited2D(Node2D body) { }
/// </code>
/// <code>
/// // Body only (3D)
/// [OnBodyExited]
/// private static void BodyExited3D(Node3D body) { }
/// </code>
/// <code>
/// // Source only (filters to Area2D)
/// [OnBodyExited]
/// private static void Area2DBodyExited(Area2D source) { }
/// </code>
/// <code>
/// // Both, any order
/// [OnBodyExited]
/// private static void BodyExited(Node2D body, Area2D source) { }
/// </code>
/// <code>
/// // Targeted to specific area
/// [OnBodyExited(Target = "Level/DamageZone")]
/// private static void DamageZoneExited(Node2D body) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnBodyExited : Attribute
{
    /// <summary>
    /// Comma-separated node paths. Fires only for these areas/bodies (null = all).
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
