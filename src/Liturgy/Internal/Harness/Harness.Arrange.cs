using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Liturgy.Internal;

// Sorting by the headings, and the panel's width.
internal sealed partial class Harness
{
    private static readonly string[] RITE_TITLES = ["", "Rite", "Nodes", "Fires", "Time", "Last"];
    private static readonly string[] NODE_TITLES = ["Node", "Type", "Rites"];
    private static readonly string[] ASSIGNED_TITLES = ["Member", "Path", "Node"];

    // The columns whose first click puts the biggest, or most recent, first.
    private static readonly HashSet<int> RITE_COLUMNS_DOWN = [2, 3, 4, 5];
    private static readonly HashSet<int> NODE_COLUMNS_DOWN = [2];

    /// <summary>
    /// A sort by one column. <see cref="Reversed"/> flips the column's first-click order: A to Z for names, biggest
    /// first for counts and times.
    /// </summary>
    private readonly record struct Sorting(int Column, bool Reversed)
    {
        public Vector2I Save() => new(Column, Reversed ? 1 : 0);

        public static Sorting? Load(Vector2I saved, int columns) => saved.X >= 0 && saved.X < columns ? new Sorting(saved.X, saved.Y != 0) : null;
    }

    private void RestoreLayout()
    {
        _panelWidth = HarnessPrefs.Get(LAYOUT, "panel_width", PANEL_WIDTH).AsSingle();
        _riteSort = Sorting.Load(HarnessPrefs.Get(SORT, "rites", new Vector2I(1, 0)).AsVector2I(), RITE_TITLES.Length) ?? new Sorting(1, false);
        _nodeSort = Sorting.Load(HarnessPrefs.Get(SORT, "nodes", new Vector2I(-1, 0)).AsVector2I(), NODE_TITLES.Length);
        _assignedSort = Sorting.Load(HarnessPrefs.Get(SORT, "assigned", new Vector2I(0, 0)).AsVector2I(), ASSIGNED_TITLES.Length) ?? new Sorting(0, false);
    }

    // The same column flips; another starts in its first-click order.
    private static Sorting Toggle(Sorting current, int column) => current.Column == column ? current with { Reversed = !current.Reversed } : new Sorting(column, false);

    // The node tree has a third state, its scene order, which a third click on the same column returns to.
    private static Sorting? Cycle(Sorting? current, int column) => current switch
    {
        { Column: var sorted, Reversed: false } when sorted == column => new Sorting(column, true),
        { Column: var sorted, Reversed: true } when sorted == column => null,
        _ => new Sorting(column, false)
    };

    private void SortRites()
    {
        ShowSort(_rites, RITE_TITLES, _riteSort, RITE_COLUMNS_DOWN);
        if (_riteItems.Count == 0) return;

        var (column, reversed) = _riteSort;
        var sorted = column == 1
            ? OrderText(_riteItems, entry => entry.Rite.Name, reversed)
            : Order(_riteItems, entry => RiteKey(entry.Rite, column), reversed).ThenBy(entry => entry.Rite.Name, StringComparer.OrdinalIgnoreCase);

        Reorder(_rites.GetRoot(), sorted.Select(entry => entry.Item));
    }

    // Smaller sorts first, so each column's first-click order comes out of an ascending sort.
    private double RiteKey(RiteInfo rite, int column) => column switch
    {
        0 => rite.Muted ? 0 : 1,
        2 => -(IsGameWide(rite) ? double.MaxValue : _touchCounts.GetValueOrDefault(rite)),
        3 => -rite.Fires,
        4 => -rite.TotalMilliseconds,
        5 => rite.SecondsSinceLastFire ?? double.MaxValue,
        _ => 0
    };

    private void SortNodes()
    {
        ShowSort(_nodes, NODE_TITLES, _nodeSort, NODE_COLUMNS_DOWN);
        if (_nodeSort is not { } sort || _nodes.GetRoot() is not { } root) return;

        var parents = new Stack<TreeItem>();
        parents.Push(root);

        while (parents.Count > 0)
        {
            var parent = parents.Pop();
            var children = parent.GetChildren().ToList();
            if (children.Count == 0) continue;

            var sorted = sort.Column switch
            {
                0 => OrderText(children, child => child.GetText(0), sort.Reversed),
                1 => OrderText(children, child => child.GetText(1), sort.Reversed).ThenBy(child => child.GetText(0), StringComparer.OrdinalIgnoreCase),
                _ => Order(children, child => -RiteCountOf(child), sort.Reversed).ThenBy(child => child.GetText(0), StringComparer.OrdinalIgnoreCase)
            };

            Reorder(parent, sorted);
            foreach (var child in children) parents.Push(child);
        }
    }

    private int RiteCountOf(TreeItem item) => _surveyById.TryGetValue(item.GetMetadata(0).AsUInt64(), out var surveyed) ? surveyed.Rites.Count : 0;

    private static IOrderedEnumerable<T> Order<T>(IEnumerable<T> items, Func<T, double> key, bool reversed) => reversed ? items.OrderByDescending(key) : items.OrderBy(key);

    private static IOrderedEnumerable<T> OrderText<T>(IEnumerable<T> items, Func<T, string> key, bool reversed) =>
        reversed ? items.OrderByDescending(key, StringComparer.OrdinalIgnoreCase) : items.OrderBy(key, StringComparer.OrdinalIgnoreCase);

    // Moves only the items out of place, so a sort that changes nothing leaves the tree alone.
    private static void Reorder(TreeItem parent, IEnumerable<TreeItem> sorted)
    {
        TreeItem? previous = null;
        foreach (var item in sorted)
        {
            if (previous is null)
            {
                if (parent.GetFirstChild() is { } first && first != item) item.MoveBefore(first);
            }
            else if (previous.GetNext() != item) item.MoveAfter(previous);

            previous = item;
        }
    }

    // The arrow shows the order on screen: up for A to Z and smallest first, down for biggest or most recent first.
    private static void ShowSort(Tree tree, string[] titles, Sorting? sort, HashSet<int> columnsDown)
    {
        for (var column = 0; column < titles.Length; column++)
        {
            if (sort is not { } active || active.Column != column)
            {
                tree.SetColumnTitle(column, titles[column]);
                continue;
            }

            var down = columnsDown.Contains(column) != active.Reversed;
            tree.SetColumnTitle(column, $"{titles[column]} {(down ? "▼" : "▲")}".Trim());
        }
    }

    private void OnGripInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }:
                _gripDragging = true;
                _panelWidth = _panel.OffsetRight - _panel.OffsetLeft;
                _grip.AcceptEvent();
                break;

            case InputEventMouseMotion motion when _gripDragging:
                _panelWidth += motion.Relative.X;
                FitToScreen();
                _panelWidth = _panel.OffsetRight - _panel.OffsetLeft;
                _grip.AcceptEvent();
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when _gripDragging:
                _gripDragging = false;
                HarnessPrefs.Set(LAYOUT, "panel_width", _panelWidth);
                _grip.QueueRedraw();
                _grip.AcceptEvent();
                break;
        }
    }

    private void DrawGrip()
    {
        var hovered = _grip.GetGlobalRect().HasPoint(_grip.GetGlobalMousePosition());
        if (hovered || _gripDragging) _grip.DrawRect(new Rect2(3, 0, 2, _grip.Size.Y), HarnessTheme.GOLD);
    }
}
