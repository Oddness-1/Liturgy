using System;
using System.ComponentModel;
using Godot;

namespace Liturgy.Internal;

/// <summary>
/// Attaches Liturgy to the scene tree. Called by <see cref="AttachAttribute"/>; not intended for direct use.
/// </summary>
/// <remarks>
/// Godot builds a script class's attributes at two points. First while it scans the game assembly, before its C# API
/// can be used in an exported game and before any scene tree exists; Liturgy ignores those. Then each time it loads a
/// script. The first load after the scene tree exists is before /root enters the tree and before any C# node is
/// built, so Liturgy adds itself to /root there and is ready before the autoloads and the main scene.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class Startup
{
    private static bool _attached;

    public static void Attach()
    {
        if (_attached || !IsGodotReady()) return;

        try
        {
            if (Engine.IsEditorHint())
            {
                _attached = true;
                return;
            }

            if (Engine.GetMainLoop() is not SceneTree tree) return;

            _attached = true;
            AddToRoot(tree.Root);
        }
        catch (Exception exception)
        {
            GD.PushError($"[Liturgy] Failed to attach: {exception}");
        }
    }

    // Godot sets up its task scheduler only once its C# API is fully wired; before that, touching any Godot object
    // crashes the game. The scheduler's context throws until then, which makes it a safe test.
    private static bool IsGodotReady()
    {
        try { return Dispatcher.SynchronizationContext is not null; }
        catch (NullReferenceException) { return false; }
    }

    private static void AddToRoot(Window root)
    {
        var manager = new LiturgyNode { Name = LiturgyNode.NODE_NAME };

        // A script loaded on a loader thread can get here first; the tree only takes children on the main thread.
        if (OS.GetThreadCallerId() != OS.GetMainThreadId())
        {
            root.CallDeferred(Node.MethodName.AddChild, manager);
            return;
        }

        // Before /root enters the tree this lands as its first child, ahead of every autoload.
        // If /root is busy (game code first ran mid-ready), Godot refuses and we defer.
        root.AddChild(manager);
        if (manager.GetParent() != root) root.CallDeferred(Node.MethodName.AddChild, manager);
    }
}
