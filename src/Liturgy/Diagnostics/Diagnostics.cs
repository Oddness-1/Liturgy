using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using Liturgy.Internal;

namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Everything Liturgy knows about the rites in the running game: which exist, which nodes they touch, how often
    /// they've run and for whom. The harness (Alt+~ in editor runs) is built on this, and a dev console can use it too.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (var rite in Rite.Diagnostics.Rites)
    ///     GD.Print($"{rite.Name}: {rite.Fires} fires, touches {Rite.Diagnostics.CountTouching()[rite]} nodes");
    /// </code>
    /// </example>
    public static class Diagnostics
    {
        /// <summary>
        /// The most recent firings <see cref="RecentFirings"/> keeps.
        /// </summary>
        public const int LOG_CAPACITY = 1024;

        /// <summary>
        /// True in editor runs, where Liturgy counts, times and logs every rite. False in exported games, where rites
        /// run with nothing in between, and in editor runs with <c>Rite.Settings(Harness = false)</c> or launched with
        /// <c>--liturgy-quiet</c>.
        /// </summary>
        public static bool IsEnabled { get; internal set; }

        /// <summary>
        /// When Liturgy started, in <see cref="System.Diagnostics.Stopwatch"/> ticks; firing timestamps count from here.
        /// </summary>
        public static long Epoch { get; } = System.Diagnostics.Stopwatch.GetTimestamp();

        /// <summary>
        /// Every method that carries rites, in the order Liturgy found them.
        /// </summary>
        public static IReadOnlyList<RiteInfo> Rites => _rites;

        /// <summary>
        /// How many rites have fired since the game started, including those no longer in <see cref="RecentFirings"/>.
        /// </summary>
        public static long TotalFirings { get; private set; }

        /// <summary>
        /// The rites that act on <paramref name="node"/>: those that fire for its signals, run for it each frame, or
        /// run when it's ready. Rites that act on the whole game (input, tree changes) aren't listed.
        /// </summary>
        public static IReadOnlyList<RiteInfo> Touching(Node node) => LiturgyNode.Instance?.RitesTouching(node) ?? [];

        /// <summary>
        /// How many nodes in the tree each rite touches right now. Walks the whole tree, so call it occasionally rather
        /// than every frame.
        /// </summary>
        public static IReadOnlyDictionary<RiteInfo, int> CountTouching() => LiturgyNode.Instance?.CountRitesTouching() ?? new Dictionary<RiteInfo, int>();

        /// <summary>
        /// The nodes in the tree <paramref name="rite"/> touches right now.
        /// </summary>
        public static IReadOnlyList<Node> NodesTouchedBy(RiteInfo rite) => LiturgyNode.Instance?.NodesTouchedBy(rite) ?? [];

        /// <summary>
        /// Every <see cref="Rite.AssignNode"/> member, and the node each holds right now.
        /// </summary>
        public static IReadOnlyList<AssignmentInfo> Assignments => LiturgyNode.Instance?.Assignments ?? [];

        /// <summary>
        /// The most recent firings, oldest first.
        /// </summary>
        public static IReadOnlyList<RiteFiring> RecentFirings
        {
            get
            {
                var count = (int)Math.Min(TotalFirings, LOG_CAPACITY);
                var firings = new RiteFiring[count];
                for (var i = 0; i < count; i++) firings[i] = _log[(int)((TotalFirings - count + i) % LOG_CAPACITY)];
                return firings;
            }
        }

        /// <summary>
        /// Firings recorded after the first <paramref name="seen"/>, for a view that shows new firings as they come. At
        /// most <see cref="LOG_CAPACITY"/> are returned.
        /// </summary>
        public static IReadOnlyList<RiteFiring> FiringsSince(long seen)
        {
            var count = (int)Math.Min(Math.Max(TotalFirings - seen, 0), LOG_CAPACITY);
            var firings = new RiteFiring[count];
            for (var i = 0; i < count; i++) firings[i] = _log[(int)((TotalFirings - count + i) % LOG_CAPACITY)];
            return firings;
        }

        /// <summary>
        /// Clears every rite's counts and the log of firings.
        /// </summary>
        public static void Reset()
        {
            foreach (var rite in _rites) rite.Reset();
            TotalFirings = 0;
        }

        private static readonly List<RiteInfo> _rites = [];
        private static readonly Dictionary<MethodInfo, RiteInfo> _byMethod = [];
        private static readonly RiteFiring[] _log = new RiteFiring[LOG_CAPACITY];

        internal static RiteInfo Register(MethodInfo method)
        {
            if (_byMethod.TryGetValue(method, out var existing)) return existing;

            var rite = new RiteInfo(method);
            _byMethod[method] = rite;
            _rites.Add(rite);
            return rite;
        }

        /// <summary>
        /// The rite declared on <paramref name="method"/>, when diagnostics are on and it's a rite.
        /// </summary>
        internal static RiteInfo? For(MethodInfo method) => IsEnabled && _byMethod.TryGetValue(method, out var rite) ? rite : null;

        internal static void Record(RiteInfo rite, ulong nodeId, long timestamp)
        {
            _log[(int)(TotalFirings % LOG_CAPACITY)] = new RiteFiring(rite, nodeId, timestamp);
            TotalFirings++;
        }
    }
}

/// <summary>
/// One run of a rite, as <see cref="Rite.Diagnostics.RecentFirings"/> records it.
/// </summary>
/// <param name="Rite">The rite that ran.</param>
/// <param name="NodeId">The instance id of the node it ran for, or 0 when it wasn't given one.</param>
/// <param name="Timestamp">When it ran, in <see cref="System.Diagnostics.Stopwatch"/> ticks.</param>
public readonly record struct RiteFiring(RiteInfo Rite, ulong NodeId, long Timestamp)
{
    /// <summary>
    /// The node it ran for, if it was given one and the node still exists.
    /// </summary>
    public Node? Node => NodeId == 0 ? null : GodotObject.InstanceFromId(NodeId) as Node;
}
