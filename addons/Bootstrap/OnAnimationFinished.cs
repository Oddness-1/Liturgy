using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when an animation finishes in an <see cref="Godot.AnimationPlayer">AnimationPlayer</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, a <see cref="Godot.StringName">StringName</see> (animation name), an <see cref="Godot.AnimationPlayer">AnimationPlayer</see>, or both in any order.
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which players invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnAnimationFinished]
/// private static void AnyAnimationFinished() { }
/// </code>
/// <code>
/// // Animation name only
/// [OnAnimationFinished]
/// private static void AnyAnimationFinished(StringName animName) { }
/// </code>
/// <code>
/// // Source only
/// [OnAnimationFinished]
/// private static void AnyAnimationFinished(AnimationPlayer player) { }
/// </code>
/// <code>
/// // Both, any order
/// [OnAnimationFinished]
/// private static void AnyAnimationFinished(StringName animName, AnimationPlayer player) { }
/// </code>
/// <code>
/// // Targeted to specific player
/// [OnAnimationFinished(Target = "Player/AnimationPlayer")]
/// private static void PlayerAnimationFinished(StringName animName) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnAnimationFinished : Attribute
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
