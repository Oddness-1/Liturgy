using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when the operating system pauses the game, as Android and iOS do when it goes to the background.
    /// </summary>
    /// <remarks>
    /// Parameterless only. Desktop platforms don't send this; use <see cref="OnApplicationFocusExited"/> there. Supports <c>Priority</c> to control invocation order.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnApplicationPaused]
    /// private static void Backgrounded() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnApplicationPaused : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
