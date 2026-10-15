using System;

namespace Bootstrap;

/// <summary>
/// Assigns a node from the scene tree to a static field or property when <c>/root</c> is ready.
/// </summary>
/// <remarks>
/// <para>
/// The assignment occurs after <c>/root</c> is ready but before any <c>[OnReady]</c> callbacks
/// in <see cref="ReadyMode.RootReady"/> mode fire, ensuring static references are available
/// when your ready callbacks execute.
/// </para>
/// <para>
/// If the node is not found at the specified path, a warning is logged and the member is not assigned.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Assign an autoload by absolute path
/// [AssignNode("/root/GameManager")]
/// private static GameManager GM;
/// </code>
/// <code>
/// // Assign a node using a relative path (relative to /root/)
/// [AssignNode("Main/Player")]
/// private static Player Player;
/// </code>
/// <code>
/// // Assign to a property
/// [AssignNode("/root/AudioManager")]
/// private static AudioManager Audio { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class AssignNode : Attribute
{
    /// <summary>
    /// The path to the node in the scene tree.
    /// Absolute paths start with '/'. Relative paths are resolved from '/root/'.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Creates an AssignNode attribute with the specified path.
    /// </summary>
    /// <param name="path">
    /// The path to the node. Absolute paths start with '/'.
    /// Relative paths are resolved from '/root/'.
    /// </param>
    public AssignNode(string path)
    {
        Path = path;
    }
}
