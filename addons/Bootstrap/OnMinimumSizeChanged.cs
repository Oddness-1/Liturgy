using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when a <see cref="Godot.Control">Control</see>'s minimum size changes.
/// </summary>
/// <remarks>
/// Parameters are optional: none, or <see cref="Godot.Control">Control</see> subtypes (filters by type).
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which controls invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnMinimumSizeChanged]
/// private static void AnyMinimumSizeChanged() { }
/// </code>
/// <code>
/// // Any control
/// [OnMinimumSizeChanged]
/// private static void AnyMinimumSizeChanged(Control c) { }
/// </code>
/// <code>
/// // Specific type (filters to Label)
/// [OnMinimumSizeChanged]
/// private static void LabelMinimumSizeChanged(Label label) { }
/// </code>
/// <code>
/// // Targeted to specific control
/// [OnMinimumSizeChanged(Target = "UI/DynamicPanel")]
/// private static void DynamicPanelMinimumSizeChanged() { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnMinimumSizeChanged : Attribute
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
