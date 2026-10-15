using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Assigns a node from the scene tree to a static field or property when <c>/root</c> is ready.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The assignment occurs after <c>/root</c> is ready but before any <c>[Rite.OnReady]</c> callbacks
    /// in <see cref="Rite.ReadyMode.RootReady"/> mode fire, ensuring static references are available
    /// when your ready callbacks execute.
    /// </para>
    /// <para>
    /// If the node is not found at the specified path, a warning is logged and the member is not assigned.
    /// </para>
    /// <para>
    /// Supports unique name syntax (%) for nodes with scene-unique names. Use the format
    /// <c>"ScenePath/%UniqueName"</c> where ScenePath is resolved from /root/ and the unique
    /// name is looked up from that context.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Assign an autoload by absolute path
    /// [Rite.AssignNode("/root/GameManager")]
    /// private static GameManager GM;
    /// </code>
    /// <code>
    /// // Assign a node using a relative path (relative to /root/)
    /// [Rite.AssignNode("Main/Player")]
    /// private static Player Player;
    /// </code>
    /// <code>
    /// // Assign to a property
    /// [Rite.AssignNode("/root/AudioManager")]
    /// private static AudioManager Audio { get; set; }
    /// </code>
    /// <code>
    /// // Assign using unique name syntax (% prefix)
    /// [Rite.AssignNode("Headspace/%Actions")]
    /// private static ActionsController Actions;
    /// </code>
    /// </example>
    [MeansImplicitUse(ImplicitUseKindFlags.Assign | ImplicitUseKindFlags.Access)]
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
}
