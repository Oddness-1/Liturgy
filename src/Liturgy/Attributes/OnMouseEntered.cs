using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when the mouse enters a <see cref="Godot.Control">Control</see>, 
    /// <see cref="Godot.CollisionObject2D">CollisionObject2D</see>, or <see cref="Godot.CollisionObject3D">CollisionObject3D</see>.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <c>Control</c>, <c>CollisionObject2D</c>, or <c>CollisionObject3D</c> subtypes (filters by type).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which nodes invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnMouseEntered]
    /// private static void AnyMouseEntered() { }
    /// </code>
    /// <code>
    /// // Any Control
    /// [Rite.OnMouseEntered]
    /// private static void ControlMouseEntered(Control c) { }
    /// </code>
    /// <code>
    /// // Specific control type (filters to Button)
    /// [Rite.OnMouseEntered]
    /// private static void ButtonHovered(Button b) { }
    /// </code>
    /// <code>
    /// // 3D collision object
    /// [Rite.OnMouseEntered]
    /// private static void Object3DHovered(CollisionObject3D obj) { }
    /// </code>
    /// <code>
    /// // Targeted to specific node
    /// [Rite.OnMouseEntered(Target = "UI/InventorySlot")]
    /// private static void InventorySlotHovered() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnMouseEntered : Attribute
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
}
