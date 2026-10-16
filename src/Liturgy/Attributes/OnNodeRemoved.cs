using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when any non-internal node is removed from the scene tree.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or <see cref="Godot.Node">Node</see> subtypes (filters by type).
    /// Supports <c>Priority</c> property to control invocation order.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnNodeRemoved]
    /// private static void AnyNodeRemoved() { }
    /// </code>
    /// <code>
    /// // Any node
    /// [Rite.OnNodeRemoved]
    /// private static void AnyNodeRemoved(Node n) { }
    /// </code>
    /// <code>
    /// // Custom type only
    /// [Rite.OnNodeRemoved]
    /// private static void EnemyRemoved(Enemy e) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnNodeRemoved : RiteAttribute
    {
        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
