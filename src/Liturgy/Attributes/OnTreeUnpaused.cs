using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when the scene tree unpauses (<c>GetTree().Paused</c> becomes false).
    /// </summary>
    /// <remarks>
    /// Parameterless only. Fires once per unpause, for the whole game. Supports <c>Priority</c> to control invocation order.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnTreeUnpaused]
    /// private static void Unpaused() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnTreeUnpaused : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
