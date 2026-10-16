using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method every physics tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Global mode (default):</b> Fires once per physics tick.
    /// With <c>Target</c>: fires once per tick for each specified node.
    /// </para>
    /// <para>
    /// <b>PerNode mode:</b> Fires for each physics-processing node per tick.
    /// Use a typed parameter to filter by node type. <c>Target</c> is ignored in this mode.
    /// </para>
    /// <para>
    /// Parameters are optional: none, a <see cref="double"/> (delta), <see cref="Godot.Node">Node</see> subtypes, or both in any order.
    /// </para>
    /// <para>
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c>, and <c>WhilePaused</c> properties for fine-grained control.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Global mode - no parameters
    /// [Rite.OnPhysicsProcess]
    /// private static void PhysicsUpdate() { }
    /// </code>
    /// <code>
    /// // Global mode - delta only
    /// [Rite.OnPhysicsProcess]
    /// private static void PhysicsUpdate(double delta) { }
    /// </code>
    /// <code>
    /// // Global mode - keeps firing while the tree is paused
    /// [Rite.OnPhysicsProcess(WhilePaused = true)]
    /// private static void PhysicsClock(double delta) { }
    /// </code>
    /// <code>
    /// // Global mode with target
    /// [Rite.OnPhysicsProcess(Target = "Main/Player")]
    /// private static void UpdatePlayer(double delta, CharacterBody3D player) { }
    /// </code>
    /// <code>
    /// // PerNode mode - fires for each RigidBody3D
    /// // WARNING: Creates a helper node for each matching node!
    /// [Rite.OnPhysicsProcess(Mode = Rite.FrameMode.PerNode)]
    /// private static void UpdateBodies(double delta, RigidBody3D body) { }
    /// </code>
    /// <code>
    /// // DANGEROUS: No type filter creates a helper node on EVERY physics processing node!
    /// [Rite.OnPhysicsProcess(Mode = Rite.FrameMode.PerNode)]
    /// private static void UpdateEverything(double delta, Node n) { }
    /// </code>
    /// <code>
    /// // ALSO DANGEROUS: No parameters will still create a helper node on EVERY physics processing node!
    /// [Rite.OnPhysicsProcess(Mode = Rite.FrameMode.PerNode)]
    /// private static void UpdateEverything(double delta) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnPhysicsProcess : RiteAttribute
    {
        /// <summary>
        /// Determines when the callback fires. Default is Global (fires once per tick).
        /// <para>Target is ignored when Mode is PerNode.</para>
        /// </summary>
        public FrameMode Mode { get; set; } = FrameMode.Global;

        /// <summary>
        /// Comma-separated node paths. Fires for each specified node with proper tree timing.
        /// Only used in Global mode; ignored in PerNode mode.
        /// </summary>
        public string? Target { get; set; }

        /// <summary>
        /// Comma-separated node paths to skip.
        /// </summary>
        public string? Exclude { get; set; }

        /// <summary>
        /// Execution order (lower runs first). Default is 0.
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// If true, the callback keeps firing while the scene tree is paused. Only applies in Global mode
        /// without a <c>Target</c>; ignored (with a warning) otherwise. These callbacks run at the start of
        /// each physics tick, before the pausable ones, so <c>Priority</c> orders them only among themselves.
        /// </summary>
        public bool WhilePaused { get; set; }
    }
}
