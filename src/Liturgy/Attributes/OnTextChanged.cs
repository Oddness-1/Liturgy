using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when text changes in a <see cref="Godot.LineEdit">LineEdit</see> or <see cref="Godot.TextEdit">TextEdit</see>.
    /// </summary>
    /// <remarks>
    /// <para>For <c>LineEdit</c>: receives the new text as a <see cref="string"/> parameter.</para>
    /// <para>For <c>TextEdit</c>: no text parameter (the signal doesn't provide it; use the node reference to get text).</para>
    /// <para>
    /// Parameters are optional: none, a <see cref="string"/> (for LineEdit), a <c>LineEdit</c> or <c>TextEdit</c>, or both in any order.
    /// </para>
    /// <para>
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which controls invoke this callback.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnTextChanged]
    /// private static void AnyTextChanged() { }
    /// </code>
    /// <code>
    /// // Text only (LineEdit)
    /// [Rite.OnTextChanged]
    /// private static void AnyTextChanged(string newText) { }
    /// </code>
    /// <code>
    /// // Source only (filters to LineEdit)
    /// [Rite.OnTextChanged]
    /// private static void LineEditOnly(LineEdit source) { }
    /// </code>
    /// <code>
    /// // Both, any order (LineEdit)
    /// [Rite.OnTextChanged]
    /// private static void AnyTextChanged(string newText, LineEdit source) { }
    /// </code>
    /// <code>
    /// // TextEdit (no text parameter - signal doesn't provide it)
    /// [Rite.OnTextChanged]
    /// private static void OnEditorChanged(TextEdit source) { }
    /// </code>
    /// <code>
    /// // Targeted to specific control
    /// [Rite.OnTextChanged(Target = "UI/NameInput")]
    /// private static void OnNameChanged(string newText) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnTextChanged : RiteAttribute
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
