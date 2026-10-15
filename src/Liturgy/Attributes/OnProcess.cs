using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method every frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Global mode (default):</b> Fires once per frame.
    /// With <c>Target</c>: fires once per frame for each specified node.
    /// </para>
    /// <para>
    /// <b>PerNode mode:</b> Fires for each processing node per frame.
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
    /// [Rite.OnProcess]
    /// private static void Update() { }
    /// </code>
    /// <code>
    /// // Global mode - delta only
    /// [Rite.OnProcess]
    /// private static void Update(double delta) { }
    /// </code>
    /// <code>
    /// // Global mode - keeps firing while the tree is paused
    /// [Rite.OnProcess(WhilePaused = true)]
    /// private static void AdvanceClock(double delta) { }
    /// </code>
    /// <code>
    /// // Global mode with target
    /// [Rite.OnProcess(Target = "Main/Player")]
    /// private static void UpdatePlayer(double delta, CharacterBody3D player) { }
    /// </code>
    /// <code>
    /// // PerNode mode - fires for each Enemy
    /// // WARNING: Creates a helper node for each matching node!
    /// [Rite.OnProcess(Mode = Rite.FrameMode.PerNode)]
    /// private static void UpdateEnemies(double delta, Enemy e) { }
    /// </code>
    /// <code>
    /// // DANGEROUS: No type filter creates a helper node on EVERY processing node!
    /// [Rite.OnProcess(Mode = Rite.FrameMode.PerNode)]
    /// private static void UpdateEverything(double delta, Node n) { }
    /// </code>
    /// <code>
    /// // ALSO DANGEROUS: No parameters will still create a helper node on EVERY physics processing node!
    /// [Rite.OnProcess(Mode = Rite.FrameMode.PerNode)]
    /// private static void UpdateEverything(double delta) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnProcess : Attribute
    {
        /// <summary>
        /// Determines when the callback fires. Default is Global (fires once per frame).
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
        /// each frame, before the pausable ones, so <c>Priority</c> orders them only among themselves.
        /// </summary>
        public bool WhilePaused { get; set; }
    }
}
