using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Bootstrap.Internal;

/// <summary>
/// Generates a Markdown report of Bootstrap initialization.
/// Only runs when Debug mode is enabled via assembly attribute.
/// </summary>
internal static class BootstrapReport
{
    private const string LOGS_DIRECTORY = "res://addons/Bootstrap/Logs";
    private const int MAX_LOG_FILES = 10;
    
    private static readonly StringBuilder _report = new();
    private static readonly List<string> _warnings = [];
    
    // Tracking stats
    private static int _totalHooks;
    private static int _processHooksCreated;
    private static int _readySubscriptions;
    private static int _nodesInScene;
    
    // Hook details by category
    private static readonly List<HookEntry> _onReadyHooks = [];
    private static readonly List<HookEntry> _onProcessHooks = [];
    private static readonly List<HookEntry> _onPhysicsProcessHooks = [];
    private static readonly List<HookEntry> _onInputHooks = [];
    private static readonly List<HookEntry> _onUnhandledInputHooks = [];
    private static readonly List<HookEntry> _onUnhandledKeyInputHooks = [];
    private static readonly List<HookEntry> _onNodeAddedHooks = [];
    private static readonly List<HookEntry> _onNodeRemovedHooks = [];
    private static readonly List<HookEntry> _onTreeChangedHooks = [];
    private static readonly List<HookEntry> _controlHooks = [];
    
    // AssignNode entries
    private static readonly List<AssignNodeEntry> _assignNodeEntries = [];
    internal readonly record struct AssignNodeEntry(string Member, string Path, string NodeType);
    
    // ProcessHook attachment details
    private static readonly List<ProcessHookAttachment> _processHookAttachments = [];
    internal readonly record struct ProcessHookAttachment(string NodeType, string Path, bool Process, bool PhysicsProcess);
    internal readonly record struct HookEntry(string Method, string? Mode, string? Target, string? Exclude, int Priority, string? TypeFilter);

    public static bool IsEnabled { get; set; }

    public static void Reset()
    {
        _report.Clear();
        _warnings.Clear();
        _totalHooks = 0;
        _processHooksCreated = 0;
        _readySubscriptions = 0;
        _nodesInScene = 0;
        
        _onReadyHooks.Clear();
        _onProcessHooks.Clear();
        _onPhysicsProcessHooks.Clear();
        _onInputHooks.Clear();
        _onUnhandledInputHooks.Clear();
        _onUnhandledKeyInputHooks.Clear();
        _onNodeAddedHooks.Clear();
        _onNodeRemovedHooks.Clear();
        _onTreeChangedHooks.Clear();
        _controlHooks.Clear();
        _assignNodeEntries.Clear();
        _processHookAttachments.Clear();
    }

    public static void AddWarning(string warning)
    {
        if (!IsEnabled) return;
        _warnings.Add(warning);
    }
    
    /// <summary>
    /// Adds a warning that will be printed to the console regardless of debug mode.
    /// When debug mode is enabled, also the warning is also included in the report.
    /// </summary>
    /// <param name="consoleMessage">Short message for console output (printed in yellow).</param>
    /// <param name="reportMessage">Detailed message for the Markdown report (optional, uses consoleMessage if null).</param>
    public static void AddConsoleWarning(string consoleMessage, string? reportMessage = null)
    {
        GD.PrintRich($"[color=yellow]{consoleMessage}[/color]\n");
        if (IsEnabled) _warnings.Add(reportMessage ?? consoleMessage);
    }
    
    /// <summary>
    /// Reports a method validation error (invalid signature, wrong parameter types, etc.).
    /// Prints to the console and adds to report if enabled.
    /// </summary>
    public static void AddMethodError(string methodName, string hookName, string message)
    {
        var consoleMsg = $"[Bootstrap] {methodName}: {message} Skipping.";
        GD.PrintRich($"[color=yellow]{consoleMsg}[/color]\n");
        if (IsEnabled) _warnings.Add($"`{methodName}` ({hookName}): {message}");
    }
    
