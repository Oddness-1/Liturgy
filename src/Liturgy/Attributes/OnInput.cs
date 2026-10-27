using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method for every input event.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <see cref="Godot.InputEvent">InputEvent</see> subtypes (filters by event type).
    /// Supports <c>Priority</c> property to control invocation order, and <c>WhilePaused</c> to keep it running while the tree is paused.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnInput]
    /// private static void HandleInput() { }
    /// </code>
    /// <code>
    /// // Any input event
    /// [Rite.OnInput]
    /// private static void HandleInput(InputEvent e) { }
    /// </code>
    /// <code>
    /// // Keyboard events only
    /// [Rite.OnInput]
    /// private static void HandleKeys(InputEventKey key) { }
    /// </code>
    /// <code>
    /// // Mouse button events only
    /// [Rite.OnInput]
    /// private static void HandleMouseButtons(InputEventMouseButton mb) { }
    /// </code>
    /// <code>
    /// // Keeps running while the tree is paused
    /// [Rite.OnInput(WhilePaused = true)]
    /// private static void ReleaseFocus(InputEventMouseButton click) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnInput : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// If true, the callback still runs while the scene tree is paused. Without it, input rites pause with the
        /// game, as a pausable node's input does. On an interface member it's ignored: the node's own process mode decides.
        /// </summary>
        public bool WhilePaused { get; set; }
    }
}
