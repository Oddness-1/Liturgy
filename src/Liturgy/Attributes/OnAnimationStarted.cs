using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when an animation starts in an <see cref="Godot.AnimationPlayer">AnimationPlayer</see>.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, a <see cref="Godot.StringName">StringName</see> (animation name), an <see cref="Godot.AnimationPlayer">AnimationPlayer</see>, or both in any order.
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which players invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnAnimationStarted]
    /// private static void AnyAnimationStarted() { }
    /// </code>
    /// <code>
    /// // Animation name only
    /// [Rite.OnAnimationStarted]
    /// private static void AnyAnimationStarted(StringName animName) { }
    /// </code>
    /// <code>
    /// // Source only
    /// [Rite.OnAnimationStarted]
    /// private static void AnyAnimationStarted(AnimationPlayer player) { }
    /// </code>
    /// <code>
    /// // Both, any order
    /// [Rite.OnAnimationStarted]
    /// private static void AnyAnimationStarted(StringName animName, AnimationPlayer player) { }
    /// </code>
    /// <code>
    /// // Targeted to specific player
    /// [Rite.OnAnimationStarted(Target = "Player/AnimationPlayer")]
    /// private static void PlayerAnimationStarted(StringName animName) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnAnimationStarted : RiteAttribute
    {
        /// <summary>
        /// Comma-separated node paths. Fires only for these animation players (null = all).
        /// </summary>
        public string? Target { get; set; }

        /// <summary>
        /// Comma-separated node paths to skip.
        /// </summary>
        public string? Exclude { get; set; }

        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; }
    }
}