    /// <summary>
    /// Reports an invalid parameter type for a hook method.
    /// </summary>
    public static void AddInvalidParameterError(string methodName, string hookName, string paramName, Type actualType, string expectedTypes)
    {
        var message = $"Parameter '{paramName}' has invalid type '{actualType.Name}'. {hookName} accepts: {expectedTypes}.";
        AddMethodError(methodName, hookName, message);
    }

    public static void IncrementTotalHooks() => _totalHooks++;
    public static void IncrementProcessHooks() => _processHooksCreated++;
    public static void IncrementReadySubscriptions() => _readySubscriptions++;
    public static void SetNodesInScene(int count) => _nodesInScene = count;
    
    public static void AddProcessHookAttachment(string nodeType, string path, bool process, bool physicsProcess)
    {
        if (!IsEnabled) return;
        _processHookAttachments.Add(new ProcessHookAttachment(nodeType, path, process, physicsProcess));
    }

    public static void AddOnReadyHook(string method, string? mode, string? target, string? exclude, int priority, string? typeFilter)
    {
        if (!IsEnabled) return;
        _onReadyHooks.Add(new HookEntry(method, mode, target, exclude, priority, typeFilter));
        _totalHooks++;
    }

    public static void AddOnProcessHook(string method, string? mode, string? target, string? exclude, int priority, string? typeFilter)
    {
        if (!IsEnabled) return;
        _onProcessHooks.Add(new HookEntry(method, mode, target, exclude, priority, typeFilter));
        _totalHooks++;
    }

    public static void AddOnPhysicsProcessHook(string method, string? mode, string? target, string? exclude, int priority, string? typeFilter)
    {
        if (!IsEnabled) return;
        _onPhysicsProcessHooks.Add(new HookEntry(method, mode, target, exclude, priority, typeFilter));
        _totalHooks++;
    }

    public static void AddOnInputHook(string method, int priority, string? typeFilter)
    {
        if (!IsEnabled) return;
        _onInputHooks.Add(new HookEntry(method, null, null, null, priority, typeFilter));
        _totalHooks++;
    }

    public static void AddOnUnhandledInputHook(string method, int priority, string? typeFilter)
    {
        if (!IsEnabled) return;
        _onUnhandledInputHooks.Add(new HookEntry(method, null, null, null, priority, typeFilter));
        _totalHooks++;
    }

    public static void AddOnUnhandledKeyInputHook(string method, int priority, string? typeFilter)
    {
        if (!IsEnabled) return;
        _onUnhandledKeyInputHooks.Add(new HookEntry(method, null, null, null, priority, typeFilter));
        _totalHooks++;
    }

    public static void AddOnNodeAddedHook(string method, int priority, string? typeFilter)
    {
        if (!IsEnabled) return;
        _onNodeAddedHooks.Add(new HookEntry(method, null, null, null, priority, typeFilter));
        _totalHooks++;
    }

    public static void AddOnNodeRemovedHook(string method, int priority, string? typeFilter)
    {
        if (!IsEnabled) return;
        _onNodeRemovedHooks.Add(new HookEntry(method, null, null, null, priority, typeFilter));
        _totalHooks++;
    }

    public static void AddOnTreeChangedHook(string method, int priority)
    {
        if (!IsEnabled) return;
        _onTreeChangedHooks.Add(new HookEntry(method, null, null, null, priority, null));
        _totalHooks++;
    }

    public static void AddControlHook(string hookName, string method, string? target, string? exclude, int priority, string? typeFilter)
    {
        if (!IsEnabled) return;
        _controlHooks.Add(new HookEntry(method, hookName, target, exclude, priority, typeFilter));
        _totalHooks++;
    }

    public static void AddAssignNodeEntry(string member, string path, string nodeType)
    {
        if (!IsEnabled) return;
        _assignNodeEntries.Add(new AssignNodeEntry(member, path, nodeType));
    }

