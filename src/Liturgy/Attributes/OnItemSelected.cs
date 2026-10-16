using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when an item is selected in an <see cref="Godot.ItemList">ItemList</see> or <see cref="Godot.OptionButton">OptionButton</see>.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, an <see cref="int"/> or <see cref="long"/> (index), an <c>ItemList</c> or <c>OptionButton</c>, or both in any order.
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which controls invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnItemSelected]
    /// private static void AnyItemSelected() { }
    /// </code>
    /// <code>
    /// // Index only
    /// [Rite.OnItemSelected]
    /// private static void AnyItemSelected(int index) { }
    /// </code>
    /// <code>
    /// // Source only (filters to ItemList)
    /// [Rite.OnItemSelected]
    /// private static void ItemListOnly(ItemList source) { }
    /// </code>
    /// <code>
    /// // Source only (filters to OptionButton)
    /// [Rite.OnItemSelected]
    /// private static void OptionButtonOnly(OptionButton source) { }
    /// </code>
    /// <code>
    /// // Both, any order
    /// [Rite.OnItemSelected]
    /// private static void AnyItemSelected(int index, ItemList source) { }
    /// </code>
    /// <code>
    /// // Targeted to specific control
    /// [Rite.OnItemSelected(Target = "UI/InventoryList")]
    /// private static void OnInventorySelected(int index) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnItemSelected : RiteAttribute
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
