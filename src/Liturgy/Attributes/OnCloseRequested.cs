using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when a <see cref="Godot.Window">Window</see> close is requested (e.g., X button clicked).
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <see cref="Godot.Window">Window</see> subtypes (filters by type).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which windows invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnCloseRequested]
    /// private static void AnyCloseRequested() { }
    /// </code>
    /// <code>
    /// // Any window
    /// [Rite.OnCloseRequested]
    /// private static void AnyCloseRequested(Window window) { }
    /// </code>
    /// <code>
    /// // Specific type (filters to AcceptDialog)
    /// [Rite.OnCloseRequested]
    /// private static void DialogCloseRequested(AcceptDialog dialog) { }
    /// </code>
    /// <code>
    /// // Targeted to specific window
    /// [Rite.OnCloseRequested(Target = "UI/SettingsWindow")]
    /// private static void SettingsCloseRequested() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnCloseRequested : Attribute
    {
        /// <summary>
        /// Comma-separated node paths. Fires only for these windows (null = all).
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
