using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method while any <see cref="Godot.BaseButton">BaseButton</see> is pressed.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <c>BaseButton</c> subtypes (filters by type).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which buttons invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnButtonPressed]
    /// private static void AnyButtonPressed() { }
    /// </code>
    /// <code>
    /// // BaseButton (any button type)
    /// [Rite.OnButtonPressed]
    /// private static void AnyButtonPressed(BaseButton b) { }
    /// </code>
    /// <code>
    /// // Targeted to specific node
    /// [Rite.OnButtonPressed(Target = "UI/PlayButton")]
    /// private static void PlayPressed() { }
    /// </code>
    /// <code>
    /// // With exclusion
    /// [Rite.OnButtonPressed(Exclude = "UI/DebugBtn")]
    /// private static void NonDebugPressed(BaseButton b) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnButtonPressed : RiteAttribute
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
