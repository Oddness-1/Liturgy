using System;
using System.Linq;
using Godot;

namespace Liturgy.Internal;

/// <summary>
/// Lets a tree's columns be resized by dragging the line between two headings; a double click there puts them back.
/// The widths are remembered as shares of the tree's width, so they carry over to another panel width or monitor.
/// </summary>
internal sealed class HarnessColumns
{
    private const string SECTION = "columns";
    private const float GRAB = 5f;
    private const float MIN_WIDTH = 24f;
    private const float RATIO_UNIT = 8f;

    private readonly Tree _tree;
    private readonly string _key;
    private readonly bool[] _expands;
    private readonly int[] _defaultMinimums;
    private readonly int[] _defaultRatios;

    private float[]? _shares;
    private float _sharedWidth = -1;
    private int _dragging = -1;
    private float _dragFrom;
    private float[] _dragWidths = [];

    /// <summary>
    /// Call once the tree's columns are set up: what they are then is what a double click restores.
    /// </summary>
    public HarnessColumns(Tree tree, string key)
    {
        _tree = tree;
        _key = key;
        _expands = Enumerable.Range(0, tree.Columns).Select(tree.IsColumnExpanding).ToArray();
        _defaultMinimums = Enumerable.Range(0, tree.Columns).Select(column => (int)tree.GetColumnWidth(column)).ToArray();
        _defaultRatios = Enumerable.Range(0, tree.Columns).Select(tree.GetColumnExpandRatio).ToArray();

        var saved = HarnessPrefs.Get(SECTION, key, Variant.From(Array.Empty<float>())).AsFloat32Array();
        if (saved.Length == tree.Columns && saved.All(share => share > 0)) _shares = saved;

        tree.GuiInput += OnGuiInput;
        tree.Resized += Apply;
    }

    private void OnGuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion when _dragging >= 0:
            {
                var (left, right) = (_dragging, _dragging + 1);
                var shift = Mathf.Clamp(motion.Position.X - _dragFrom, MIN_WIDTH - _dragWidths[left], _dragWidths[right] - MIN_WIDTH);
                var widths = (float[])_dragWidths.Clone();
                widths[left] += shift;
                widths[right] -= shift;
                SetWidths(widths);
                _tree.AcceptEvent();
                break;
            }

            case InputEventMouseMotion motion:
                _tree.MouseDefaultCursorShape = BoundaryAt(motion.Position) >= 0 ? Control.CursorShape.Hsize : Control.CursorShape.Arrow;
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true, DoubleClick: true } click when BoundaryAt(click.Position) >= 0:
                Reset();
                _tree.AcceptEvent();
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when BoundaryAt(press.Position) is >= 0 and var boundary:
                _dragging = boundary;
                _dragFrom = press.Position.X;
                _dragWidths = Widths();
                _tree.AcceptEvent();
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when _dragging >= 0:
                _dragging = -1;
                var current = Widths();
                var total = current.Sum();
                _shares = current.Select(width => width / total).ToArray();
                _sharedWidth = ContentWidth();
                HarnessPrefs.Set(SECTION, _key, Variant.From(_shares));
                _tree.AcceptEvent();
                break;
        }
    }

    // Re-applied whenever the tree changes width, so the columns keep their shares of it.
    private void Apply()
    {
        if (_shares is null || _dragging >= 0) return;

        var width = ContentWidth();
        if (width <= 0 || Mathf.IsEqualApprox(width, _sharedWidth)) return;

        _sharedWidth = width;
        SetWidths(_shares.Select(share => share * width).ToArray());
    }

    private void Reset()
    {
        _dragging = -1;
        _shares = null;
        _sharedWidth = -1;
        HarnessPrefs.Set(SECTION, _key, Variant.From(Array.Empty<float>()));

        for (var column = 0; column < _expands.Length; column++)
        {
            if (_expands[column]) _tree.SetColumnExpandRatio(column, _defaultRatios[column]);
            else _tree.SetColumnCustomMinimumWidth(column, _defaultMinimums[column]);
        }
    }

    // Fixed columns take their width outright; expanding ones share what's left by ratio. The ratios are kept well
    // under the width left over, since Tree expands nothing once their total exceeds it.
    private void SetWidths(float[] widths)
    {
        for (var column = 0; column < widths.Length; column++)
        {
            var width = Mathf.Max(MIN_WIDTH, widths[column]);
            if (_expands[column]) _tree.SetColumnExpandRatio(column, Mathf.Max(1, Mathf.RoundToInt(width / RATIO_UNIT)));
            else _tree.SetColumnCustomMinimumWidth(column, Mathf.RoundToInt(width));
        }
    }

    private float[] Widths() => Enumerable.Range(0, _tree.Columns).Select(column => (float)_tree.GetColumnWidth(column)).ToArray();

    // The width the columns share: the tree's, less its panel's margins and the scroll bar when it shows.
    private float ContentWidth()
    {
        var panel = _tree.GetThemeStylebox("panel");
        var width = _tree.Size.X - panel.GetMargin(Side.Left) - panel.GetMargin(Side.Right);
        if (_tree.GetChildren(includeInternal: true).OfType<VScrollBar>().FirstOrDefault() is { Visible: true } bar) width -= bar.Size.X;
        return width;
    }

    // The boundary under the point: the index of the column to its left, or -1. Only the headings take a drag.
    private int BoundaryAt(Vector2 point)
    {
        var panel = _tree.GetThemeStylebox("panel");
        var top = panel.GetMargin(Side.Top);
        if (point.Y < top || point.Y > top + HeaderHeight()) return -1;

        var x = panel.GetMargin(Side.Left) - _tree.GetScroll().X;
        for (var column = 0; column < _tree.Columns - 1; column++)
        {
            x += _tree.GetColumnWidth(column);
            if (Mathf.Abs(point.X - x) <= GRAB) return column;
        }

        return -1;
    }

    private float HeaderHeight()
    {
        var box = _tree.GetThemeStylebox("title_button_normal");
        var font = _tree.GetThemeFont("title_button_font");
        var size = _tree.GetThemeFontSize("title_button_font_size");
        return box.GetMinimumSize().Y + font.GetHeight(size);
    }
}
