using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when any non-internal node is added to the scene tree.
/// </summary>
/// <remarks>
/// Parameters are optional: none, or <see cref="Godot.Node">Node</see> subtypes (filters by type).
/// Supports <c>Priority</c> property to control invocation order.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnNodeAdded]
/// private static void AnyNodeAdded() { }
/// </code>
/// <code>
/// // Any node
/// [OnNodeAdded]
/// private static void AnyNodeAdded(Node n) { }
/// </code>
/// <code>
/// // Controls only
/// [OnNodeAdded]
/// private static void ControlAdded(Control c) { }
/// </code>
/// <code>
/// // Custom type only
/// [OnNodeAdded]
/// private static void EnemyAdded(Enemy e) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnNodeAdded : Attribute
{
    /// <summary>
    /// Execution order (lower runs first). Default is 0.
    /// </summary>
    public int Priority { get; set; } = 0;
}
