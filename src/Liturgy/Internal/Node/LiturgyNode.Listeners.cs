using System.Collections.Generic;
using Godot;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    // The connections each node's rites made, so they can be undone when it leaves the tree. A node that comes back
    // (Reparent, pooling) is subscribed afresh with its new path, rather than a second time on top of the first.
    private readonly Dictionary<ulong, List<(GodotObject Emitter, StringName Signal, Callable Callable)>> _listeners = [];

    private void Listen(Node node, StringName signal, Callable callable)
    {
        node.Connect(signal, callable);

        var id = node.GetInstanceId();
        if (!_listeners.TryGetValue(id, out var connections))
        {
            connections = [];
            _listeners[id] = connections;
        }

        connections.Add((node, signal, callable));
    }

    private void OnNodeRemovedForListeners(Node node)
    {
        if (!_listeners.Remove(node.GetInstanceId(), out var connections)) return;

        foreach (var (emitter, signal, callable) in connections)
        {
            if (IsInstanceValid(emitter) && emitter.IsConnected(signal, callable)) emitter.Disconnect(signal, callable);
        }
    }
}
