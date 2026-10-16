using Godot;

namespace Liturgy.Internal;

/// <summary>
/// The harness's look, built in code so the game's own theme never reaches it.
/// </summary>
internal static class HarnessTheme
{
    public static readonly Color BACKGROUND = new(0.07f, 0.075f, 0.095f, 0.94f);
    public static readonly Color SURFACE = new(0.11f, 0.115f, 0.14f, 1f);
    public static readonly Color RAISED = new(0.16f, 0.165f, 0.2f, 1f);
    public static readonly Color BORDER = new(0.24f, 0.25f, 0.3f, 1f);
    public static readonly Color TEXT = new(0.91f, 0.89f, 0.84f, 1f);
    public static readonly Color DIM = new(0.56f, 0.58f, 0.64f, 1f);
    public static readonly Color GOLD = new(0.86f, 0.69f, 0.29f, 1f);
    public static readonly Color BLUE = new(0.44f, 0.7f, 0.86f, 1f);
    public static readonly Color RED = new(0.89f, 0.42f, 0.38f, 1f);

    public const int FONT_SIZE = 14;

    // The screen height at which one harness unit is one pixel; taller screens scale the harness up to match.
    private const float REFERENCE_HEIGHT = 1080f;

    private static Theme? _theme;
    private static Font? _sans;
    private static Font? _mono;

    public static Theme Theme => _theme ??= Build();

    // Its own font: the project's custom font would otherwise reach it through ThemeDB.FallbackFont.
    public static Font Sans => _sans ??= new SystemFont { FontNames = ["Segoe UI", "Inter", "Noto Sans", "Ubuntu", "DejaVu Sans", "Helvetica Neue", "Arial", "sans-serif"] };

    public static Font Mono => _mono ??= new SystemFont { FontNames = ["Cascadia Mono", "Consolas", "JetBrains Mono", "DejaVu Sans Mono", "Menlo", "monospace"] };

