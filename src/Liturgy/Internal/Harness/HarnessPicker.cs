using Godot;

namespace Liturgy.Internal;

/// <summary>
/// Where game nodes are on screen, in the root viewport's units, and which control is under a point.
/// </summary>
internal static class HarnessPicker
{
    /// <summary>
    /// The topmost visible control under <paramref name="point"/> in the root viewport, preferring one that takes
    /// the mouse over one that lets it through. Controls in other viewports aren't considered.
    /// </summary>
    public static Control? ControlAt(Window root, Vector2 point)
    {
        Control? best = null, bestAny = null;
        (int Layer, int Order) bestKey = (int.MinValue, -1), bestAnyKey = (int.MinValue, -1);
        var order = 0;

        Visit(root, 0);
        return best ?? bestAny;

        void Visit(Node node, int layer)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is LiturgyNode or Viewport) continue;

                var childLayer = layer;
                if (child is CanvasLayer canvasLayer)
                {
                    if (!canvasLayer.Visible) continue;
                    childLayer = canvasLayer.Layer;
                }

                if (child is Control control)
                {
                    if (!control.IsVisibleInTree()) continue;

                    var inside = Contains(control, point);
                    if (!inside && control.ClipContents) continue;

                    if (inside)
                    {
                        var key = (childLayer, order);
                        if (Later(key, bestAnyKey)) (bestAny, bestAnyKey) = (control, key);
                        if (control.MouseFilter != Control.MouseFilterEnum.Ignore && Later(key, bestKey)) (best, bestKey) = (control, key);
                    }
                }
                else if (child is CanvasItem { Visible: false }) continue;

                order++;
                Visit(child, childLayer);
            }
        }
    }

    /// <summary>
    /// The node's outline as corners in the root viewport, a single point for a node with no area, or null when it
    /// isn't drawn in the root viewport.
    /// </summary>
    public static Vector2[]? ScreenShape(Node node)
    {
        if (!node.IsInsideTree() || node.GetViewport() != node.GetTree().Root) return null;

        switch (node)
        {
            case Control control:
                return control.IsVisibleInTree() ? Corners(control.GetGlobalTransformWithCanvas(), new Rect2(Vector2.Zero, control.Size)) : null;

            case Sprite2D sprite:
                return sprite.IsVisibleInTree() ? Corners(sprite.GetGlobalTransformWithCanvas(), sprite.GetRect()) : null;

            case CanvasItem item:
                return item.IsVisibleInTree() ? [item.GetGlobalTransformWithCanvas().Origin] : null;

            case Node3D spatial:
                var camera = node.GetViewport().GetCamera3D();
                if (camera is null || !spatial.IsVisibleInTree() || camera.IsPositionBehind(spatial.GlobalPosition)) return null;
                return [camera.UnprojectPosition(spatial.GlobalPosition)];

            default:
                return null;
        }
    }

    private static bool Contains(Control control, Vector2 point) => new Rect2(Vector2.Zero, control.Size).HasPoint(control.GetGlobalTransformWithCanvas().AffineInverse() * point);

    private static bool Later((int Layer, int Order) key, (int Layer, int Order) than) => key.Layer > than.Layer || (key.Layer == than.Layer && key.Order > than.Order);

    private static Vector2[] Corners(Transform2D transform, Rect2 rect) =>
    [
        transform * rect.Position,
        transform * new Vector2(rect.End.X, rect.Position.Y),
        transform * rect.End,
        transform * new Vector2(rect.Position.X, rect.End.Y)
    ];
}
