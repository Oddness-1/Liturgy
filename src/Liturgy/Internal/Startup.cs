using System;
using System.ComponentModel;
using Godot;

namespace Liturgy.Internal;

/// <summary>
/// Attaches Liturgy to the scene tree. Called by the module initializer Liturgy's source generator
/// emits into every assembly that references it; not intended for direct use.
/// </summary>
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

            if (Engine.GetMainLoop() is not SceneTree tree)
            {
                GD.PushWarning("[Liturgy] The main loop isn't a SceneTree, so Liturgy can't attach and no hooks will run.");
                return;
            }

            var root = tree.Root;
            var manager = new LiturgyNode { Name = LiturgyNode.NODE_NAME };

            // At module initialization /root has not entered the tree yet, so this lands as its first child,
            // ahead of every autoload. If /root is busy (game code first ran mid-ready), Godot refuses and we defer.
            root.AddChild(manager);
            if (manager.GetParent() != root) root.CallDeferred(Node.MethodName.AddChild, manager);
        }
        catch (Exception exception)
        {
            GD.PushError($"[Liturgy] Failed to attach: {exception}");
        }
    }
}
