using System;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Suppresses Liturgy warnings for methods that intentionally use patterns
    /// which may impact performance at scale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Apply this attribute to acknowledge that you understand the performance implications
    /// of patterns like PerNode OnProcess/OnPhysicsProcess without type filters.
    /// </para>
    /// <para>
    /// Without this attribute, Liturgy will print a one-time warning to the console
    /// when such patterns are detected.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // This would normally warn about running on every node every frame
    /// [Rite.OnProcess(Mode = Rite.FrameMode.PerNode)]
    /// [Rite.SuppressWarnings]
    /// public static void UpdateAllNodes(Node node, double delta) { }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class SuppressWarnings : Attribute;
}