    public static void Generate()
    {
        if (!IsEnabled) return;

        _report.Clear();
        
        // Header
        _report.AppendLine("# Bootstrap Report");
        _report.AppendLine();
        _report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _report.AppendLine();

        // Summary
        _report.AppendLine("## Summary");
        _report.AppendLine();
        _report.AppendLine("| Metric | Count |");
        _report.AppendLine("|--------|-------|");
        _report.AppendLine($"| Total hooks registered | {_totalHooks} |");
        _report.AppendLine($"| ProcessHooks created | {_processHooksCreated}{(_processHooksCreated > 50 ? " ⚠️" : "")} |");
        _report.AppendLine($"| Ready subscriptions | {_readySubscriptions}{(_readySubscriptions > 100 ? " ⚠️" : "")} |");
        _report.AppendLine($"| Nodes in scene | {_nodesInScene} |");
        _report.AppendLine();
        
        // Add a note about ProcessHooks if any were created
        if (_processHooksCreated > 0)
        {
            _report.AppendLine("> **ℹ️ About ProcessHooks:** Each PerNode `[OnProcess]` or `[OnPhysicsProcess]` creates a child node ");
            _report.AppendLine("> on matching targets. The hook checks `target.IsProcessing()` each frame and only fires callbacks ");
            _report.AppendLine("> for nodes that are actively processing. Nodes without `_Process` methods won't trigger callbacks.");
            _report.AppendLine();
        }

        // Warnings
        if (_warnings.Count > 0)
        {
            _report.AppendLine("## Warnings");
            _report.AppendLine();
            foreach (var warning in _warnings)
            {
                _report.AppendLine($"> [!] {warning}");
                _report.AppendLine();
            }
        }

        // AssignNode entries
        WriteAssignNodeSection();

        // Registered Hooks
        _report.AppendLine("## Registered Hooks");
        _report.AppendLine();

        WriteHookSection("OnReady", _onReadyHooks, hasMode: true, hasTarget: true);
        WriteHookSection("OnProcess", _onProcessHooks, hasMode: true, hasTarget: true);
        WriteHookSection("OnPhysicsProcess", _onPhysicsProcessHooks, hasMode: true, hasTarget: true);
        WriteHookSection("OnInput", _onInputHooks, hasMode: false, hasTarget: false);
        WriteHookSection("OnUnhandledInput", _onUnhandledInputHooks, hasMode: false, hasTarget: false);
        WriteHookSection("OnUnhandledKeyInput", _onUnhandledKeyInputHooks, hasMode: false, hasTarget: false);
        WriteHookSection("OnNodeAdded", _onNodeAddedHooks, hasMode: false, hasTarget: false);
        WriteHookSection("OnNodeRemoved", _onNodeRemovedHooks, hasMode: false, hasTarget: false);
        WriteHookSection("OnTreeChanged", _onTreeChangedHooks, hasMode: false, hasTarget: false);
        WriteControlHooksSection();
        WriteProcessHookAttachmentsSection();

        // Footer
        _report.AppendLine("---");
        _report.AppendLine();
        _report.AppendLine("*Bootstrap • Editor only • No impact on exported builds*");

        // Write to the file
        WriteToFile();
    }

    private static void WriteHookSection(string name, List<HookEntry> hooks, bool hasMode, bool hasTarget)
    {
        if (hooks.Count == 0) return;

        _report.AppendLine($"### {name} ({hooks.Count})");
        _report.AppendLine();

        // Build header
        var header = "| Method |";
        var separator = "|--------|";
        
        if (hasMode)
        {
            header += " Mode |";
            separator += "------|";
        }
        
        if (hasTarget)
        {
            header += " Target | Exclude |";
            separator += "--------|---------|";
        }
        
        header += " Priority | Type Filter |";
        separator += "----------|-------------|";

        _report.AppendLine(header);
        _report.AppendLine(separator);

        foreach (var hook in hooks)
        {
            var row = $"| `{hook.Method}` |";
            if (hasMode) row += $" {hook.Mode ?? "—"} |";
            if (hasTarget) row += $" {hook.Target ?? "—"} | {hook.Exclude ?? "—"} |";
            row += $" {hook.Priority} | {hook.TypeFilter ?? "—"} |";
            _report.AppendLine(row);
        }

        _report.AppendLine();
    }

    private static void WriteAssignNodeSection()
    {
        if (_assignNodeEntries.Count == 0) return;

        _report.AppendLine($"## AssignNode Entries ({_assignNodeEntries.Count})");
        _report.AppendLine();
        _report.AppendLine("Static fields/properties assigned when `/root` is ready:");
        _report.AppendLine();
        _report.AppendLine("| Member | Path | Type |");
        _report.AppendLine("|--------|------|------|");

        foreach (var entry in _assignNodeEntries)
        {
            _report.AppendLine($"| `{entry.Member}` | `{entry.Path}` | {entry.NodeType} |");
        }

        _report.AppendLine();
    }

