using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when a directory is selected in a <see cref="Godot.FileDialog">FileDialog</see>.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, a <see cref="string"/> (path), a <see cref="Godot.FileDialog">FileDialog</see>, or both in any order.
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which dialogs invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnDirSelected]
    /// private static void AnyDirSelected() { }
    /// </code>
    /// <code>
    /// // Path only
    /// [Rite.OnDirSelected]
    /// private static void AnyDirSelected(string path) { }
    /// </code>
    /// <code>
    /// // Source only
    /// [Rite.OnDirSelected]
    /// private static void AnyDirSelected(FileDialog source) { }
    /// </code>
    /// <code>
    /// // Both, any order
    /// [Rite.OnDirSelected]
    /// private static void AnyDirSelected(string path, FileDialog source) { }
    /// </code>
    /// <code>
    /// // Targeted to specific dialog
    /// [Rite.OnDirSelected(Target = "UI/FolderBrowser")]
    /// private static void OnFolderSelected(string path) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnDirSelected : RiteAttribute
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
}
