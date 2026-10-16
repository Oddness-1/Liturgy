using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when dragging ends on a <see cref="Godot.Slider">Slider</see>.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, a <see cref="bool"/> (value_changed), <see cref="Godot.Slider">Slider</see> subtypes, or both in any order.
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which sliders invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnDragEnded]
    /// private static void AnyDragEnded() { }
    /// </code>
    /// <code>
    /// // Value changed flag only
    /// [Rite.OnDragEnded]
    /// private static void AnyDragEnded(bool valueChanged) { }
    /// </code>
    /// <code>
    /// // Slider only
    /// [Rite.OnDragEnded]
    /// private static void AnyDragEnded(Slider slider) { }
    /// </code>
    /// <code>
    /// // Both, any order
    /// [Rite.OnDragEnded]
    /// private static void AnyDragEnded(bool valueChanged, Slider slider) { }
    /// </code>
    /// <code>
    /// // Targeted to specific slider
    /// [Rite.OnDragEnded(Target = "UI/VolumeSlider")]
    /// private static void VolumeDragEnded(bool valueChanged) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnDragEnded : RiteAttribute
    {
        /// <summary>
        /// Comma-separated node paths. Fires only for these sliders (null = all).
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
