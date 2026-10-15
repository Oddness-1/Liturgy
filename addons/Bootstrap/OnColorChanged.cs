using System;
// ReSharper disable GrammarMistakeInComment

namespace Bootstrap;

/// <summary>
/// Invokes a static method when a color is changed in a <see cref="Godot.ColorPicker">ColorPicker</see> or <see cref="Godot.ColorPickerButton">ColorPickerButton</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, a <see cref="Godot.Color">Color</see>, a <c>ColorPicker</c> or <c>ColorPickerButton</c>, or both in any order.
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which controls invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnColorChanged]
/// private static void AnyColorChanged() { }
/// </code>
/// <code>
/// // Color only
/// [OnColorChanged]
/// private static void AnyColorChanged(Color color) { }
/// </code>
/// <code>
/// // Source only (filters to ColorPickerButton)
/// [OnColorChanged]
/// private static void ButtonColorChanged(ColorPickerButton source) { }
/// </code>
/// <code>
/// // Both, any order
/// [OnColorChanged(Target = "UI/BackgroundColorPicker")]
/// private static void OnBackgroundColorChanged(Color color, ColorPicker source) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnColorChanged : Attribute
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
