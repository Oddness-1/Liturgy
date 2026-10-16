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
    /// The member follows its path for the rest of the game. When its node leaves the tree, the member is set to null;
    /// when a node arrives at the path again, such as after a scene change, it's assigned that one. If no node is at
    /// the path when <c>/root</c> is ready, a warning is logged and the member stays null until one arrives.
    /// </para>
    /// <para>
    /// The member's type can be a node type or an interface; the node found must be of that type or implement that
    /// interface, or the member isn't assigned.
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
    /// // Assign by interface: any node at the path that implements it
    /// [Rite.AssignNode("/root/Music")]
    /// private static IMusicPlayer Music;
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
