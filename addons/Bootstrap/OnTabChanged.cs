using System;

namespace Bootstrap;

/// <summary>
/// Invokes a static method when a tab is changed in a <see cref="Godot.TabContainer">TabContainer</see> or <see cref="Godot.TabBar">TabBar</see>.
/// </summary>
/// <remarks>
/// Parameters are optional: none, an <see cref="int"/> or <see cref="long"/> (tab index), a <c>TabContainer</c> or <c>TabBar</c>, or both in any order.
/// Supports <c>Target</c>, <c>Exclude</c>, <c>Priority</c> properties for fine-grained control over which controls invoke this callback.
/// </remarks>
/// <example>
/// <code>
/// // No parameters
/// [OnTabChanged]
/// private static void AnyTabChanged() { }
/// </code>
/// <code>
/// // Index only
/// [OnTabChanged]
/// private static void AnyTabChanged(int tabIndex) { }
/// </code>
/// <code>
/// // Source only (filters to TabContainer)
/// [OnTabChanged]
/// private static void TabContainerOnly(TabContainer source) { }
/// </code>
/// <code>
/// // Source only (filters to TabBar)
/// [OnTabChanged]
/// private static void TabBarOnly(TabBar source) { }
/// </code>
/// <code>
/// // Both, any order
/// [OnTabChanged]
/// private static void AnyTabChanged(int tabIndex, TabContainer source) { }
/// </code>
/// <code>
/// // Targeted to specific container
/// [OnTabChanged(Target = "UI/SettingsTabs")]
/// private static void OnSettingsTabChanged(int tabIndex) { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnTabChanged : Attribute
{
    /// <summary>
    /// Comma-separated node paths to target.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Comma-separated node paths to skip.
    /// </summary>
    public string? Exclude { get; set; }

    /// <summary>
    /// Execution order (lower runs first). Default is 0.
    /// </summary>
    public int Priority { get; set; } = 0;
}
