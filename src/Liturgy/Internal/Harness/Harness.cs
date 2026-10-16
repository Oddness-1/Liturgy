using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

/// <summary>
/// The harness: an overlay of every rite in the running game, opened with Alt+~ in editor runs. It lives under
/// /root/Liturgy, so no rite ever sees it, and while it's open it takes all keyboard and mouse input.
/// </summary>
internal sealed partial class Harness : Node
{
    private const double REFRESH_SECONDS = 0.5;
    private const double SURVEY_SECONDS = 1.0;
    private const int LOG_LIMIT = 500;
    private const int NODE_LIST_LIMIT = 500;
    private const float PANEL_WIDTH = 540;
    private const float MIN_PANEL_WIDTH = 360;
    private const int RITES_TAB = 0;
    private const int NODES_TAB = 1;
    private const int ASSIGNED_TAB = 2;
    private const string LAYOUT = "layout";
    private const string SORT = "sort";

    private Window _window = null!;
    private HarnessOverlay _overlay = null!;
    private PanelContainer _panel = null!;
    private Control _grip = null!;
    private bool _gripDragging;
    private float _panelWidth = PANEL_WIDTH;
    private Label _stats = null!;
    private TabContainer _tabs = null!;

    private LineEdit _riteFilter = null!;
    private Tree _rites = null!;
    private RichTextLabel _riteDetails = null!;
    private ItemList _riteNodes = null!;
    private readonly List<(RiteInfo Rite, TreeItem Item)> _riteItems = [];
    private Sorting _riteSort = new(1, false);

    private Button _pick = null!;
    private CheckBox _onlyWithRites = null!;
    private Tree _nodes = null!;
    private RichTextLabel _nodeDetails = null!;
    private readonly Dictionary<ulong, TreeItem> _nodeItems = [];
    private readonly HashSet<ulong> _collapsed = [];
    private string _nodeShape = "";
    private Sorting? _nodeSort;

    private Tree _assigned = null!;
    private RichTextLabel _assignedDetails = null!;
    private readonly List<(AssignmentInfo Assignment, TreeItem Item)> _assignedItems = [];
    private Sorting _assignedSort = new(0, false);
    private AssignmentInfo? _selectedAssignment;

    private CheckBox _logPaused = null!;
    private CheckBox _hidePerFrame = null!;
    private LineEdit _logFilter = null!;
    private ItemList _log = null!;
    private long _logSeen;
    private int _logFollowFrames;

    private HarnessGreeting? _greeting;

    private List<LiturgyNode.SurveyedNode> _survey = [];
    private Dictionary<ulong, LiturgyNode.SurveyedNode> _surveyById = [];
    private Dictionary<RiteInfo, int> _touchCounts = [];
    private RiteInfo? _selectedRite;
    private ulong _selectedNodeId;

    private double _sinceRefresh;
    private double _sinceSurvey;
    private long _firingsAtRefresh;
    private long _refreshedAt = Stopwatch.GetTimestamp();
    private double _firesPerSecond;

    private bool _toggleHeld;
    private bool _pausedBefore;
    private Input.MouseModeEnum _mouseModeBefore;
    private bool _pausesGame;
    private bool _detached;

    /// <summary>
    /// Passed when launching the game (after <c>--</c>, or in Project Settings > Editor > Run > Main Run Args), runs
    /// it with no harness and no counting, for profiling.
    /// </summary>
    public const string QUIET_ARGUMENT = "--liturgy-quiet";

