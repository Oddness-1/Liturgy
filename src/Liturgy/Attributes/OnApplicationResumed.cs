using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when the operating system resumes the game after pausing it, as Android and iOS do when it returns to the foreground.
    /// </summary>
    /// <remarks>
    /// Parameterless only. Desktop platforms don't send this; use <see cref="OnApplicationFocusEntered"/> there. Supports <c>Priority</c> to control invocation order.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnApplicationResumed]
    /// private static void Foregrounded() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnApplicationResumed : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
