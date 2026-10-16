using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when a <see cref="Godot.Control">Control</see> receives GUI input.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, an <see cref="Godot.InputEvent">InputEvent</see> subtypes, <see cref="Godot.Control">Control</see> subtypes, or both in any order.
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which controls invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnGuiInput]
    /// private static void AnyGuiInput() { }
    /// </code>
    /// <code>
    /// // InputEvent only
    /// [Rite.OnGuiInput]
    /// private static void AnyGuiInput(InputEvent e) { }
    /// </code>
    /// <code>
    /// // Specific event type (filters to mouse button events)
    /// [Rite.OnGuiInput]
    /// private static void MouseButtonInput(InputEventMouseButton mb) { }
    /// </code>
    /// <code>
    /// // Control only
    /// [Rite.OnGuiInput]
    /// private static void AnyGuiInput(Control source) { }
    /// </code>
    /// <code>
    /// // Both, any order
    /// [Rite.OnGuiInput]
    /// private static void AnyGuiInput(InputEvent e, Control source) { }
    /// </code>
    /// <code>
    /// // Targeted to specific node
    /// [Rite.OnGuiInput(Target = "UI/InventoryPanel")]
    /// private static void InventoryInput(InputEvent e) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnGuiInput : RiteAttribute
    {
        /// <summary>
        /// Comma-separated node paths. Fires only for these controls (null = all).
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
