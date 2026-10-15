using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when audio playback finishes in an <see cref="Godot.AudioStreamPlayer">AudioStreamPlayer</see>,
/// <see cref="Godot.AudioStreamPlayer2D">AudioStreamPlayer2D</see>, or <see cref="Godot.AudioStreamPlayer3D">AudioStreamPlayer3D</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, or an audio player type (filters by type).
/// Use typed parameters to filter by dimension (non-positional, 2D, or 3D).
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which players invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnAudioFinished]
/// private static void AnyAudioFinished() { }
/// </code>
/// <code>
/// // Non-positional audio
/// [OnAudioFinished]
/// private static void AudioFinished(AudioStreamPlayer player) { }
/// </code>
/// <code>
/// // 2D audio
/// [OnAudioFinished]
/// private static void Audio2DFinished(AudioStreamPlayer2D player) { }
/// </code>
/// <code>
/// // 3D audio
/// [OnAudioFinished]
/// private static void Audio3DFinished(AudioStreamPlayer3D player) { }
/// </code>
/// <code>
/// // Targeted to specific player
/// [OnAudioFinished(Target = "Audio/MusicPlayer")]
/// private static void MusicFinished() { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnAudioFinished : Attribute
{
    /// <summary>
    /// Comma-separated node paths. Fires only for these audio players (null = all).
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
