using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when an <see cref="Godot.AcceptDialog">AcceptDialog</see> is confirmed (OK button pressed).
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <see cref="Godot.AcceptDialog">AcceptDialog</see> subtypes (filters by type).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which dialogs invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnConfirmed]
    /// private static void AnyConfirmed() { }
    /// </code>
    /// <code>
    /// // Any dialog
    /// [Rite.OnConfirmed]
    /// private static void AnyConfirmed(AcceptDialog dialog) { }
    /// </code>
    /// <code>
    /// // Specific type (filters to ConfirmationDialog)
    /// [Rite.OnConfirmed]
    /// private static void ConfirmationConfirmed(ConfirmationDialog dialog) { }
    /// </code>
    /// <code>
    /// // Targeted to specific dialog
    /// [Rite.OnConfirmed(Target = "UI/DeleteConfirmDialog")]
    /// private static void DeleteConfirmed() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnConfirmed : RiteAttribute
    {
        /// <summary>
        /// Comma-separated node paths. Fires only for these dialogs (null = all).
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