    /// <summary>
    /// The harness, and the counting behind it, run in editor runs unless <c>[assembly: Rite.Settings(Harness =
    /// false)]</c> or <see cref="QUIET_ARGUMENT"/> turns them off.
    /// </summary>
    public static bool IsWanted() =>
        OS.HasFeature("editor") &&
        !AllSettings().Any(settings => !settings.Harness) &&
        !OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).Contains(QUIET_ARGUMENT);

    private static IEnumerable<Settings> AllSettings() => AppDomain.CurrentDomain.GetAssemblies()
        .Where(assembly => !assembly.IsDynamic)
        .Select(assembly => assembly.GetCustomAttribute<Settings>())
        .OfType<Settings>();

    public override void _Ready()
    {
        Name = "Harness";
        ProcessMode = ProcessModeEnum.Always;
        _pausesGame = AllSettings().Any(settings => settings.HarnessPausesGame);
        RestoreLayout();

        BuildWindow();
        BuildRitesTab();
        BuildNodesTab();
        BuildAssignedTab();
        BuildLogTab();
        BuildGuideTab();

        // Built now, hidden, so showing it later adds nothing to the tree for OnTreeChanged rites to see.
        if (HarnessGreeting.IsDue())
        {
            _greeting = new HarnessGreeting();
            _greeting.OpenGuideRequested += () => Open(guide: true);
            AddChild(_greeting);
            GetTree().CreateTimer(1.0, processAlways: true).Timeout += () => _greeting.Appear(GetTree().Root);
        }
    }

    public override void _Process(double delta)
    {
        // Polled rather than read from events, so a game that handles every key can't swallow it.
        var held = Input.IsPhysicalKeyPressed(Key.Quoteleft) && Input.IsKeyPressed(Key.Alt);
        if (held && !_toggleHeld)
        {
            if (_window.Visible) Close();
            else Open(guide: false);
        }

        _toggleHeld = held;
        if (!_window.Visible) return;

        FitToScreen();

        // A popup the game opens would take the keyboard back; keep it while the harness is open.
        if (!_detached && !_window.HasFocus()) _window.GrabFocus();
        _pick.SetPressedNoSignal(_overlay.Picking);
        FollowLog();

        _sinceSurvey += delta;
        if (_sinceSurvey >= SURVEY_SECONDS) TakeSurvey();

        _sinceRefresh += delta;
        if (_sinceRefresh >= REFRESH_SECONDS) Refresh();
    }

    private void Open(bool guide)
    {
        _greeting?.Dismiss();

        _mouseModeBefore = Input.MouseMode;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        if (_pausesGame)
        {
            _pausedBefore = GetTree().Paused;
            GetTree().Paused = true;
        }

        FitToScreen();
        _window.Show();
        _window.GrabFocus();

        if (guide) _tabs.CurrentTab = _tabs.GetTabCount() - 1;

        ListRites();
        TakeSurvey();
        RebuildLog();
        Refresh();
    }

    private void Close()
    {
        _overlay.Picking = false;
        _window.Hide();

        if (Input.MouseMode == Input.MouseModeEnum.Visible) Input.MouseMode = _mouseModeBefore;
        if (_pausesGame) GetTree().Paused = _pausedBefore;
    }

    private void FitToScreen()
    {
        // A project that turns off embedded subwindows gets the harness as its own OS window instead of an overlay.
        var detached = !GetTree().Root.GuiEmbedSubwindows;
        if (detached != _detached) Detach(detached);
        if (detached) return;

        var root = GetTree().Root;
        var visible = root.GetVisibleRect().Size;
        var gameUnitsPerUnit = HarnessTheme.UiScale() / HarnessTheme.GameScale(root);
        var screen = visible / gameUnitsPerUnit;

        // The window spans the game's view in game units but sizes its contents by the screen, so the harness
        // reads the same whatever the game's stretch settings. Checked every frame: a game can rescale at any time.
        var size = new Vector2I(Mathf.CeilToInt(visible.X), Mathf.CeilToInt(visible.Y));
        if (_window.Size != size || _window.Position != Vector2I.Zero) (_window.Position, _window.Size) = (Vector2I.Zero, size);
        if (!Mathf.IsEqualApprox(_window.ContentScaleFactor, gameUnitsPerUnit)) _window.ContentScaleFactor = gameUnitsPerUnit;
        _overlay.ScreenScale = 1f / gameUnitsPerUnit;

        // The saved width is in harness units, which follow the screen; clamping it here keeps a width chosen on a
        // bigger monitor from running off a smaller one.
        var width = Mathf.Clamp(_panelWidth, Mathf.Min(MIN_PANEL_WIDTH, screen.X * 0.9f), screen.X * 0.9f);
        if (!Mathf.IsEqualApprox(_panel.OffsetRight, _panel.OffsetLeft + width)) _panel.OffsetRight = _panel.OffsetLeft + width;
        _grip.Position = new Vector2(_panel.OffsetRight - 4, _panel.OffsetTop);
        _grip.Size = new Vector2(8, Mathf.Max(0, screen.Y + _panel.OffsetBottom - _panel.OffsetTop));
    }

    private void Detach(bool detached)
    {
        _detached = detached;
        _overlay.Picking = false;
        _overlay.Visible = !detached;
        _grip.Visible = !detached;
        _pick.Visible = !detached;

        _window.Borderless = !detached;
        _window.Transparent = !detached;
        _window.TransparentBg = !detached;
        _window.Unresizable = !detached;
        _window.AlwaysOnTop = !detached;
        _window.ContentScaleFactor = detached ? HarnessTheme.UiScale() : 1;

        if (detached)
        {
            _window.Title = "Liturgy harness";
            _window.Size = (Vector2I)(new Vector2(PANEL_WIDTH + 32, 760) * _window.ContentScaleFactor);
            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return;
        }

        _panel.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        (_panel.OffsetLeft, _panel.OffsetTop, _panel.OffsetBottom) = (16, 16, -16);
    }

    private void TakeSurvey()
    {
        _sinceSurvey = 0;
        if (LiturgyNode.Instance is not { } liturgy) return;

        _survey = liturgy.Survey();
        _surveyById = _survey.ToDictionary(surveyed => surveyed.Id);
        _touchCounts = Rite.Diagnostics.Rites.ToDictionary(rite => rite, _ => 0);
        foreach (var surveyed in _survey)
        {
            foreach (var rite in surveyed.Rites) _touchCounts[rite]++;
        }

        UpdateNodesTree();
        if (_selectedRite is not null) ShowRite(_selectedRite);
        UpdateHighlights();
    }

    private void Refresh()
    {
        _sinceRefresh = 0;

        var now = Stopwatch.GetTimestamp();
        var seconds = (now - _refreshedAt) / (double)Stopwatch.Frequency;
        if (seconds > 0) _firesPerSecond = (Rite.Diagnostics.TotalFirings - _firingsAtRefresh) / seconds;
        _firingsAtRefresh = Rite.Diagnostics.TotalFirings;
        _refreshedAt = now;

        var running = Rite.Diagnostics.Rites.Count(rite => rite.Running);
        var touched = _survey.Count(surveyed => surveyed.Rites.Count > 0);
        _stats.Text = $"{running} rites running  ·  {touched} nodes touched  ·  {_firesPerSecond:0} fires/s";

        UpdateRitesTree();
        UpdateAssignedTree();
        if (_selectedRite is not null && _tabs.CurrentTab == RITES_TAB) ShowRite(_selectedRite, nodes: false);
        if (!_logPaused.ButtonPressed) AppendLog();
    }

    private void UpdateHighlights()
    {
        var onRites = _tabs.CurrentTab == RITES_TAB;
        _overlay.Highlighted = onRites && _selectedRite is not null ? NodesTouchedBy(_selectedRite).ToList() : [];
        _overlay.SelectedId = _tabs.CurrentTab switch
        {
            NODES_TAB => _selectedNodeId,
            ASSIGNED_TAB when _selectedAssignment?.Node is { } node && IsInstanceValid(node) => node.GetInstanceId(),
            _ => 0
        };
    }

    private IEnumerable<Node> NodesTouchedBy(RiteInfo rite) => _survey.Where(surveyed => surveyed.Rites.Contains(rite) && IsInstanceValid(surveyed.Node)).Select(surveyed => surveyed.Node);

    /// <summary>
    /// A rite that acts on the game as a whole (input, tree changes, global frames, root ready) rather than on nodes.
    /// </summary>
    private static bool IsGameWide(RiteInfo rite) => !rite.OnInterface && rite.Attributes.All(attribute => attribute switch
    {
        OnInput or OnUnhandledInput or OnUnhandledKeyInput or OnNodeAdded or OnNodeRemoved or OnTreeChanged => true,
        OnProcess process => process.Mode == FrameMode.Global && process.Target is null,
        OnPhysicsProcess process => process.Mode == FrameMode.Global && process.Target is null,
        OnReady ready => ready.Mode == ReadyMode.Immediate || (ready.Mode == ReadyMode.RootReady && ready.Target is null),
        _ => false
    });

    private static bool IsPerFrame(RiteInfo rite) => rite.Attributes.Any(attribute => attribute is OnProcess or OnPhysicsProcess);

    private static string Escape(string text) => text.Replace("[", "[lb]");

    private static string Hex(Color color) => color.ToHtml(false);

    private static string ShortPath(Node node) => node.IsInsideTree() ? ShortPath(node.GetPath().ToString()) : $"{node.Name} (out of the tree)";

    private static string ShortPath(string path) => path.StartsWith("/root/", StringComparison.Ordinal) ? path["/root/".Length..] : path;

    private static string Duration(double milliseconds) => milliseconds switch
    {
        0 => "",
        < 10 => $"{milliseconds:0.00} ms",
        < 1000 => $"{milliseconds:0.#} ms",
        _ => $"{milliseconds / 1000:0.0} s"
    };

    private static string Ago(double? seconds) => seconds switch
    {
        null => "never",
        < 1 => "now",
        < 60 => $"{seconds:0}s",
        < 3600 => $"{seconds / 60:0}m",
        _ => $"{seconds / 3600:0}h"
    };
}
