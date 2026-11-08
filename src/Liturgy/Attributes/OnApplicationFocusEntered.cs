using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when the game gains focus: its window becomes the active one, or it's restored from minimized.
    /// </summary>
    /// <remarks>
    /// Parameterless only. Fires once for the whole game, not per window: moving between the game's own windows and popups doesn't count. Supports <c>Priority</c> to control invocation order.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnApplicationFocusEntered]
    /// private static void Focused() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnApplicationFocusEntered : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
