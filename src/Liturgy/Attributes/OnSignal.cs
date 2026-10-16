using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when any node in the scene tree emits the named signal. Covers every signal the other
    /// rites don't, including your own <c>[Signal]</c> delegates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parameters are optional. The first may be the source node (any node type or interface), which also filters
    /// which nodes count. The rest are the signal's arguments: all of them or none, converted to the types you declare.
    /// An object argument whose value isn't of the declared type skips the call, so <c>Player body</c> fires for players only.
    /// </para>
    /// <para>
    /// When the signal has exactly one argument and the method one parameter, that parameter is the argument, unless
    /// it's a node type and the argument isn't an object. Take both <c>(source, argument)</c> to be explicit.
    /// </para>
    /// <para>
    /// Without a source parameter, every node that has the signal counts. Signals on objects outside the tree
    /// (a Tween, a Resource, the SceneTree itself) aren't reached.
    /// </para>
    /// <para>
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which nodes invoke this callback.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Source node, which also filters to Timers
    /// [Rite.OnSignal("timeout")]
    /// private static void AnyTimeout(Timer timer) { }
    /// </code>
    /// <code>
    /// // Source and arguments
    /// [Rite.OnSignal("value_changed", Target = "UI/Volume")]
    /// private static void VolumeChanged(Range slider, double value) { }
    /// </code>
    /// <code>
    /// // Your own signal; nameof keeps the name in step with the delegate
    /// [Rite.OnSignal(nameof(Player.SignalName.Died))]
    /// private static void PlayerDied(Player player) { }
    /// </code>
    /// <code>
    /// // Arguments only, filtered by the argument's type
    /// [Rite.OnSignal("body_entered")]
    /// private static void PlayerEntered(Player body) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public sealed class OnSignal(string signal) : RiteAttribute
    {
        /// <summary>
        /// The signal's name as Godot knows it, e.g. <c>"timeout"</c> or <c>"value_changed"</c>.
        /// </summary>
        public string Signal { get; } = signal;

        /// <summary>
        /// Comma-separated node paths. Fires only for these nodes (null = all).
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
