using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when a body enters an <see cref="Godot.Area2D">Area2D</see>, <see cref="Godot.Area3D">Area3D</see>,
/// <see cref="Godot.RigidBody2D">RigidBody2D</see>, or <see cref="Godot.RigidBody3D">RigidBody3D</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, a <see cref="Godot.Node">Node</see> (the entering body), the source node, or both in any order.
/// Use typed parameters to filter by source type (e.g., <c>Area2D</c> vs <c>Area3D</c>).
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which nodes invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnBodyEntered]
/// private static void AnyBodyEntered() { }
/// </code>
/// <code>
/// // Body only (2D)
/// [OnBodyEntered]
/// private static void BodyEntered2D(Node2D body) { }
/// </code>
/// <code>
/// // Body only (3D)
/// [OnBodyEntered]
/// private static void BodyEntered3D(Node3D body) { }
/// </code>
/// <code>
/// // Source only (filters to Area2D)
/// [OnBodyEntered]
/// private static void Area2DBodyEntered(Area2D source) { }
/// </code>
/// <code>
/// // Both, any order
/// [OnBodyEntered]
/// private static void BodyEntered(Node2D body, Area2D source) { }
/// </code>
/// <code>
/// // Targeted to specific area
/// [OnBodyEntered(Target = "Level/DamageZone")]
/// private static void DamageZoneEntered(Node2D body) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnBodyEntered : Attribute
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
