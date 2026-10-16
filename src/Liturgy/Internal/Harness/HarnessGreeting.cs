using System;
using Godot;

namespace Liturgy.Internal;

/// <summary>
/// A note in the corner the first time a project runs with Liturgy, and again whenever its version changes. It never
/// takes the keyboard, and once shown it doesn't come back until the next version.
/// </summary>
/// <remarks>
/// The version last greeted is kept in the project's .godot folder: it's per machine and never committed, it goes
/// with the project, and deleting the folder just means one more hello.
/// </remarks>
internal sealed partial class HarnessGreeting : Window
{
    private const string SECTION = "greeting";
    private const string KEY = "version";
    private const int MARGIN = 16;

    public event Action? OpenGuideRequested;

    private bool _dismissed;

    public static bool IsDue() => SeenVersion() != HarnessText.VERSION;

    public HarnessGreeting()
    {
        Name = "Greeting";
        Visible = false;
        Borderless = true;
        Transparent = true;
        TransparentBg = true;
        Unresizable = true;
        Unfocusable = true;
        AlwaysOnTop = true;
        WrapControls = true;
        ProcessMode = ProcessModeEnum.Always;

        var panel = new PanelContainer { Theme = HarnessTheme.Theme };
        AddChild(panel);

        var column = new VBoxContainer();
        panel.AddChild(column);

        var title = new Label { Text = HarnessText.GreetingTitle(SeenVersion()) };
        title.AddThemeColorOverride("font_color", HarnessTheme.GOLD);
        title.AddThemeFontSizeOverride("font_size", 17);
        column.AddChild(title);

        var seen = SeenVersion();
        column.AddChild(new Label
        {
            Text = HarnessText.GreetingBody(seen),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 0)
        });

        // An update leads with what changed since the version this project last ran.
        if (seen is not null and not "" && HarnessText.ChangesSince(seen, HarnessTheme.GOLD.ToHtml(false)) is { Length: > 0 } changes)
        {
            column.AddChild(new RichTextLabel
            {
                BbcodeEnabled = true,
                Text = changes,
                ScrollActive = true,
                SelectionEnabled = false,
                CustomMinimumSize = new Vector2(380, 220)
            });
        }

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        column.AddChild(buttons);

        var guide = new Button { Text = "Show me around", FocusMode = Control.FocusModeEnum.None };
        guide.Pressed += () => OpenGuideRequested?.Invoke();
        buttons.AddChild(guide);

        var dismiss = new Button { Text = "Got it", FocusMode = Control.FocusModeEnum.None };
        dismiss.Pressed += Dismiss;
        buttons.AddChild(dismiss);
    }

    public void Appear(Window root)
    {
        if (_dismissed) return;

        var visible = root.GetVisibleRect().Size;
        var gameUnitsPerUnit = HarnessTheme.UiScale() / HarnessTheme.GameScale(root);
        ContentScaleFactor = gameUnitsPerUnit;

        Show();
        ResetSize();
        Position = new Vector2I((int)(visible.X - Size.X - MARGIN * gameUnitsPerUnit), (int)(visible.Y - Size.Y - MARGIN * gameUnitsPerUnit));

        MarkSeen();
    }

    // Hidden rather than freed: removing it would show up to the game's OnTreeChanged rites.
    public void Dismiss()
    {
        if (_dismissed) return;
        _dismissed = true;
        MarkSeen();
        Hide();
    }

    private static string? SeenVersion() => HarnessPrefs.Get(SECTION, KEY, "").AsString() is { Length: > 0 } seen ? seen : null;

    private static void MarkSeen() => HarnessPrefs.Set(SECTION, KEY, HarnessText.VERSION);
}
