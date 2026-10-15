using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when a <see cref="Godot.CanvasItem">CanvasItem</see> or <see cref="Godot.Node3D">Node3D</see> visibility changes.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <c>CanvasItem</c> or <c>Node3D</c> subtypes (filters by type).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which nodes invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnVisibilityChanged]
    /// private static void AnyVisibilityChanged() { }
    /// </code>
    /// <code>
    /// // Any CanvasItem (2D)
    /// [Rite.OnVisibilityChanged]
    /// private static void CanvasItemVisibilityChanged(CanvasItem item) { }
    /// </code>
    /// <code>
    /// // Specific type (filters to Control)
    /// [Rite.OnVisibilityChanged]
    /// private static void ControlVisibilityChanged(Control c) { }
    /// </code>
    /// <code>
    /// // 3D nodes
    /// [Rite.OnVisibilityChanged]
    /// private static void Node3DVisibilityChanged(Node3D node) { }
    /// </code>
    /// <code>
    /// // Targeted to specific node
    /// [Rite.OnVisibilityChanged(Target = "UI/Popup")]
    /// private static void PopupVisibilityChanged() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnVisibilityChanged : Attribute
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
