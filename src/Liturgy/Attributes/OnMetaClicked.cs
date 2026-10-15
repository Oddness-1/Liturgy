using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when a <c>[url]</c> or other meta tag is clicked in a <see cref="Godot.RichTextLabel">RichTextLabel</see>.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: the <c>RichTextLabel</c> and/or the meta value (<see cref="Godot.Variant">Variant</see> or <see cref="string"/>), source first.
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which nodes invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnMetaClicked]
    /// private static void LinkClicked(RichTextLabel label, string meta) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnMetaClicked : Attribute
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
        public int Priority { get; set; }
    }
}
