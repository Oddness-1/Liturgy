using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when the game loses focus: another app becomes active, or the game is minimized.
    /// </summary>
    /// <remarks>
    /// Parameterless only. Fires once for the whole game, not per window: moving between the game's own windows and popups doesn't count. Supports <c>Priority</c> to control invocation order.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnApplicationFocusExited]
    /// private static void Unfocused() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnApplicationFocusExited : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
