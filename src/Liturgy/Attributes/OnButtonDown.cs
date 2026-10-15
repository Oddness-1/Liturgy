using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when any <see cref="Godot.BaseButton">BaseButton</see> is pressed down.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <c>BaseButton</c> subtypes (filters by type).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which buttons invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnButtonDown]
    /// private static void AnyButtonDown() { }
    /// </code>
    /// <code>
    /// // BaseButton (any button type)
    /// [Rite.OnButtonDown]
    /// private static void AnyButtonDown(BaseButton b) { }
    /// </code>
    /// <code>
    /// // Specific button type (filters to Button only)
    /// [Rite.OnButtonDown]
    /// private static void ButtonOnly(Button b) { }
    /// </code>
    /// <code>
    /// // Targeted to specific node
    /// [Rite.OnButtonDown(Target = "UI/HoldButton")]
    /// private static void HoldStarted() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnButtonDown : Attribute
    {
        /// <summary>
        /// Comma-separated node paths. Fires only for these buttons (null = all).
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
