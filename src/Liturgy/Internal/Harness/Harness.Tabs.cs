using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Godot;

namespace Liturgy.Internal;

internal sealed partial class Harness
{
    private void BuildWindow()
    {
        _window = new Window
        {
            Name = "Window",
            Title = "Liturgy",
            Visible = false,
            Borderless = true,
            Transparent = true,
            TransparentBg = true,
            Unresizable = true,
            AlwaysOnTop = true,
            WrapControls = false,
            ProcessMode = ProcessModeEnum.Always
        };

        AddChild(_window);
        _window.CloseRequested += Close;

        var ui = new Control { Name = "UI", Theme = HarnessTheme.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        ui.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _window.AddChild(ui);

        _overlay = new HarnessOverlay { Name = "Overlay" };
        _overlay.CloseRequested += Close;
        _overlay.Picked += node =>
        {
            SelectNode(node);
            _tabs.CurrentTab = NODES_TAB;
        };
        ui.AddChild(_overlay);

        _panel = new PanelContainer { Name = "Panel" };
        _panel.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        _panel.OffsetLeft = 16;
        _panel.OffsetTop = 16;
        _panel.OffsetBottom = -16;
        _panel.OffsetRight = 16 + PANEL_WIDTH;
        ui.AddChild(_panel);

        _grip = new Control { Name = "Grip", MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.Hsize };
        _grip.GuiInput += OnGripInput;
        _grip.MouseEntered += _grip.QueueRedraw;
        _grip.MouseExited += _grip.QueueRedraw;
        _grip.Draw += DrawGrip;
        ui.AddChild(_grip);

        var column = new VBoxContainer();
        _panel.AddChild(column);

        var header = new HBoxContainer();
        column.AddChild(header);

        var title = new Label { Text = "LITURGY" };
        title.AddThemeColorOverride("font_color", HarnessTheme.GOLD);
        title.AddThemeFontSizeOverride("font_size", 20);
        header.AddChild(title);
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        header.AddChild(DimLabel("Alt+~ or Esc to close"));

        _stats = DimLabel("");
        column.AddChild(_stats);

        _tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _tabs.TabChanged += _ =>
        {
            _overlay.Picking = false;
            UpdateHighlights();
        };
        column.AddChild(_tabs);
    }

    private void BuildRitesTab()
    {
        var tab = new VBoxContainer { Name = "Rites" };
        _tabs.AddChild(tab);

        _riteFilter = Filter("Filter by name, rite or description");
        _riteFilter.TextChanged += _ => UpdateRitesTree();
        tab.AddChild(_riteFilter);

        var split = NewSplit("rites");
        tab.AddChild(split);

        // Muting has a column of its own, so only the checkbox mutes; clicking a rite's name just selects it.
        _rites = NewTree(RITE_TITLES.Length);
        _rites.SetColumnCustomMinimumWidth(0, 28);
        _rites.SetColumnCustomMinimumWidth(2, 60);
        _rites.SetColumnCustomMinimumWidth(3, 64);
        _rites.SetColumnCustomMinimumWidth(4, 76);
        _rites.SetColumnCustomMinimumWidth(5, 56);
        foreach (var column in new[] { 0, 2, 3, 4, 5 }) _rites.SetColumnExpand(column, false);
        _rites.SizeFlagsStretchRatio = 2f;
        _rites.ColumnTitleClicked += (column, button) =>
        {
            if (button != (long)MouseButton.Left) return;
            _riteSort = Toggle(_riteSort, (int)column);
            HarnessPrefs.Set(SORT, "rites", _riteSort.Save());
            SortRites();
        };
        split.AddChild(_rites);
        _ = new HarnessColumns(_rites, "rites");

        _rites.ItemSelected += () =>
        {
            if (RiteAt(_rites.GetSelected()) is not { } rite) return;
            _selectedRite = rite;
            ShowRite(rite);
            UpdateHighlights();
        };

        _rites.ItemEdited += () =>
        {
            var item = _rites.GetEdited();
            if (RiteAt(item) is { } rite) rite.Muted = !item.IsChecked(0);
        };

        var details = new VBoxContainer();
        split.AddChild(details);

        _riteDetails = Details();
        _riteDetails.Text = $"[color=#{Hex(HarnessTheme.DIM)}]Select a rite to see what it does and which nodes it touches; they're outlined in blue. Untick a rite to mute it while you watch what changes.[/color]";
        details.AddChild(_riteDetails);

        _riteNodes = NewList();
        _riteNodes.Visible = false;
        _riteNodes.ItemSelected += index =>
        {
            if (InstanceFromId(_riteNodes.GetItemMetadata((int)index).AsUInt64()) is not Node node) return;
            SelectNode(node);
            _tabs.CurrentTab = NODES_TAB;
        };
        details.AddChild(_riteNodes);
        SortRites();
    }

    private void BuildNodesTab()
    {
        var tab = new VBoxContainer { Name = "Nodes" };
        _tabs.AddChild(tab);

        var bar = new HBoxContainer();
        tab.AddChild(bar);

        _pick = new Button { Text = "Pick on screen", ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
        _pick.Toggled += on => _overlay.Picking = on;
        bar.AddChild(_pick);

        _onlyWithRites = new CheckBox { Text = "Only nodes with rites", ButtonPressed = true, FocusMode = Control.FocusModeEnum.None };
        _onlyWithRites.Toggled += _ => UpdateNodesTree();
        bar.AddChild(_onlyWithRites);

        var split = NewSplit("nodes");
        tab.AddChild(split);

        _nodes = NewTree(NODE_TITLES.Length);
        _nodes.SetColumnExpandRatio(0, 3);
        _nodes.SetColumnExpandRatio(1, 2);
        _nodes.SetColumnExpand(2, false);
        _nodes.SetColumnCustomMinimumWidth(2, 56);
        _nodes.SizeFlagsStretchRatio = 3f;
        _nodes.ColumnTitleClicked += (column, button) =>
        {
            if (button != (long)MouseButton.Left) return;
            _nodeSort = Cycle(_nodeSort, (int)column);
            HarnessPrefs.Set(SORT, "nodes", _nodeSort?.Save() ?? new Vector2I(-1, 0));

            // Scene order is the order the tree is built in, so going back to it means building it again.
            _nodeShape = "";
            UpdateNodesTree();
        };
        _nodes.ItemSelected += () =>
        {
            if (_nodes.GetSelected() is not { } item || InstanceFromId(item.GetMetadata(0).AsUInt64()) is not Node node) return;
            _selectedNodeId = node.GetInstanceId();
            ShowNode(node);
            UpdateHighlights();
        };
        _nodes.ItemCollapsed += item =>
        {
            var id = item.GetMetadata(0).AsUInt64();
            if (item.Collapsed) _collapsed.Add(id);
            else _collapsed.Remove(id);
        };
        split.AddChild(_nodes);
        _ = new HarnessColumns(_nodes, "nodes");

        _nodeDetails = Details();
        _nodeDetails.Text = $"[color=#{Hex(HarnessTheme.DIM)}]Select a node, or pick one on screen, to see every rite that acts on it.[/color]";
        split.AddChild(_nodeDetails);
        SortNodes();
    }

    private void BuildLogTab()
    {
        var tab = new VBoxContainer { Name = "Log" };
        _tabs.AddChild(tab);

        var bar = new HBoxContainer();
        tab.AddChild(bar);

        _logPaused = new CheckBox { Text = "Pause", FocusMode = Control.FocusModeEnum.None };
        bar.AddChild(_logPaused);

        _hidePerFrame = new CheckBox { Text = "Hide per-frame", ButtonPressed = true, FocusMode = Control.FocusModeEnum.None };
        _hidePerFrame.Toggled += _ => RebuildLog();
        bar.AddChild(_hidePerFrame);

        _logFilter = Filter("Filter");
        _logFilter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _logFilter.TextChanged += _ => RebuildLog();
        bar.AddChild(_logFilter);

        _log = NewList();
        _log.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _log.AddThemeFontOverride("font", HarnessTheme.Mono);
        _log.AddThemeFontSizeOverride("font_size", HarnessTheme.FONT_SIZE - 1);
        _log.ItemSelected += index =>
        {
            if (InstanceFromId(_log.GetItemMetadata((int)index).AsUInt64()) is not Node node) return;
            SelectNode(node);
            _tabs.CurrentTab = NODES_TAB;
        };
        tab.AddChild(_log);
    }

    private void BuildGuideTab()
    {
        var guide = Details();
        guide.Name = "Guide";
        guide.FitContent = false;
        guide.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        guide.Text = HarnessText.Guide(Hex(HarnessTheme.GOLD), Hex(HarnessTheme.DIM));
        _tabs.AddChild(guide);
    }

    /// <summary>
    /// Lists the rites, once Liturgy has found them; the harness is built before it looks.
    /// </summary>
    private void ListRites()
    {
        if (_riteItems.Count == Rite.Diagnostics.Rites.Count) return;

        _rites.Clear();
        _riteItems.Clear();

        var root = _rites.CreateItem();
        foreach (var rite in Rite.Diagnostics.Rites.OrderBy(rite => rite.Name, StringComparer.OrdinalIgnoreCase))
        {
            var item = _rites.CreateItem(root);
            item.SetCellMode(0, TreeItem.TreeCellMode.Check);
            item.SetEditable(0, true);
            item.SetText(1, rite.Name);
            item.SetMetadata(0, _riteItems.Count);
            for (var column = 2; column < RITE_TITLES.Length; column++) item.SetTextAlignment(column, HorizontalAlignment.Right);
            _riteItems.Add((rite, item));
        }

        SortRites();
    }

    private void UpdateRitesTree()
    {
        var filter = _riteFilter.Text.Trim();

        foreach (var (rite, item) in _riteItems)
        {
            item.Visible = filter.Length == 0
                || rite.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || rite.Kinds.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || rite.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;
            if (!item.Visible) continue;

            item.SetText(2, IsGameWide(rite) ? "game" : _touchCounts.GetValueOrDefault(rite).ToString());
            item.SetText(3, rite.Fires.ToString());
            item.SetText(4, Duration(rite.TotalMilliseconds));
            item.SetText(5, Ago(rite.SecondsSinceLastFire));
            item.SetChecked(0, !rite.Muted);
            item.SetCustomColor(1, !rite.Running ? HarnessTheme.RED : rite.Muted ? HarnessTheme.DIM : HarnessTheme.TEXT);
        }

        // Re-sorted as the numbers change, the way a task manager keeps its busiest at the top.
        if (_riteSort.Column != 1) SortRites();
    }

    private void ShowRite(RiteInfo rite, bool nodes = true)
    {
        var gold = Hex(HarnessTheme.GOLD);
        var dim = Hex(HarnessTheme.DIM);
        var text = new StringBuilder();

        text.Append($"[b][color=#{gold}]{Escape(rite.Name)}[/color][/b]\n");
        text.Append($"[color=#{dim}]{Escape(rite.FullName)}[/color]\n\n");
        text.Append(rite.Description is { } description
            ? $"{Escape(description)}\n\n"
            : $"[i][color=#{dim}]No description. Add one with Description = \"...\" on the rite.[/color][/i]\n\n");

        text.Append($"[color=#{dim}]Rites[/color]  {Escape(rite.Kinds)}{(rite.OnInterface ? "  (on an interface: each implementing node runs it)" : "")}\n");
        text.Append($"[color=#{dim}]Priority[/color]  {rite.Priority}\n");

        var status = !rite.Running ? $"[color=#{Hex(HarnessTheme.RED)}]Not running: Liturgy rejected it at startup. The console says why.[/color]"
            : rite.Muted ? "Muted" : "Running";
        text.Append($"[color=#{dim}]Status[/color]  {status}\n");

        var average = rite.Fires == 0 ? 0 : rite.TotalMilliseconds * 1000 / rite.Fires;
        text.Append($"[color=#{dim}]Fired[/color]  {rite.Fires} times, {Ago(rite.SecondsSinceLastFire)} last");
        if (rite.LastNode is { } last) text.Append($", for {Escape(ShortPath(last))}");
        text.Append('\n');
        text.Append($"[color=#{dim}]Time[/color]  {rite.TotalMilliseconds:0.##} ms total, {average:0.#} µs each");
        if (rite.Exceptions > 0) text.Append($"  [color=#{Hex(HarnessTheme.RED)}]{rite.Exceptions} threw[/color]");
        text.Append("\n\n");

        var touched = NodesTouchedBy(rite).ToList();
        text.Append(IsGameWide(rite)
            ? $"[color=#{dim}]Acts on the game as a whole, not on particular nodes.[/color]"
            : $"Touches [b]{touched.Count}[/b] node{(touched.Count == 1 ? "" : "s")}{(touched.Count > 0 ? ", outlined in blue:" : ".")}");

        _riteDetails.Text = text.ToString();
        if (!nodes) return;

        _riteNodes.Clear();
        foreach (var node in touched.Take(NODE_LIST_LIMIT))
        {
            var index = _riteNodes.AddItem(ShortPath(node));
            _riteNodes.SetItemMetadata(index, node.GetInstanceId());
            _riteNodes.SetItemTooltipEnabled(index, false);
        }

        if (touched.Count > NODE_LIST_LIMIT) _riteNodes.SetItemDisabled(_riteNodes.AddItem($"…and {touched.Count - NODE_LIST_LIMIT} more"), true);
        _riteNodes.Visible = touched.Count > 0;
    }

    private void UpdateNodesTree()
    {
        var onlyWithRites = _onlyWithRites.ButtonPressed;
        var kept = new HashSet<ulong>();

        if (onlyWithRites)
        {
            foreach (var surveyed in _survey.Where(surveyed => surveyed.Rites.Count > 0))
            {
                for (var id = surveyed.Id; id != 0 && kept.Add(id); id = _surveyById.TryGetValue(id, out var above) ? above.ParentId : 0) { }
            }
        }

        var shown = _survey.Where(surveyed => !onlyWithRites || kept.Contains(surveyed.Id)).ToList();
        var shape = string.Join(',', shown.Select(surveyed => $"{surveyed.Id}:{surveyed.ParentId}"));

        if (shape != _nodeShape)
        {
            _nodeShape = shape;
            _nodes.Clear();
            _nodeItems.Clear();

            var root = _nodes.CreateItem();
            foreach (var surveyed in shown)
            {
                var parent = _nodeItems.GetValueOrDefault(surveyed.ParentId) ?? root;
                var item = _nodes.CreateItem(parent);
                item.SetMetadata(0, surveyed.Id);
                item.SetText(0, surveyed.Node.Name);
                item.SetText(1, surveyed.Node.GetType().Name);
                item.SetCustomColor(1, HarnessTheme.DIM);
                item.SetTextAlignment(2, HorizontalAlignment.Right);
                item.Collapsed = _collapsed.Contains(surveyed.Id);
                _nodeItems[surveyed.Id] = item;
            }

            if (_nodeItems.TryGetValue(_selectedNodeId, out var selected))
            {
                selected.Select(0);
                _nodes.ScrollToItem(selected);
            }
        }

        foreach (var surveyed in shown)
        {
            if (!_nodeItems.TryGetValue(surveyed.Id, out var item)) continue;
            var count = surveyed.Rites.Count;
            item.SetText(2, count == 0 ? "" : count.ToString());
            item.SetCustomColor(0, count == 0 ? HarnessTheme.DIM : HarnessTheme.TEXT);
        }

        SortNodes();

        if (_selectedNodeId != 0 && InstanceFromId(_selectedNodeId) is Node node && _tabs.CurrentTab == NODES_TAB) ShowNode(node);
    }

    private void SelectNode(Node node)
    {
        _selectedNodeId = node.GetInstanceId();

        if (!_nodeItems.ContainsKey(_selectedNodeId) && _onlyWithRites.ButtonPressed && _surveyById.ContainsKey(_selectedNodeId))
            _onlyWithRites.ButtonPressed = false;

        if (_nodeItems.TryGetValue(_selectedNodeId, out var item))
        {
            for (var parent = item.GetParent(); parent is not null; parent = parent.GetParent()) parent.Collapsed = false;
            item.Select(0);
            _nodes.ScrollToItem(item);
        }

        ShowNode(node);
        UpdateHighlights();
    }

    private void ShowNode(Node node)
    {
        var gold = Hex(HarnessTheme.GOLD);
        var dim = Hex(HarnessTheme.DIM);
        var text = new StringBuilder();

        text.Append($"[b][color=#{gold}]{Escape(node.Name)}[/color][/b]  [color=#{dim}]{Escape(node.GetType().Name)}[/color]\n");
        text.Append($"[color=#{dim}]{Escape(node.IsInsideTree() ? node.GetPath().ToString() : "Not in the tree")}[/color]\n\n");

        if (!_surveyById.ContainsKey(node.GetInstanceId()))
        {
            text.Append($"Hidden from rites: it's in the [b]{Rite.IGNORE_GROUP}[/b] group, under a node that is, or internal to its parent. No rite acts on it.");
            _nodeDetails.Text = text.ToString();
            return;
        }

        var rites = Rite.Diagnostics.Touching(node);
        if (rites.Count == 0) text.Append($"[color=#{dim}]No rite acts on this node. Rites that act on the game as a whole (input, tree changes) aren't listed per node.[/color]");
        else text.Append($"[b]{rites.Count}[/b] rite{(rites.Count == 1 ? "" : "s")} act on it:\n\n");

        foreach (var rite in rites)
        {
            var here = rite.FiresFor(node);
            text.Append($"[color=#{gold}]{Escape(rite.Name)}[/color]  [color=#{dim}]{Escape(rite.Kinds)}{(rite.Muted ? ", muted" : "")}[/color]\n");
            if (rite.Description is { } description) text.Append($"{Escape(description)}\n");
            text.Append($"[color=#{dim}]Fired {here} time{(here == 1 ? "" : "s")} for this node, {rite.Fires} in all.[/color]\n\n");
        }

        _nodeDetails.Text = text.ToString();
    }

    // The list sizes its scroll bar when it next draws, so following the newest line takes a frame or two.
    private void FollowLog()
    {
        if (_logFollowFrames <= 0 || !_log.IsVisibleInTree()) return;
        _logFollowFrames--;
        var scrollBar = _log.GetVScrollBar();
        scrollBar.Value = scrollBar.MaxValue;
    }

    private void RebuildLog()
    {
        _log.Clear();
        _logSeen = Math.Max(0, Rite.Diagnostics.TotalFirings - Rite.Diagnostics.LOG_CAPACITY);
        AppendLog();
    }

    private void AppendLog()
    {
        var firings = Rite.Diagnostics.FiringsSince(_logSeen);
        _logSeen = Rite.Diagnostics.TotalFirings;
        if (firings.Count == 0) return;

        var scrollBar = _log.GetVScrollBar();
        var atBottom = _log.ItemCount == 0 || scrollBar.Value + scrollBar.Page >= scrollBar.MaxValue - 1;
        var filter = _logFilter.Text.Trim();

        foreach (var firing in firings)
        {
            if (_hidePerFrame.ButtonPressed && IsPerFrame(firing.Rite)) continue;

            var node = firing.Node;
            var target = node is null ? "" : $"  {ShortPath(node)}";
            var line = $"{(firing.Timestamp - Rite.Diagnostics.Epoch) / (double)Stopwatch.Frequency,9:0.000}  {firing.Rite.Name}{target}";
            if (filter.Length > 0 && !line.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

            var index = _log.AddItem(line);
            _log.SetItemMetadata(index, firing.NodeId);
            _log.SetItemTooltipEnabled(index, false);
        }

        while (_log.ItemCount > LOG_LIMIT) _log.RemoveItem(0);
        if (atBottom) _logFollowFrames = 3;
    }

    private RiteInfo? RiteAt(TreeItem? item) => item is null ? null : _riteItems[item.GetMetadata(0).AsInt32()].Rite;

    private static Label DimLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", HarnessTheme.DIM);
        return label;
    }

    private static LineEdit Filter(string placeholder) => new() { PlaceholderText = placeholder, ClearButtonEnabled = true, ContextMenuEnabled = false };

    // No tooltips anywhere: each one is a popup node, and adding it would show up to the game's OnTreeChanged rites.
    private static Tree NewTree(int columns) => new()
    {
        Columns = columns,
        HideRoot = true,
        ColumnTitlesVisible = true,
        SelectMode = Tree.SelectModeEnum.Row,
        AutoTooltip = false,
        SizeFlagsVertical = Control.SizeFlags.ExpandFill
    };

    private static VSplitContainer NewSplit(string key)
    {
        var split = new VSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };

        // An offset from the default split, which the container clamps to its own height on any screen.
        if (HarnessPrefs.Has(LAYOUT, $"split_{key}")) split.SplitOffsets = [HarnessPrefs.Get(LAYOUT, $"split_{key}", 0).AsInt32()];
        split.DragEnded += () => HarnessPrefs.Set(LAYOUT, $"split_{key}", split.SplitOffsets is [var offset, ..] ? offset : 0);
        return split;
    }

    private static ItemList NewList() => new()
    {
        AutoHeight = false,
        CustomMinimumSize = new Vector2(0, 120),
        SizeFlagsVertical = Control.SizeFlags.ExpandFill
    };

    private static RichTextLabel Details() => new()
    {
        BbcodeEnabled = true,
        FitContent = false,
        ScrollActive = true,
        SelectionEnabled = false,
        CustomMinimumSize = new Vector2(0, 160),
        SizeFlagsVertical = Control.SizeFlags.ExpandFill
    };
}
