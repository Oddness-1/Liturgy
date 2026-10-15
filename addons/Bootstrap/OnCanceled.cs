using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when an <see cref="Godot.AcceptDialog">AcceptDialog</see> is canceled (Cancel button pressed or closed).
/// </summary>
/// <remarks>
/// Parameters are optional: none, or <see cref="Godot.AcceptDialog">AcceptDialog</see> subtypes (filters by type).
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which dialogs invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnCanceled]
/// private static void AnyCanceled() { }
/// </code>
/// <code>
/// // Any dialog
/// [OnCanceled]
/// private static void AnyCanceled(AcceptDialog dialog) { }
/// </code>
/// <code>
/// // Specific type (filters to ConfirmationDialog)
/// [OnCanceled]
/// private static void ConfirmationCanceled(ConfirmationDialog dialog) { }
/// </code>
/// <code>
/// // Targeted to specific dialog
/// [OnCanceled(Target = "UI/DeleteConfirmDialog")]
/// private static void DeleteCanceled() { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnCanceled : Attribute
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
