using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method as the game shuts down, after every other node has left the tree.
    /// </summary>
    /// <remarks>
    /// Parameterless only. Runs last: the scene and the autoloads are already gone, so use it for static state (saving settings, flushing a log), not for reading nodes. Fires however the game ends: the close button, <c>GetTree().Quit()</c>, or anything else that tears the tree down. Supports <c>Priority</c> to control invocation order.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnApplicationQuit]
    /// private static void SaveSettings() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnApplicationQuit : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
