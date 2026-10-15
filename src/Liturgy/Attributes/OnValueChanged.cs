using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when a <see cref="Godot.Range">Range</see>-based control's value changes.
    /// Works with <see cref="Godot.Slider">Slider</see>, <see cref="Godot.SpinBox">SpinBox</see>, <see cref="Godot.ProgressBar">ProgressBar</see>, <see cref="Godot.ScrollBar">ScrollBar</see>, and other <c>Range</c> subtypes.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, a <see cref="double"/> (value), <c>Range</c> subtypes (source), or both in any order.
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which controls invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnValueChanged]
    /// private static void AnyValueChanged() { }
    /// </code>
    /// <code>
    /// // Value only
    /// [Rite.OnValueChanged]
    /// private static void AnyValueChanged(double value) { }
    /// </code>
    /// <code>
    /// // Source only (any Range)
    /// [Rite.OnValueChanged]
    /// private static void AnyValueChanged(Range source) { }
    /// </code>
    /// <code>
    /// // Source only (filters to SpinBox)
    /// [Rite.OnValueChanged]
    /// private static void SpinBoxOnly(SpinBox source) { }
    /// </code>
    /// <code>
    /// // Both, any order
    /// [Rite.OnValueChanged]
    /// private static void AnyValueChanged(double value, Range source) { }
    /// </code>
    /// <code>
    /// // Targeted to specific control
    /// [Rite.OnValueChanged(Target = "UI/VolumeSlider")]
    /// private static void OnVolumeChanged(double value) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnValueChanged : Attribute
    {
        /// <summary>
        /// Comma-separated node paths to target.
        /// </summary>
        public string? Target { get; set; }

        /// <summary>
        /// Comma-separated node paths to skip.
        /// </summary>
        public string? Exclude { get; set; }

        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
