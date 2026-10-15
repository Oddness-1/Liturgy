using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when text is submitted (Enter pressed) in a <see cref="Godot.LineEdit">LineEdit</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, a <see cref="string"/> (text), a <see cref="Godot.LineEdit">LineEdit</see>, or both in any order.
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which controls invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnTextSubmitted]
/// private static void AnySubmit() { }
/// </code>
/// <code>
/// // Text only
/// [OnTextSubmitted]
/// private static void AnySubmit(string text) { }
/// </code>
/// <code>
/// // Source only
/// [OnTextSubmitted]
/// private static void AnySubmit(LineEdit source) { }
/// </code>
/// <code>
/// // Both, any order
/// [OnTextSubmitted]
/// private static void AnySubmit(string text, LineEdit source) { }
/// </code>
/// <code>
/// // Targeted to specific control
/// [OnTextSubmitted(Target = "UI/SearchBox")]
/// private static void OnSearch(string text) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnTextSubmitted : Attribute
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
