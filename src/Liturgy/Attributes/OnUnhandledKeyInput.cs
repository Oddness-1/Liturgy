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
    /// Supports <c>Priority</c> property to control invocation order.
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
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnUnhandledKeyInput : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
