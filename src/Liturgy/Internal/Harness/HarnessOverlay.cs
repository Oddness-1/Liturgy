using System;
using System.Collections.Generic;
using Godot;

namespace Liturgy.Internal;

/// <summary>
/// Fills the harness window behind its panel: outlines the nodes being looked at, picks a node under the mouse, and
/// takes every click the game would otherwise get.
/// </summary>
internal sealed partial class HarnessOverlay : Control
{
    private const int MAX_OUTLINES = 400;

    public event Action<Node>? Picked;
    public event Action? CloseRequested;

    /// <summary>
    /// Game units to harness units: the harness draws at screen pixels while the game may be stretched.
    /// </summary>
    public float ScreenScale { get; set; } = 1f;

    public bool Picking { get; set; }
    public ulong SelectedId { get; set; }
    public IReadOnlyList<Node> Highlighted { get; set; } = [];

    private Node? _hovered;

    public HarnessOverlay()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;

        GetViewport().SetInputAsHandled();
        if (Picking) Picking = false;
        else CloseRequested?.Invoke();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!Picking) return;

        switch (@event)
        {
            case InputEventMouseMotion motion:
                _hovered = HarnessPicker.ControlAt(GetTree().Root, motion.Position / ScreenScale);
                break;

            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                Picking = false;
                if (HarnessPicker.ControlAt(GetTree().Root, click.Position / ScreenScale) is { } picked) Picked?.Invoke(picked);
                AcceptEvent();
                break;

            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                Picking = false;
                AcceptEvent();
                break;
        }
    }

    public override void _Draw()
    {
        var count = 0;
        foreach (var node in Highlighted)
        {
            if (++count > MAX_OUTLINES) break;
            if (IsInstanceValid(node)) Outline(node, HarnessTheme.BLUE, 1.5f, label: false);
        }

        if (SelectedId != 0 && InstanceFromId(SelectedId) is Node selected) Outline(selected, HarnessTheme.GOLD, 2.5f, label: true);

        if (!Picking) return;

        if (_hovered is not null && IsInstanceValid(_hovered)) Outline(_hovered, HarnessTheme.GOLD, 2f, label: true);
        else _hovered = null;

        var hint = "Click a control to inspect it. Right-click or Esc to stop.";
        var font = HarnessTheme.Theme.DefaultFont;
        var size = font.GetStringSize(hint, HorizontalAlignment.Left, -1, HarnessTheme.FONT_SIZE);
        var at = new Vector2(Size.X - size.X - 24, 24);
        DrawRect(new Rect2(at - new Vector2(8, size.Y), size + new Vector2(16, 12)), HarnessTheme.BACKGROUND);
        DrawString(font, at, hint, HorizontalAlignment.Left, -1, HarnessTheme.FONT_SIZE, HarnessTheme.GOLD);
    }

    private void Outline(Node node, Color color, float width, bool label)
    {
        if (HarnessPicker.ScreenShape(node) is not { } shape) return;

        var points = new Vector2[shape.Length + 1];
        for (var i = 0; i < shape.Length; i++) points[i] = shape[i] * ScreenScale;
        points[^1] = points[0];

        if (shape.Length == 1) DrawCircle(points[0], 5f, color with { A = 0.5f });
        else
        {
            DrawColoredPolygon(points[..^1], color with { A = 0.12f });
            DrawPolyline(points, color, width);
        }

        if (!label) return;

        var font = HarnessTheme.Theme.DefaultFont;
        var text = $"{node.Name}  ({node.GetType().Name})";
        var size = font.GetStringSize(text, HorizontalAlignment.Left, -1, HarnessTheme.FONT_SIZE);
        var at = points[0] + new Vector2(0, -6);
        if (at.Y - size.Y < 0) at.Y = points[0].Y + size.Y + 6;
        DrawRect(new Rect2(at - new Vector2(4, size.Y), size + new Vector2(8, 6)), HarnessTheme.BACKGROUND);
        DrawString(font, at, text, HorizontalAlignment.Left, -1, HarnessTheme.FONT_SIZE, color);
    }
}
