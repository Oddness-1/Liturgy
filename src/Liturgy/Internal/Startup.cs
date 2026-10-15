using System;
using System.ComponentModel;
using Godot;

namespace Liturgy.Internal;

/// <summary>
/// Attaches Liturgy to the scene tree. Called by the module initializer Liturgy's source generator
/// emits into every assembly that references it; not intended for direct use.
/// </summary>
/// <remarks>
/// The module initializer runs at one of two points. If the game assembly defines attributes on its script classes,
/// Godot constructs them while scanning the assembly, before any SceneTree exists; Liturgy then registers itself as
/// an autoload, in memory only, and Godot builds it with the others. Otherwise it runs when Godot builds the first
/// C# node, while /root is still outside the tree, and Liturgy adds itself to /root directly.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class Startup
{
    private static bool _attached;

    public static void Attach()
    {
        if (_attached) return;
        _attached = true;

        try
        {
            if (Engine.IsEditorHint()) return;

            switch (Engine.GetMainLoop())
            {
                case null:
                    LiturgyLoader.Register();
                    ProjectSettings.SetSetting($"autoload/{LiturgyNode.NODE_NAME}", LiturgyLoader.PATH);
                    return;
                case SceneTree tree:
                    AddToRoot(tree.Root);
                    return;
                default:
                    GD.PushWarning("[Liturgy] The main loop isn't a SceneTree, so Liturgy can't attach and no hooks will run.");
                    return;
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"[Liturgy] Failed to attach: {exception}");
        }
    }

    private static void AddToRoot(Window root)
    {
        var manager = new LiturgyNode { Name = LiturgyNode.NODE_NAME };

        // Before /root enters the tree this lands as its first child, ahead of every autoload.
        // If /root is busy (game code first ran mid-ready), Godot refuses and we defer.
        root.AddChild(manager);
        if (manager.GetParent() != root) root.CallDeferred(Node.MethodName.AddChild, manager);
    }
}
