using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when any <see cref="Godot.BaseButton">BaseButton</see>'s toggle state changes.
/// </summary>
/// <remarks>
/// Parameters are optional: none, a <see cref="bool"/> (pressed state), <c>BaseButton</c> subtypes (source), or both in any order.
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which buttons invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnButtonToggled]
/// private static void AnyToggle() { }
/// </code>
/// <code>
/// // Bool only
/// [OnButtonToggled]
/// private static void AnyToggle(bool pressed) { }
/// </code>
/// <code>
/// // Source only (filters to CheckBox)
/// [OnButtonToggled]
/// private static void CheckBoxOnly(CheckBox cb) { }
/// </code>
/// <code>
/// // Both, any order
/// [OnButtonToggled]
/// private static void AnyToggle(bool pressed, BaseButton b) { }
/// </code>
/// <code>
/// // Targeted to specific node
/// [OnButtonToggled(Target = "UI/MuteToggle")]
/// private static void MuteChanged(bool pressed) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnButtonToggled : Attribute
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
