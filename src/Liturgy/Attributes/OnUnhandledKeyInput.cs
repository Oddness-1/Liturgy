using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method for unhandled key input events.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <see cref="Godot.InputEvent">InputEvent</see> subtypes (filters by event type).
    /// Supports <c>Priority</c> property to control invocation order, and <c>WhilePaused</c> to keep it running while the tree is paused.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnUnhandledKeyInput]
    /// private static void HandleKey() { }
    /// </code>
    /// <code>
    /// // Any key event
    /// [Rite.OnUnhandledKeyInput]
    /// private static void HandleKey(InputEventKey key) { }
    /// </code>
    /// <code>
    /// // Keeps running while the tree is paused
    /// [Rite.OnUnhandledKeyInput(WhilePaused = true)]
    /// private static void ClosePauseMenu(InputEventKey key) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnUnhandledKeyInput : RiteAttribute
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
