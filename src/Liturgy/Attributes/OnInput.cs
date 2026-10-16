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
    /// Supports <c>Priority</c> property to control invocation order.
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
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnInput : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
