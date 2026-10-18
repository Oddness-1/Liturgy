using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Liturgy.Internal;

// The Assigned tab: every [Rite.AssignNode] member and the node it holds.
internal sealed partial class Harness
{
    private const int MAX_HOLDERS_SHOWN = 20;

    private void BuildAssignedTab()
    {
        var tab = new VBoxContainer { Name = "Assigned" };
        _tabs.AddChild(tab);

        var split = NewSplit("assigned");
        tab.AddChild(split);

        _assigned = NewTree(ASSIGNED_TITLES.Length);
        _assigned.SetColumnExpandRatio(0, 3);
        _assigned.SetColumnExpandRatio(1, 3);
        _assigned.SetColumnExpandRatio(2, 2);
        _assigned.SizeFlagsStretchRatio = 2f;
        _assigned.ItemSelected += () =>
        {
            if (_assigned.GetSelected() is not { } item) return;
            _selectedAssignment = _assignedItems[item.GetMetadata(0).AsInt32()].Assignment;
            ShowAssignment(_selectedAssignment);
            UpdateHighlights();
        };
        _assigned.ItemActivated += () =>
        {
            if (_selectedAssignment?.Node is not { } node || !IsInstanceValid(node)) return;
            SelectNode(node);
            _tabs.CurrentTab = NODES_TAB;
        };
        _assigned.ColumnTitleClicked += (column, button) =>
        {
            if (button != (long)MouseButton.Left) return;
            _assignedSort = Toggle(_assignedSort, (int)column);
            HarnessPrefs.Set(SORT, "assigned", _assignedSort.Save());
            SortAssigned();
        };
        split.AddChild(_assigned);
        _ = new HarnessColumns(_assigned, "assigned");

        _assignedDetails = Details();
        split.AddChild(_assignedDetails);
        SortAssigned();
    }

    private void UpdateAssignedTree()
    {
        var assignments = Rite.Diagnostics.Assignments;

        if (_assignedItems.Count != assignments.Count)
        {
            _assigned.Clear();
            _assignedItems.Clear();

            var root = _assigned.CreateItem();
            foreach (var assignment in assignments)
            {
                var item = _assigned.CreateItem(root);
                item.SetMetadata(0, _assignedItems.Count);
                item.SetText(0, assignment.Name);
                item.SetText(1, ShortPath(assignment.Path));
                item.SetCustomColor(1, HarnessTheme.DIM);
                _assignedItems.Add((assignment, item));
            }

            if (assignments.Count == 0)
            {
                _assignedDetails.Text = $"[color=#{Hex(HarnessTheme.DIM)}]No [lb]Rite.AssignNode] members in this game. Put one on a static field or property, or on an interface property, to have Liturgy keep it pointed at a node.[/color]";
            }
            else if (_selectedAssignment is null)
            {
                _assignedDetails.Text = $"[color=#{Hex(HarnessTheme.DIM)}]Select a member to see the node it holds, outlined in gold. Double-click to inspect that node.[/color]";
            }
        }

        foreach (var (assignment, item) in _assignedItems)
        {
            // An interface property is held by every implementing node; the count says how many hold it now.
            if (assignment.PerNode) item.SetText(0, $"{assignment.Name}  ×{assignment.HolderCount}");

            var node = assignment.Node is { } held && IsInstanceValid(held) ? held : null;
            item.SetText(2, node?.GetType().Name ?? "empty");
            item.SetCustomColor(2, node is not null ? HarnessTheme.TEXT : assignment.Problem?.StartsWith("The node at") == true ? HarnessTheme.RED : HarnessTheme.DIM);
            item.SetCustomColor(0, node is not null ? HarnessTheme.TEXT : HarnessTheme.DIM);
        }

        SortAssigned();
        if (_selectedAssignment is not null && _tabs.CurrentTab == ASSIGNED_TAB) ShowAssignment(_selectedAssignment);
    }

    private void SortAssigned()
    {
        ShowSort(_assigned, ASSIGNED_TITLES, _assignedSort, []);
        if (_assignedItems.Count == 0) return;

        var reversed = _assignedSort.Reversed;
        var sorted = _assignedSort.Column switch
        {
            0 => OrderText(_assignedItems, entry => entry.Assignment.Name, reversed),
            1 => OrderText(_assignedItems, entry => entry.Assignment.Path, reversed),
            _ => Order(_assignedItems, entry => entry.Assignment.Node is null ? 1 : 0, reversed).ThenBy(entry => entry.Item.GetText(2), StringComparer.OrdinalIgnoreCase)
        };

        Reorder(_assigned.GetRoot(), sorted.ThenBy(entry => entry.Assignment.Name, StringComparer.OrdinalIgnoreCase).Select(entry => entry.Item));
    }

    private void ShowAssignment(AssignmentInfo assignment)
    {
        var gold = Hex(HarnessTheme.GOLD);
        var dim = Hex(HarnessTheme.DIM);
        var text = new StringBuilder();

        text.Append($"[b][color=#{gold}]{Escape(assignment.Name)}[/color][/b]  [color=#{dim}]{Escape(assignment.MemberType.Name)}[/color]\n");
        text.Append($"[color=#{dim}]{Escape(assignment.FullName)}[/color]\n\n");
        text.Append($"[color=#{dim}]Follows[/color]  {Escape(assignment.Path)}\n");

        if (assignment.Node is { } node && IsInstanceValid(node))
            text.Append($"[color=#{dim}]Holds[/color]  {Escape(ShortPath(node))}, outlined in gold\n");
        else
            text.Append($"[color=#{dim}]Holds[/color]  nothing. {Escape(assignment.Problem ?? "")}\n");

        text.Append($"[color=#{dim}]Assigned[/color]  {assignment.Assignments} time{(assignment.Assignments == 1 ? "" : "s")}");

        if (assignment.PerNode)
        {
            var holders = assignment.Holders.Where(IsInstanceValid).ToList();
            text.Append($"\n\n[color=#{dim}]Held by {holders.Count} node{(holders.Count == 1 ? "" : "s")} implementing {Escape(assignment.Member.DeclaringType?.Name ?? "")}[/color]");
            foreach (var holder in holders.Take(MAX_HOLDERS_SHOWN)) text.Append($"\n{Escape(ShortPath(holder))}");
            if (holders.Count > MAX_HOLDERS_SHOWN) text.Append($"\n[color=#{dim}]and {holders.Count - MAX_HOLDERS_SHOWN} more[/color]");
        }

        _assignedDetails.Text = text.ToString();
    }
}
