using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when nodes become ready.
/// </summary>
/// <remarks>
/// <para>
/// <b>RootReady mode (default):</b> Fires once when <c>/root</c> is ready.
/// With <c>Target</c>: fires once when each specified node is ready.
/// </para>
/// <para>
/// <b>Immediate mode:</b> Fires immediately during Bootstrap initialization,
/// before <c>/root</c> is ready. Use when you need to set up state before the scene tree is fully constructed.
/// </para>
/// <para>
/// <b>PerNode mode:</b> Fires each time any node becomes ready.
/// Use a typed parameter to filter by node type. <c>Target</c> is ignored in this mode.
/// </para>
/// <para>
/// Parameters are optional: none, or <see cref="Godot.Node">Node</see> subtypes (filters by type).
/// </para>
/// <para>
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // RootReady mode - no parameters, fires once when /root is ready
/// [OnReady]
/// private static void GameStart() { }
/// </code>
/// <code>
/// // RootReady mode - fires once when Player is ready
/// [OnReady(Target = "Main/Player")]
/// private static void PlayerReady(CharacterBody3D player) { }
/// </code>
/// <code>
/// // RootReady mode - fires once for each specified node
/// [OnReady(Target = "UI/Btn1, UI/Btn2")]
/// private static void ButtonReady(Button btn) { }
/// </code>
/// <code>
/// // Immediate mode - fires before /root is ready
/// [OnReady(Mode = ReadyMode.Immediate)]
/// private static void EarlySetup() { }
/// </code>
/// <code>
/// // PerNode mode - fires for EVERY node as it becomes ready
/// [OnReady(Mode = ReadyMode.PerNode)]
/// private static void AnyNodeReady(Node n) { }
/// </code>
/// <code>
/// // PerNode mode - fires for each Enemy as it becomes ready
/// [OnReady(Mode = ReadyMode.PerNode)]
/// private static void EnemyReady(Enemy e) { }
/// </code>
/// <code>
/// // PerNode mode with exclusion
/// [OnReady(Mode = ReadyMode.PerNode, Exclude = "UI")]
/// private static void GameNodeReady(Node n) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnReady : Attribute
{
    /// <summary>
    /// Determines when the callback fires. Default is RootReady (fires once when /root is ready).
    /// <para>Target is ignored when Mode is PerNode.</para>
    /// </summary>
    public ReadyMode Mode { get; set; } = ReadyMode.RootReady;

    /// <summary>
    /// Comma-separated node paths. Fires once when each specified node is ready.
    /// Only used in RootReady mode; ignored in PerNode mode.
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
}
