using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    /// <summary>
    /// Rites that are one signal on one family of nodes, run through the same machinery as <c>[Rite.OnSignal]</c>.
    /// </summary>
    private static readonly (Type Attribute, string Signal, Type[] Sources, Func<Node, GodotObject?>? Emitter)[] SIGNAL_RITES =
    [
        (typeof(OnIdPressed), "id_pressed", [typeof(PopupMenu), typeof(MenuButton)], node => node is MenuButton menuButton ? menuButton.GetPopup() : node),
        (typeof(OnMetaClicked), "meta_clicked", [typeof(RichTextLabel)], null),
        (typeof(OnScreenEntered), "screen_entered", [typeof(VisibleOnScreenNotifier2D), typeof(VisibleOnScreenNotifier3D)], null),
        (typeof(OnScreenExited), "screen_exited", [typeof(VisibleOnScreenNotifier2D), typeof(VisibleOnScreenNotifier3D)], null)
    ];

    private const int MAX_SIGNAL_ARGUMENTS = 9;

    private readonly List<SignalRite> _signalRites = [];
    private readonly Dictionary<ulong, List<SignalConnection>> _signalConnections = [];
    private readonly HashSet<(SignalRite Rite, Type NodeType)> _missingSignalReported = [];
    private readonly List<ulong> _pendingDetach = [];

    private readonly record struct SignalConnection(ulong EmitterId, StringName Signal, Callable Callable);

    private void SetupSignalCallbacks()
    {
        var rites = new List<SignalRite>();

        foreach (var method in HookMethods)
        {
            foreach (var attr in method.GetCustomAttributes<OnSignal>())
            {
                if (string.IsNullOrWhiteSpace(attr.Signal))
                {
                    LiturgyReport.AddMethodError($"{method.DeclaringType?.FullName}.{method.Name}", "OnSignal", "Needs a signal name.");
                    continue;
                }

                rites.Add(CreateSignalRite(method, "OnSignal", attr.Signal, [typeof(Node)], null, attr.Target, attr.Exclude, attr.Priority));
            }

            foreach (var (attributeType, signal, sources, emitter) in SIGNAL_RITES)
            {
                if (method.GetCustomAttribute(attributeType) is not { } attr) continue;

                var (target, exclude, priority) = ReadRiteFilters(attr);
                rites.Add(CreateSignalRite(method, attributeType.Name, signal, sources, emitter, target, exclude, priority));
            }
        }

        _signalRites.AddRange(rites.OrderBy(rite => rite.Priority));

        SetupInterfaceRites();

        if (_signalRites.Count == 0 && !HasInterfaceRites) return;

        AttachToExisting(_root);
        GetTree().NodeAdded += OnNodeAddedForRites;
        GetTree().NodeRemoved += OnNodeRemovedForRites;
    }

    private static SignalRite CreateSignalRite(MethodInfo method, string hookName, string signal, Type[] sources, Func<Node, GodotObject?>? emitter, string? target, string? exclude, int priority)
    {
        var rite = new SignalRite
        {
            HookName = hookName,
            Method = method,
            Signal = signal,
            SourceTypes = sources,
            Emitter = emitter,
            Targets = ParseTargetSet(target),
            Excludes = ParseExcludeSet(exclude),
            Priority = priority
        };

        RiteProbe.MarkRunning(method);

        var parameters = method.GetParameters();
        var typeFilter = parameters.Length > 0 && (typeof(Node).IsAssignableFrom(parameters[0].ParameterType) || parameters[0].ParameterType.IsInterface) ? parameters[0].ParameterType.Name : null;
        LiturgyReport.AddControlHook(hookName == "OnSignal" ? $"OnSignal(\"{signal}\")" : hookName, rite.MethodName, target, exclude, priority, typeFilter);

        return rite;
    }

    /// <summary>
    /// Target, Exclude and Priority, which every node rite declares.
    /// </summary>
    private static (string? Target, string? Exclude, int Priority) ReadRiteFilters(Attribute attr)
    {
        var type = attr.GetType();
        return (
            type.GetProperty("Target")?.GetValue(attr) as string,
            type.GetProperty("Exclude")?.GetValue(attr) as string,
            type.GetProperty("Priority")?.GetValue(attr) is int priority ? priority : 0);
    }

    private static HashSet<string>? ParseTargetSet(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;
        return target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(NormalizePath).ToHashSet();
    }

    private void AttachToExisting(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (IsShielded(node)) continue;
            AttachRites(node, initial: true);
            foreach (var child in node.GetChildren()) stack.Push(child);
        }
    }

    private void OnNodeAddedForRites(Node node) => AttachRites(node, initial: false);

    private void AttachRites(Node node, bool initial)
    {
        if (IsHiddenFromRites(node)) return;

        var id = node.GetInstanceId();

        // Moved within the tree before the deferred check ran: reconnect so paths and targets are current.
        if (_signalConnections.Remove(id, out var previous)) Disconnect(previous);
        _signalRitesByNode.Remove(id);

        var interfaceRites = InterfaceRitesFor(node.GetType());
        ConnectSignalRites(node, id, interfaceRites?.Signals);
        if (interfaceRites is not null) AttachInterfaceLifecycle(node, id, interfaceRites, initial);
    }

    private void OnNodeRemovedForRites(Node node)
    {
        var id = node.GetInstanceId();
        if (!_signalConnections.ContainsKey(id) && !IsInterfaceInputNode(id)) return;

        // Deferred so signals emitted on the way out (tree_exited) still arrive, and a node moved elsewhere in the tree keeps its rites.
        if (_pendingDetach.Count == 0) Callable.From(DetachRemovedNodes).CallDeferred();
        _pendingDetach.Add(id);
    }

    private void DetachRemovedNodes()
    {
        foreach (var id in _pendingDetach)
        {
            var node = InstanceFromId(id) as Node;
            if (node is not null && node.IsInsideTree()) continue;

            if (_signalConnections.Remove(id, out var connections) && node is not null) Disconnect(connections);
            _signalRitesByNode.Remove(id);
            DetachInterfaceInput(id);
        }

        _pendingDetach.Clear();
    }

    private static void Disconnect(List<SignalConnection> connections)
    {
        foreach (var (emitterId, signal, callable) in connections)
        {
            if (InstanceFromId(emitterId) is { } emitter && emitter.IsConnected(signal, callable)) emitter.Disconnect(signal, callable);
        }
    }

    private void ConnectSignalRites(Node node, ulong id, List<SignalRite>? interfaceRites)
    {
        if (_signalRites.Count == 0 && interfaceRites is null) return;

        string? path = null;
        Dictionary<(ulong EmitterId, StringName Signal), (GodotObject Emitter, SignalShape Shape, List<SignalRite.Binding> Bindings)>? groups = null;

        IEnumerable<SignalRite> rites = interfaceRites is null ? _signalRites : _signalRites.Concat(interfaceRites).OrderBy(rite => rite.Priority);

        foreach (var rite in rites)
        {
            if (!rite.AppliesTo(node)) continue;

            if (rite.Targets is not null || rite.Excludes is not null)
            {
                path ??= node.GetPath().ToString();
                if (rite.Targets is not null && !rite.Targets.Contains(path)) continue;
                if (PathMatchesAnyExclude(path, rite.Excludes)) continue;
            }

            if (rite.EmitterFor(node) is not { } emitter) continue;

            if (SignalShape.Of(emitter, rite.Signal) is not { } shape)
            {
                // An interface promised this signal; a plain OnSignal just skips nodes without it.
                if (rite.OnInstance && _missingSignalReported.Add((rite, node.GetType())))
                    LiturgyReport.AddMethodError(rite.MethodName, rite.HookName, $"{node.GetType().Name} implements {rite.Method.DeclaringType?.Name} but has no '{rite.Signal}' signal.");
                continue;
            }

            if (rite.Bind(shape) is not { } binding || !binding.Accepts(node)) continue;

            if (shape.Arity > MAX_SIGNAL_ARGUMENTS)
            {
                if (_missingSignalReported.Add((rite, node.GetType())))
                    LiturgyReport.AddMethodError(rite.MethodName, rite.HookName, $"Signal '{rite.Signal}' has {shape.Arity} arguments; Liturgy handles at most {MAX_SIGNAL_ARGUMENTS}.");
                continue;
            }

            groups ??= [];
            var key = (emitter.GetInstanceId(), rite.Signal);
            if (!groups.TryGetValue(key, out var group))
            {
                group = (emitter, shape, []);
                groups[key] = group;
            }

            group.Bindings.Add(binding);
        }

        if (groups is null) return;

        var connections = new List<SignalConnection>(groups.Count);

        foreach (var ((emitterId, signal), (emitter, shape, bindings)) in groups)
        {
            var ordered = bindings.ToArray();
            var callable = CreateSignalCallable(shape.Arity, arguments =>
            {
                foreach (var binding in ordered) binding.Invoke(node, arguments);
            });

            emitter.Connect(signal, callable);
            connections.Add(new SignalConnection(emitterId, signal, callable));
        }

        _signalConnections[id] = connections;
        RecordSignalRites(id, groups.Values.SelectMany(group => group.Bindings).Select(binding => binding.Rite.Method));
    }

    // The arguments are a span so each emission passes them on the stack instead of in a new array.
    private delegate void SignalHandler(ReadOnlySpan<Variant> arguments);

    private static Callable CreateSignalCallable(int arity, SignalHandler handler) => arity switch
    {
        0 => Callable.From(() => handler([])),
        1 => Callable.From((Variant a) => handler([a])),
        2 => Callable.From((Variant a, Variant b) => handler([a, b])),
        3 => Callable.From((Variant a, Variant b, Variant c) => handler([a, b, c])),
        4 => Callable.From((Variant a, Variant b, Variant c, Variant d) => handler([a, b, c, d])),
        5 => Callable.From((Variant a, Variant b, Variant c, Variant d, Variant e) => handler([a, b, c, d, e])),
        6 => Callable.From((Variant a, Variant b, Variant c, Variant d, Variant e, Variant f) => handler([a, b, c, d, e, f])),
        7 => Callable.From((Variant a, Variant b, Variant c, Variant d, Variant e, Variant f, Variant g) => handler([a, b, c, d, e, f, g])),
        8 => Callable.From((Variant a, Variant b, Variant c, Variant d, Variant e, Variant f, Variant g, Variant h) => handler([a, b, c, d, e, f, g, h])),
        9 => Callable.From((Variant a, Variant b, Variant c, Variant d, Variant e, Variant f, Variant g, Variant h, Variant i) => handler([a, b, c, d, e, f, g, h, i])),
        _ => throw new ArgumentOutOfRangeException(nameof(arity))
    };
}
