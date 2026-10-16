using System;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// The group that hides a node, and everything under it, from every rite. Add a scene's root to it when that scene
    /// should behave exactly as written, wherever it's used.
    /// </summary>
    /// <example>
    /// <code>
    /// AddToGroup(Rite.IGNORE_GROUP);
    /// </code>
    /// </example>
    public const string IGNORE_GROUP = "liturgy_ignore";

    /// <summary>
    /// Assembly-level configuration for Liturgy.
    /// </summary>
    /// <example>
    /// <code>
    /// [assembly: Rite.Settings(Debug = true)]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class Settings : Attribute
    {
        /// <summary>
        /// Enables verbose logging and report generation. Default is false.
        /// When enabled and running from the editor, Liturgy will output debug messages to the console
        /// and generate timestamped markdown reports in <c>user://Liturgy/Logs</c>. Exported builds ignore it.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Whether the harness, Liturgy's in-game overlay of every rite, is available on Alt+~ when the game runs from
        /// the editor, along with the counting behind it and <see cref="Rite.Diagnostics"/>. Default is true. Exported
        /// builds never include either. To leave it on but profile one run without it, launch with
        /// <c>--liturgy-quiet</c> (Project Settings > Editor > Run > Main Run Args).
        /// </summary>
        public bool Harness { get; set; } = true;

        /// <summary>
        /// Pauses the game while the harness is open, so code that polls <c>Input</c> every frame doesn't react to
        /// keys meant for the harness. Default is false.
        /// </summary>
        public bool HarnessPausesGame { get; set; }
    }
}
