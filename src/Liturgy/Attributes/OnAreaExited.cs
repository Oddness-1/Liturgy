using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Invokes a static method when an area exits an <see cref="Godot.Area2D">Area2D</see> or <see cref="Godot.Area3D">Area3D</see>.
    /// </summary>
    /// <remarks>
    /// Parameters are optional: none, an <c>Area2D</c> or <c>Area3D</c> (the exiting area), the source area, or both in any order.
    /// Use typed parameters to filter by dimension (2D vs 3D).
    /// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which areas invoke this callback.
    /// </remarks>
    /// <example>
    /// <code>
    /// // No parameters
    /// [Rite.OnAreaExited]
    /// private static void AnyAreaExited() { }
    /// </code>
    /// <code>
    /// // Exiting area only (2D)
    /// [Rite.OnAreaExited]
    /// private static void AreaExited2D(Area2D area) { }
    /// </code>
    /// <code>
    /// // Exiting area only (3D)
    /// [Rite.OnAreaExited]
    /// private static void AreaExited3D(Area3D area) { }
    /// </code>
    /// <code>
    /// // Both areas (source filters to Area2D)
    /// [Rite.OnAreaExited]
    /// private static void AreaExited(Area2D exiting, Area2D source) { }
    /// </code>
    /// <code>
    /// // Targeted to specific area
    /// [Rite.OnAreaExited(Target = "Level/TriggerZone")]
    /// private static void TriggerZoneAreaExited(Area2D exiting) { }
    /// </code>
    /// </example>
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class OnAreaExited : RiteAttribute
    {
        /// <summary>
        /// Comma-separated node paths. Fires only for these areas (null = all).
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
