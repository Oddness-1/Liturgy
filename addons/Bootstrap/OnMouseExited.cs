using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when the mouse exits a <see cref="Godot.Control">Control</see>, 
/// <see cref="Godot.CollisionObject2D">CollisionObject2D</see>, or <see cref="Godot.CollisionObject3D">CollisionObject3D</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, or <c>Control</c>, <c>CollisionObject2D</c>, or <c>CollisionObject3D</c> subtypes (filters by type).
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which nodes invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnMouseExited]
/// private static void AnyMouseExited() { }
/// </code>
/// <code>
/// // Any Control
/// [OnMouseExited]
/// private static void ControlMouseExited(Control c) { }
/// </code>
/// <code>
/// // Specific control type (filters to Button)
/// [OnMouseExited]
/// private static void ButtonUnhovered(Button b) { }
/// </code>
/// <code>
/// // 3D collision object
/// [OnMouseExited]
/// private static void Object3DUnhovered(CollisionObject3D obj) { }
/// </code>
/// <code>
/// // Targeted to specific node
/// [OnMouseExited(Target = "UI/InventorySlot")]
/// private static void InventorySlotUnhovered() { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnMouseExited : Attribute
{
    /// <summary>
    /// Comma-separated node paths. Fires only for these nodes (null = all).
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
