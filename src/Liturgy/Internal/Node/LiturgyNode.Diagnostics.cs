using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

/// <summary>
/// What <see cref="Rite.Diagnostics"/> reports about nodes: which rites touch which nodes, and which nodes are hidden
/// from rites altogether (Liturgy's own, and those in <see cref="Rite.IGNORE_GROUP"/>).
/// </summary>
internal sealed partial class LiturgyNode
{
    internal static LiturgyNode? Instance { get; private set; }

    // Rites whose signals pass the physics node they meet, so the rite's node parameter isn't the emitter.
    private static readonly HashSet<Type> OTHER_NODE_SIGNALS = [typeof(OnBodyEntered), typeof(OnBodyExited), typeof(OnAreaEntered), typeof(OnAreaExited)];

    private static readonly Dictionary<ulong, bool> _shielded = [];
    private static readonly List<ulong> _pendingUnshield = [];

    // The signal rites (OnSignal, promoted and interface) each node has connected, exactly.
    private readonly Dictionary<ulong, HashSet<MethodInfo>> _signalRitesByNode = [];

    private List<(RiteInfo Rite, Func<Node, string, bool> Touches)>? _touchRules;

    /// <summary>
    /// Liturgy's own nodes, a node in <see cref="Rite.IGNORE_GROUP"/>, and everything under either. No rite sees them.
    /// </summary>
    private static bool IsShielded(Node node)
    {
        var id = node.GetInstanceId();
        if (_shielded.TryGetValue(id, out var shielded)) return shielded;

        var parent = node.GetParent();
        shielded = node is LiturgyNode || node.IsInGroup(IGNORE_GROUP) || (parent is not null && IsShielded(parent));
        if (node.IsInsideTree()) _shielded[id] = shielded;
        return shielded;
    }

    private static bool IsHiddenFromRites(Node node) => IsShielded(node) || IsInternalNode(node);

    // In every run, exports included: the shield cache would otherwise keep every node that ever left.
    private void OnNodeRemovedForShield(Node node)
    {
        // Forgotten after every other handler has seen the node leave, so none of them caches it again.
        if (_pendingUnshield.Count == 0) Callable.From(ForgetRemovedNodes).CallDeferred();
        _pendingUnshield.Add(node.GetInstanceId());
    }

    private static void ForgetRemovedNodes()
    {
        foreach (var id in _pendingUnshield)
        {
            if (InstanceFromId(id) is Node node && node.IsInsideTree()) continue;

            _shielded.Remove(id);
            if (!Rite.Diagnostics.IsEnabled) continue;
            foreach (var rite in Rite.Diagnostics.Rites) rite.Forget(id);
        }

        _pendingUnshield.Clear();
    }

    private void RecordSignalRites(ulong id, IEnumerable<MethodInfo> methods)
    {
        if (!Rite.Diagnostics.IsEnabled) return;
        _signalRitesByNode[id] = methods.ToHashSet();
    }

    internal IReadOnlyList<RiteInfo> RitesTouching(Node node)
    {
        if (!IsInstanceValid(node) || !node.IsInsideTree() || IsHiddenFromRites(node)) return [];

        var id = node.GetInstanceId();
        var path = node.GetPath().ToString();
        _signalRitesByNode.TryGetValue(id, out var signalRites);

        var touching = new List<RiteInfo>();
        foreach (var (rite, touches) in TouchRules)
        {
            if (signalRites?.Contains(rite.Method) == true || rite.FiresFor(id) > 0 || touches(node, path)) touching.Add(rite);
        }

        return touching;
    }

    internal readonly record struct SurveyedNode(Node Node, ulong Id, ulong ParentId, int Depth, IReadOnlyList<RiteInfo> Rites);

    /// <summary>
    /// Every node rites can see, in tree order, with the rites touching each.
    /// </summary>
    internal List<SurveyedNode> Survey()
    {
        var surveyed = new List<SurveyedNode>();
        var stack = new Stack<(Node Node, ulong ParentId, int Depth)>();
        stack.Push((_root, 0, 0));

        while (stack.Count > 0)
        {
            var (node, parentId, depth) = stack.Pop();
            if (IsShielded(node)) continue;

            // An internal node isn't listed; its regular children hang from its parent instead.
            if (IsInternalNode(node))
            {
                PushChildren(node, parentId, depth);
                continue;
            }

            var id = node.GetInstanceId();
            surveyed.Add(new SurveyedNode(node, id, parentId, depth, RitesTouching(node)));
            PushChildren(node, id, depth + 1);
        }

        return surveyed;

        void PushChildren(Node node, ulong parentId, int depth)
        {
            var children = node.GetChildren();
            for (var i = children.Count - 1; i >= 0; i--) stack.Push((children[i], parentId, depth));
        }
    }

