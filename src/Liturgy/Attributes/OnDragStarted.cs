using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when dragging starts on a <see cref="Godot.Slider">Slider</see>.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <see cref="Godot.Slider">Slider</see> subtypes (filters by type).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which sliders invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnDragStarted]
    /// private static void AnyDragStarted() { }
    /// </code>
    /// <code>
    /// // Any slider
    /// [Rite.OnDragStarted]
    /// private static void AnyDragStarted(Slider slider) { }
    /// </code>
    /// <code>
    /// // Specific type (filters to HSlider)
    /// [Rite.OnDragStarted]
    /// private static void HSliderDragStarted(HSlider slider) { }
    /// </code>
    /// <code>
    /// // Targeted to specific slider
    /// [Rite.OnDragStarted(Target = "UI/VolumeSlider")]
    /// private static void VolumeDragStarted() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnDragStarted : Attribute
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
