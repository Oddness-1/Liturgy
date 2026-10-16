using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when a <see cref="Godot.VisibleOnScreenNotifier2D">VisibleOnScreenNotifier2D</see> or <see cref="Godot.VisibleOnScreenNotifier3D">VisibleOnScreenNotifier3D</see> leaves the screen.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, or the notifier (2D or 3D, which filters to that type).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which nodes invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// [Rite.OnScreenExited]
    /// private static void LeftScreen(VisibleOnScreenNotifier2D notifier) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnScreenExited : RiteAttribute
    {
        /// <summary>
        /// Comma-separated node paths to target.
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