    /// <summary>
    /// Screen pixels per harness unit, from the screen rather than the game, so the harness reads the same size on
    /// a 1080p laptop and a 4K monitor whatever resolution the game renders at.
    /// </summary>
    public static float UiScale()
    {
        var height = DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen()).Y;
        return Mathf.Max(1f, Mathf.Snapped(height / REFERENCE_HEIGHT, 0.25f));
    }

    /// <summary>
    /// Screen pixels per unit of the root viewport, under any stretch mode.
    /// </summary>
    public static float GameScale(Window root)
    {
        var visible = root.GetVisibleRect().Size;
        var pixels = (Vector2)root.Size;
        var scale = Mathf.Min(pixels.X / visible.X, pixels.Y / visible.Y);
        return scale > 0 && !float.IsInfinity(scale) ? scale : 1f;
    }

    private static Theme Build()
    {
        var theme = new Theme { DefaultFont = Sans, DefaultFontSize = FONT_SIZE };

        var panel = Box(BACKGROUND, border: GOLD, borderWidth: 1, margin: 12, radius: 6);
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetStylebox("panel", "Panel", panel);

        foreach (var type in new[] { "Label", "Button", "CheckBox", "CheckButton", "LineEdit", "ItemList", "Tree", "RichTextLabel", "TabContainer", "TooltipLabel" })
            theme.SetColor("font_color", type, TEXT);

        theme.SetColor("default_color", "RichTextLabel", TEXT);
        theme.SetFont("bold_font", "RichTextLabel", new FontVariation { BaseFont = Sans, VariationEmbolden = 0.6f });
        theme.SetFont("italics_font", "RichTextLabel", new FontVariation { BaseFont = Sans, VariationTransform = new Transform2D(1, 0.2f, 0, 1, 0, 0) });

        var check = CheckIcon(true);
        var uncheck = CheckIcon(false);
        foreach (var type in new[] { "CheckBox", "Tree" })
        {
            theme.SetIcon("checked", type, check);
            theme.SetIcon("unchecked", type, uncheck);
        }
        theme.SetStylebox("normal", "RichTextLabel", Box(SURFACE, margin: 8, radius: 4));
        theme.SetStylebox("focus", "RichTextLabel", new StyleBoxEmpty());

        var button = Box(RAISED, border: BORDER, borderWidth: 1, margin: 6, radius: 4);
        theme.SetStylebox("normal", "Button", button);
        theme.SetStylebox("hover", "Button", Box(RAISED.Lightened(0.08f), border: GOLD, borderWidth: 1, margin: 6, radius: 4));
        theme.SetStylebox("pressed", "Button", Box(GOLD.Darkened(0.55f), border: GOLD, borderWidth: 1, margin: 6, radius: 4));
        theme.SetStylebox("hover_pressed", "Button", Box(GOLD.Darkened(0.45f), border: GOLD, borderWidth: 1, margin: 6, radius: 4));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetColor("font_hover_color", "Button", TEXT);
        theme.SetColor("font_pressed_color", "Button", GOLD);

        foreach (var type in new[] { "CheckBox", "CheckButton" })
        {
            theme.SetStylebox("normal", type, Box(new Color(0, 0, 0, 0), margin: 4));
            theme.SetStylebox("hover", type, Box(RAISED, margin: 4, radius: 4));
            theme.SetStylebox("pressed", type, Box(new Color(0, 0, 0, 0), margin: 4));
            theme.SetStylebox("hover_pressed", type, Box(RAISED, margin: 4, radius: 4));
            theme.SetStylebox("focus", type, new StyleBoxEmpty());
            theme.SetColor("font_hover_color", type, TEXT);
            theme.SetColor("font_pressed_color", type, TEXT);
        }

        theme.SetStylebox("normal", "LineEdit", Box(SURFACE, border: BORDER, borderWidth: 1, margin: 6, radius: 4));
        theme.SetStylebox("focus", "LineEdit", Box(new Color(0, 0, 0, 0), border: GOLD, borderWidth: 1, margin: 6, radius: 4));
        theme.SetColor("font_placeholder_color", "LineEdit", DIM);
        theme.SetColor("caret_color", "LineEdit", GOLD);

        theme.SetStylebox("panel", "TabContainer", Box(new Color(0, 0, 0, 0), margin: 0));
        theme.SetStylebox("tab_selected", "TabContainer", Box(SURFACE, border: GOLD, borderWidth: 0, margin: 8, radius: 4, topBorder: 2));
        theme.SetStylebox("tab_unselected", "TabContainer", Box(new Color(0, 0, 0, 0), margin: 8));
        theme.SetStylebox("tab_hovered", "TabContainer", Box(RAISED, margin: 8, radius: 4));
        theme.SetStylebox("tab_focus", "TabContainer", new StyleBoxEmpty());
        theme.SetColor("font_selected_color", "TabContainer", GOLD);
        theme.SetColor("font_unselected_color", "TabContainer", DIM);
        theme.SetColor("font_hovered_color", "TabContainer", TEXT);

        var list = Box(SURFACE, margin: 4, radius: 4);
        theme.SetStylebox("panel", "Tree", list);
        theme.SetStylebox("focus", "Tree", new StyleBoxEmpty());
        theme.SetStylebox("selected", "Tree", Box(GOLD.Darkened(0.6f), radius: 3));
        theme.SetStylebox("selected_focus", "Tree", Box(GOLD.Darkened(0.5f), radius: 3));
        theme.SetStylebox("hovered", "Tree", Box(RAISED, radius: 3));
        theme.SetStylebox("cursor", "Tree", new StyleBoxEmpty());
        theme.SetStylebox("cursor_unfocused", "Tree", new StyleBoxEmpty());
        theme.SetStylebox("title_button_normal", "Tree", Box(RAISED, margin: 4));
        theme.SetStylebox("title_button_hover", "Tree", Box(RAISED, margin: 4));
        theme.SetStylebox("title_button_pressed", "Tree", Box(RAISED, margin: 4));
        theme.SetColor("title_button_color", "Tree", DIM);
        theme.SetColor("font_selected_color", "Tree", TEXT);
        theme.SetColor("guide_color", "Tree", BORDER);
        theme.SetColor("relationship_line_color", "Tree", BORDER);
        theme.SetConstant("v_separation", "Tree", 3);
        theme.SetConstant("draw_relationship_lines", "Tree", 1);

        theme.SetStylebox("panel", "ItemList", list);
        theme.SetStylebox("focus", "ItemList", new StyleBoxEmpty());
        theme.SetStylebox("selected", "ItemList", Box(GOLD.Darkened(0.6f), radius: 3));
        theme.SetStylebox("selected_focus", "ItemList", Box(GOLD.Darkened(0.5f), radius: 3));
        theme.SetStylebox("hovered", "ItemList", Box(RAISED, radius: 3));
        theme.SetStylebox("cursor", "ItemList", new StyleBoxEmpty());
        theme.SetStylebox("cursor_unfocused", "ItemList", new StyleBoxEmpty());
        theme.SetColor("font_selected_color", "ItemList", TEXT);
        theme.SetColor("font_hovered_color", "ItemList", TEXT);

        theme.SetStylebox("panel", "TooltipPanel", Box(SURFACE, border: GOLD, borderWidth: 1, margin: 8, radius: 4));

        theme.SetStylebox("grabber", "VScrollBar", Box(BORDER, radius: 3));
        theme.SetStylebox("grabber_highlight", "VScrollBar", Box(DIM, radius: 3));
        theme.SetStylebox("grabber_pressed", "VScrollBar", Box(GOLD, radius: 3));
        theme.SetStylebox("scroll", "VScrollBar", Box(new Color(0, 0, 0, 0), margin: 2));
        theme.SetStylebox("grabber", "HScrollBar", Box(BORDER, radius: 3));
        theme.SetStylebox("grabber_highlight", "HScrollBar", Box(DIM, radius: 3));
        theme.SetStylebox("grabber_pressed", "HScrollBar", Box(GOLD, radius: 3));
        theme.SetStylebox("scroll", "HScrollBar", Box(new Color(0, 0, 0, 0), margin: 2));

        // Always shown, so the panes read as resizable.
        theme.SetIcon("grabber", "VSplitContainer", Bar(28, 3, BORDER));
        theme.SetConstant("separation", "VSplitContainer", 12);
        theme.SetConstant("minimum_grab_thickness", "VSplitContainer", 12);
        theme.SetConstant("autohide", "VSplitContainer", 0);

        theme.SetConstant("separation", "VBoxContainer", 8);
        theme.SetConstant("separation", "HBoxContainer", 8);

        return theme;
    }

    // The default check icons are dark on dark here; these match the harness.
    private static ImageTexture CheckIcon(bool on)
    {
        const int SIZE = 16;
        var image = Image.CreateEmpty(SIZE, SIZE, false, Image.Format.Rgba8);
        for (var x = 0; x < SIZE; x++)
        {
            for (var y = 0; y < SIZE; y++)
            {
                var edge = x is 1 or SIZE - 2 || y is 1 or SIZE - 2;
                var inside = x is > 1 and < SIZE - 2 && y is > 1 and < SIZE - 2;
                var color = x is 0 or SIZE - 1 || y is 0 or SIZE - 1 ? new Color(0, 0, 0, 0)
                    : edge ? (on ? GOLD : DIM)
                    : inside && on && x is > 3 and < SIZE - 4 && y is > 3 and < SIZE - 4 ? GOLD
                    : new Color(0, 0, 0, 0);
                image.SetPixel(x, y, color);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static ImageTexture Bar(int width, int height, Color color)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }

    public static StyleBoxFlat Box(Color background, Color? border = null, int borderWidth = 0, int margin = 0, int radius = 0, int topBorder = -1)
    {
        var box = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border ?? BORDER,
            ContentMarginLeft = margin,
            ContentMarginRight = margin,
            ContentMarginTop = margin,
            ContentMarginBottom = margin
        };

        box.SetBorderWidthAll(borderWidth);
        if (topBorder >= 0) box.BorderWidthTop = topBorder;
        box.SetCornerRadiusAll(radius);
        return box;
    }
}
