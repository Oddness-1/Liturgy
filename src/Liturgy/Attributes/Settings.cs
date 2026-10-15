using System;

namespace Liturgy;

public static partial class Rite
{
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
    }
}
