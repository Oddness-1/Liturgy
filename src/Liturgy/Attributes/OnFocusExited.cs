using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when a <see cref="Godot.Control">Control</see> loses focus.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <see cref="Godot.Control">Control</see> subtypes (filters by type).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which controls invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnFocusExited]
    /// private static void AnyFocusExited() { }
    /// </code>
    /// <code>
    /// // Any control
    /// [Rite.OnFocusExited]
    /// private static void AnyFocusExited(Control c) { }
    /// </code>
    /// <code>
    /// // Specific control type (filters to LineEdit)
    /// [Rite.OnFocusExited]
    /// private static void LineEditUnfocused(LineEdit le) { }
    /// </code>
    /// <code>
    /// // Targeted to specific node
    /// [Rite.OnFocusExited(Target = "UI/NameInput")]
    /// private static void NameInputUnfocused() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnFocusExited : Attribute
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
