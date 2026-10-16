using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when the scene tree structure changes.
    /// </summary>
    /// <remarks>
    /// Parameterless only. Fires on any add, remove, or reparent operation. Use sparingly—fires frequently.
    /// Supports <c>Priority</c> property to control invocation order.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnTreeChanged]
    /// private static void TreeChanged() { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnTreeChanged : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
