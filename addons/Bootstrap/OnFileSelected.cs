using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when a file is selected in a <see cref="Godot.FileDialog">FileDialog</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, a <see cref="string"/> (path), a <see cref="Godot.FileDialog">FileDialog</see>, or both in any order.
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which dialogs invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnFileSelected]
/// private static void AnyFileSelected() { }
/// </code>
/// <code>
/// // Path only
/// [OnFileSelected]
/// private static void AnyFileSelected(string path) { }
/// </code>
/// <code>
/// // Source only
/// [OnFileSelected]
/// private static void AnyFileSelected(FileDialog source) { }
/// </code>
/// <code>
/// // Both, any order
/// [OnFileSelected]
/// private static void AnyFileSelected(string path, FileDialog source) { }
/// </code>
/// <code>
/// // Targeted to specific dialog
/// [OnFileSelected(Target = "UI/SaveDialog")]
/// private static void OnSaveFileSelected(string path) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnFileSelected : Attribute
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
