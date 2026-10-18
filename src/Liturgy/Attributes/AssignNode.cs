using System;
using JetBrains.Annotations;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Assigns a node from the scene tree to a static field or property, or to an interface property on every node
    /// that implements the interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A static member is assigned after <c>/root</c> is ready but before any <c>[Rite.OnReady]</c> callbacks
    /// in <see cref="Rite.ReadyMode.RootReady"/> mode fire, ensuring static references are available
    /// when your ready callbacks execute.
    /// </para>
    /// <para>
    /// An interface property is held by each implementing node: it's filled as the node enters the tree, so it's set
    /// by the node's own <c>_Ready</c>, and emptied when the node leaves. Every holder holds the same node, the one at
    /// the path. An instance member of a class is never assigned (LIT008); declare it on an interface instead.
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
    /// A path containing <c>*</c> is a pattern, matched like <c>Exclude</c>: <c>**</c> spans any depth, and a last
    /// segment written <c>%Name</c> needs a scene-unique name. The first match in tree order is assigned; when it
    /// leaves, the next match is.
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
    /// // A pattern: the camera in whatever scene is loaded
    /// [Rite.AssignNode("**/%Camera")]
    /// private static Camera3D Camera;
    /// </code>
    /// <code>
    /// // On an interface property: each implementing node holds its own
    /// public interface ISeesCamera
    /// {
    ///     [Rite.AssignNode("**/%Camera")]
    ///     Camera3D Camera { get; set; }
    /// }
    /// </code>
    /// <code>
    /// // Assign using unique name syntax (% prefix)
    /// [Rite.AssignNode("Headspace/%Actions")]
    /// private static ActionsController Actions;
    /// </code>
    /// </example>
    [MeansImplicitUse(ImplicitUseKindFlags.Assign | ImplicitUseKindFlags.Access)]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
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