    internal Dictionary<RiteInfo, int> CountRitesTouching()
    {
        var counts = Rite.Diagnostics.Rites.ToDictionary(rite => rite, _ => 0);
        foreach (var surveyed in Survey())
        {
            foreach (var rite in surveyed.Rites) counts[rite]++;
        }

        return counts;
    }

    internal List<Node> NodesTouchedBy(RiteInfo rite) => Survey().Where(surveyed => surveyed.Rites.Contains(rite)).Select(surveyed => surveyed.Node).ToList();

    /// <summary>
    /// For each rite, whether it acts on a given node, worked out from the rite's own declaration. Signal rites are
    /// answered exactly from their connections instead; rites that act on the whole game (input, tree changes,
    /// global process and root ready) touch no node.
    /// </summary>
    private List<(RiteInfo Rite, Func<Node, string, bool> Touches)> TouchRules => _touchRules ??= BuildTouchRules();

    private List<(RiteInfo, Func<Node, string, bool>)> BuildTouchRules()
    {
        var rules = new List<(RiteInfo, Func<Node, string, bool>)>();

        foreach (var rite in Rite.Diagnostics.Rites)
        {
            var tests = rite.Attributes.Select(attribute => TouchTest(rite.Method, attribute)).OfType<Func<Node, string, bool>>().ToArray();
            rules.Add((rite, tests.Length == 0 ? static (_, _) => false : (node, path) => tests.Any(test => test(node, path))));
        }

        return rules;
    }

    private Func<Node, string, bool>? TouchTest(MethodInfo method, RiteAttribute attribute)
    {
        if (method.DeclaringType is { IsInterface: true } owner) return InterfaceTouchTest(owner, attribute);

        var attributeType = attribute.GetType();

        // Connected per node through SignalRite; RecordSignalRites has them exactly.
        if (attribute is OnSignal || SIGNAL_RITES.Any(entry => entry.Attribute == attributeType)) return null;

        switch (attribute)
        {
            case OnProcess or OnPhysicsProcess:
            {
                var entries = attribute is OnProcess ? _processTargetedEntries : _physicsProcessTargetedEntries;
                var mine = entries.Where(entry => entry.Method == method).ToArray();
                if (mine.Length == 0) return null;
                return (node, path) => mine.Any(entry => MatchesEntry(node, path, entry));
            }

            case OnReady ready:
            {
                var nodeType = NodeParameterType(method);
                if (ready.Mode == ReadyMode.PerNode)
                {
                    var excludes = ParseExcludeSet(ready.Exclude);
                    return (node, path) => (nodeType is null || nodeType.IsInstanceOfType(node)) && !PathMatchesAnyExclude(path, excludes);
                }

                if (ready.Mode == ReadyMode.RootReady && ParseTargetSet(ready.Target) is { } targets) return (_, path) => targets.Contains(path);
                return null;
            }
        }

        if (!INTERFACE_SIGNALS.TryGetValue(attributeType, out var signal)) return null;

        // A node-wide rite Liturgy reaches by listening to the node's own signal.
        var (target, exclude, _) = ReadRiteFilters(attribute);
        var targetSet = ParseTargetSet(target);
        var excludeSet = ParseExcludeSet(exclude);
        var sourceType = OTHER_NODE_SIGNALS.Contains(attributeType) ? null : NodeParameterType(method);
        var signalName = new StringName(signal);

        return (node, path) =>
            IsListening(node, signalName) &&
            (sourceType is null || sourceType.IsInstanceOfType(node)) &&
            (targetSet is null || targetSet.Contains(path)) &&
            !PathMatchesAnyExclude(path, excludeSet);
    }

    private static Func<Node, string, bool>? InterfaceTouchTest(Type owner, RiteAttribute attribute)
    {
        // Interface signal rites connect per node, and are recorded exactly.
        if (INTERFACE_SIGNALS.ContainsKey(attribute.GetType()) || attribute is OnSignal) return null;

        var definition = owner.IsGenericType ? owner.GetGenericTypeDefinition() : owner;
        var excludes = ParseExcludeSet(ReadRiteFilters(attribute).Exclude);

        return (node, path) => Implements(node.GetType(), definition) && !PathMatchesAnyExclude(path, excludes);
    }

    private static bool Implements(Type type, Type interfaceDefinition) =>
        type.GetInterfaces().Any(implemented => implemented == interfaceDefinition || (implemented.IsGenericType && implemented.GetGenericTypeDefinition() == interfaceDefinition));

    private bool IsListening(Node node, StringName signal) =>
        _listeners.TryGetValue(node.GetInstanceId(), out var connections) && connections.Any(connection => connection.Signal == signal);

    /// <summary>
    /// The most specific node (or interface) type among the rite's parameters, which narrows the nodes it accepts.
    /// </summary>
    private static Type? NodeParameterType(MethodInfo method) => method.GetParameters()
        .Select(parameter => parameter.ParameterType)
        .Where(type => typeof(Node).IsAssignableFrom(type) || type.IsInterface)
        .Where(type => type != typeof(Node))
        .FirstOrDefault();
}
