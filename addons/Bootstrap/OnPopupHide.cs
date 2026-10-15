using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when a <see cref="Godot.Popup">Popup</see> is hidden.
/// </summary>
/// <remarks>
/// Parameters are optional: none, or <see cref="Godot.Popup">Popup</see> subtypes (filters by type).
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which popups invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnPopupHide]
/// private static void AnyPopupHidden() { }
/// </code>
/// <code>
/// // Any popup
/// [OnPopupHide]
/// private static void AnyPopupHidden(Popup popup) { }
/// </code>
/// <code>
/// // Specific type (filters to PopupMenu)
/// [OnPopupHide]
/// private static void PopupMenuHidden(PopupMenu menu) { }
/// </code>
/// <code>
/// // Targeted to specific popup
/// [OnPopupHide(Target = "UI/InventoryPopup")]
/// private static void InventoryPopupHidden() { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnPopupHide : Attribute
{
    /// <summary>
    /// Comma-separated node paths. Fires only for these popups (null = all).
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Comma-separated node paths to skip.
    /// </summary>
    public string? Exclude { get; set; }

    /// <summary>
    /// Execution order (lower runs first). Default is 0.
    /// </summary>
    public int Priority { get; set; }
}