    private static void WriteControlHooksSection()
    {
        if (_controlHooks.Count == 0) return;

        _report.AppendLine($"### Control Hooks ({_controlHooks.Count})");
        _report.AppendLine();
        _report.AppendLine("| Hook | Method | Target | Exclude | Priority | Type Filter |");
        _report.AppendLine("|------|--------|--------|---------|----------|-------------|");

        foreach (var row in _controlHooks.Select(hook => $"| {hook.Mode} | `{hook.Method}` | {hook.Target ?? "—"} | {hook.Exclude ?? "—"} | {hook.Priority} | {hook.TypeFilter ?? "—"} |")) 
            _report.AppendLine(row);

        _report.AppendLine();
    }

    private static void WriteProcessHookAttachmentsSection()
    {
        if (_processHookAttachments.Count == 0) return;

        _report.AppendLine($"## ProcessHook Attachments ({_processHookAttachments.Count})");
        _report.AppendLine();
        _report.AppendLine("Nodes that received a ProcessHook child during initial scene load:");
        _report.AppendLine();
        _report.AppendLine("| Node Type | Path | Process | Physics |");
        _report.AppendLine("|-----------|------|---------|--------|");

        foreach (var attachment in _processHookAttachments)
        {
            var process = attachment.Process ? "✓" : "—";
            var physics = attachment.PhysicsProcess ? "✓" : "—";
            _report.AppendLine($"| {attachment.NodeType} | `{attachment.Path}` | {process} | {physics} |");
        }

        _report.AppendLine();
    }

    private static void WriteToFile()
    {
        try
        {
            // Ensure that a Logs directory exists
            var logsDir = DirAccess.Open(LOGS_DIRECTORY);
            if (logsDir is null)
            {
                // Directory doesn't exist, create it
                var parentDir = DirAccess.Open("res://addons/Bootstrap");
                if (parentDir is null)
                {
                    GD.PushError($"[Bootstrap] Failed to open addon directory: {DirAccess.GetOpenError()}");
                    return;
                }
                var mkdirError = parentDir.MakeDir("Logs");
                if (mkdirError != Error.Ok && mkdirError != Error.AlreadyExists)
                {
                    GD.PushError($"[Bootstrap] Failed to create Logs directory: {mkdirError}");
                    return;
                }
            }
            
            // Clean up old logs
            CleanupOldLogs();
            
            // Generate timestamped filename
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var path = $"{LOGS_DIRECTORY}/bootstrap_report_{timestamp}.md";
            
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            
            if (file is null)
            {
                GD.PushError($"[Bootstrap] Failed to write report: {FileAccess.GetOpenError()}");
                return;
            }
            
            file.StoreString(_report.ToString());
            GD.Print($"[Bootstrap] Report generated: {path}");
        }
        catch (Exception ex) { GD.PushError($"[Bootstrap] Failed to write report: {ex.Message}"); }
    }
    
    private static void CleanupOldLogs()
    {
        try
        {
            var dir = DirAccess.Open(LOGS_DIRECTORY);
            if (dir is null) return;
            
            var logFiles = new List<string>();
            
            dir.ListDirBegin();
            var fileName = dir.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (!dir.CurrentIsDir() && fileName.StartsWith("bootstrap_report_") && fileName.EndsWith(".md"))
                {
                    logFiles.Add(fileName);
                }
                fileName = dir.GetNext();
            }
            dir.ListDirEnd();
            
            // Sort by name (which sorts by timestamp due to naming convention)
            logFiles.Sort();
            
            // Delete the oldest files if we exceed the limit (leave room for the new one)
            var filesToDelete = logFiles.Count - (MAX_LOG_FILES - 1);
            if (filesToDelete <= 0) return;
            foreach (var file in logFiles.Take(filesToDelete)) dir.Remove(file);
        }
        catch (Exception ex) { GD.PushWarning($"[Bootstrap] Failed to cleanup old logs: {ex.Message}"); }
    }
}
