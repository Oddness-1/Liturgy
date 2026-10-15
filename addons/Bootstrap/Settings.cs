using System;

namespace Bootstrap;

/// <summary>
/// Assembly-level configuration for Bootstrap.
/// </summary>
/// <example>
/// <code>
/// [assembly: Bootstrap.Settings(Debug = true)]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class Settings : Attribute
{
    /// <summary>
    /// Enables verbose logging and report generation. Default is false.
    /// When enabled, Bootstrap will output debug messages to the console
    /// and generate timestamped markdown reports in the Logs directory.
    /// </summary>
    public bool Debug { get; set; }
}
