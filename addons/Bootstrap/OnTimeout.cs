using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when a <see cref="Godot.Timer">Timer</see> times out.
/// </summary>
/// <remarks>
/// Parameters are optional: none, or a <see cref="Godot.Timer">Timer</see> (the source).
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which timers invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnTimeout]
/// private static void AnyTimeout() { }
/// </code>
/// <code>
/// // Timer source
/// [OnTimeout]
/// private static void AnyTimeout(Timer timer) { }
/// </code>
/// <code>
/// // Targeted to specific timer
/// [OnTimeout(Target = "Game/SpawnTimer")]
/// private static void SpawnTimerTimeout() { }
/// </code>
/// <code>
/// // With exclusion
/// [OnTimeout(Exclude = "UI/BlinkTimer")]
/// private static void GameTimerTimeout(Timer timer) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnTimeout : Attribute
{
    /// <summary>
    /// Comma-separated node paths. Fires only for these timers (null = all).
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
