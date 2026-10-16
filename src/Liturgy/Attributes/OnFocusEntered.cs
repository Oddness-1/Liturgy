using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when a <see cref="Godot.Control">Control</see> receives focus.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <see cref="Godot.Control">Control</see> subtypes (filters by type).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which controls invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnFocusEntered]
    /// private static void AnyFocusEntered() { }
    /// </code>
    /// <code>
    /// // Any control
    /// [Rite.OnFocusEntered]
    /// private static void AnyFocusEntered(Control c) { }
    /// </code>
    /// <code>
    /// // Specific control type (filters to LineEdit)
    /// [Rite.OnFocusEntered]
    /// private static void LineEditFocused(LineEdit le) { }
    /// </code>
    /// <code>
    /// // Targeted to specific node
    /// [Rite.OnFocusEntered(Target = "UI/NameInput")]
    /// private static void NameInputFocused() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnFocusEntered : RiteAttribute
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
