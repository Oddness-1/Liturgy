using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when an item is pressed in a <see cref="Godot.PopupMenu">PopupMenu</see>, including the menu of a <see cref="Godot.MenuButton">MenuButton</see>.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: the source (<c>PopupMenu</c> or <c>MenuButton</c>, which filters to that type) and/or an <see cref="int"/> or <see cref="long"/> (the item id), source first.
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which nodes invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnIdPressed]
    /// private static void MenuItemPressed(MenuButton menu, int id) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnIdPressed : RiteAttribute
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
