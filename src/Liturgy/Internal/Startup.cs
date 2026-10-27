using System;
using System.ComponentModel;
using System.Threading;
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
    // An int for Interlocked: attribute construction can happen on a loader thread at the same time as the main thread.
    private static int _attached;

    public static void Attach()
    {
        if (Volatile.Read(ref _attached) != 0 || !IsGodotReady()) return;

        try
        {
            if (Engine.IsEditorHint())
            {
                Volatile.Write(ref _attached, 1);
                return;
            }

            if (Engine.GetMainLoop() is not SceneTree tree) return;
            if (Interlocked.Exchange(ref _attached, 1) != 0) return;

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
            GD.PushWarning("[Liturgy] Started from a loader thread, so it joins the tree at the end of the frame, possibly after the autoloads; Immediate rites may run late.");
            root.CallDeferred(Node.MethodName.AddChild, manager);
            return;
        }

        // Before /root enters the tree this lands as its first child, ahead of every autoload.
        // If /root is busy (game code first ran mid-ready), Godot refuses and we defer. Godot doesn't expose "busy", so
        // its error is printed first; deferring every late start instead would delay Immediate rites when it isn't busy.
        root.AddChild(manager);
        if (manager.GetParent() != root) root.CallDeferred(Node.MethodName.AddChild, manager);
    }
}
