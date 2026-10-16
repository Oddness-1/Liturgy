using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when an item is activated (double-clicked or Enter pressed) in an <see cref="Godot.ItemList">ItemList</see>.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, an <see cref="int"/> (index), an <see cref="Godot.ItemList">ItemList</see>, or both in any order.
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which lists invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnItemActivated]
    /// private static void AnyItemActivated() { }
    /// </code>
    /// <code>
    /// // Index only
    /// [Rite.OnItemActivated]
    /// private static void AnyItemActivated(int index) { }
    /// </code>
    /// <code>
    /// // Source only
    /// [Rite.OnItemActivated]
    /// private static void AnyItemActivated(ItemList source) { }
    /// </code>
    /// <code>
    /// // Both, any order
    /// [Rite.OnItemActivated]
    /// private static void AnyItemActivated(int index, ItemList source) { }
    /// </code>
    /// <code>
    /// // Targeted to specific list
    /// [Rite.OnItemActivated(Target = "UI/FileList")]
    /// private static void OnFileActivated(int index) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnItemActivated : RiteAttribute
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
