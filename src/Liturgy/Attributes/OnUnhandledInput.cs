using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method for unhandled input events.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <see cref="Godot.InputEvent">InputEvent</see> subtypes (filters by event type).
    /// Supports <c>Priority</c> property to control invocation order.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnUnhandledInput]
    /// private static void HandleInput() { }
    /// </code>
    /// <code>
    /// // Any input event
    /// [Rite.OnUnhandledInput]
    /// private static void HandleInput(InputEvent e) { }
    /// </code>
    /// <code>
    /// // Keyboard events only
    /// [Rite.OnUnhandledInput]
    /// private static void HandleKeys(InputEventKey key) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnUnhandledInput : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
