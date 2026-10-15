using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Godot;

namespace Bootstrap.Internal;

/// <summary>
/// Internal autoload that scans for and invokes Bootstrap attributes.
/// This class is not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public partial class Autoload : Node
{
    private Window _root = null!;
    
    // Global callbacks for frame-based hooks (no target specified)
    private readonly List<Action<Window, double>> _processGlobalCallbacks = [];
    private readonly List<Action<Window, double>> _physicsProcessGlobalCallbacks = [];
    
    // Targeted process callbacks: stored info needed to attach ProcessHooks
    private readonly List<ProcessCallbackEntry> _processTargetedEntries = [];
    private readonly List<ProcessCallbackEntry> _physicsProcessTargetedEntries = [];
    
    // Track per-entry match counts for runtime warnings (keyed by method name to survive across entries)
    private readonly Dictionary<string, int> _processEntryMatchCounts = new();
    private readonly HashSet<string> _processEntryWarned = new();
    
    // Callbacks for input hooks
    private readonly List<Action<InputEvent>> _inputCallbacks = [];
    private readonly List<Action<InputEvent>> _unhandledInputCallbacks = [];
    private readonly List<Action<InputEvent>> _unhandledKeyInputCallbacks = [];
    
    // Callbacks for scene tree hooks
    private readonly List<Action<Node>> _nodeAddedCallbacks = [];
    private readonly List<Action<Node>> _nodeRemovedCallbacks = [];
    private readonly List<Action> _treeChangedCallbacks = [];
    
    // OnReady RootReady mode: callbacks for specific targeted nodes
    private readonly Dictionary<string, List<Action<Node>>> _onReadyTargetedCallbacks = new();
    
    // OnReady RootReady mode: callbacks that fire once when /root is ready (no target)
    private readonly List<Action<Window>> _onReadyRootReadyCallbacks = [];
    
    // OnReady PerNode mode: callbacks that fire for each node
    private readonly List<(Action<Node> Callback, HashSet<string>? Excludes, Type? NodeType)> _onReadyPerNodeCallbacks = [];
    
    // Callbacks for button hooks (Tier 3)
    private readonly List<(Action<BaseButton> Callback, HashSet<string>? Excludes)> _buttonPressedGlobalCallbacks = [];
    private readonly List<(Action<BaseButton> Callback, HashSet<string>? Excludes)> _buttonUpGlobalCallbacks = [];
    private readonly List<(Action<BaseButton> Callback, HashSet<string>? Excludes)> _buttonDownGlobalCallbacks = [];
    private readonly List<(Action<bool, BaseButton> Callback, HashSet<string>? Excludes)> _buttonToggledGlobalCallbacks = [];
    
    private readonly Dictionary<string, List<(Action<BaseButton> Callback, HashSet<string>? Excludes)>> _buttonPressedTargetedCallbacks = new();
    private readonly Dictionary<string, List<(Action<BaseButton> Callback, HashSet<string>? Excludes)>> _buttonUpTargetedCallbacks = new();
    private readonly Dictionary<string, List<(Action<BaseButton> Callback, HashSet<string>? Excludes)>> _buttonDownTargetedCallbacks = new();
    private readonly Dictionary<string, List<(Action<bool, BaseButton> Callback, HashSet<string>? Excludes)>> _buttonToggledTargetedCallbacks = new();
    
    private readonly HashSet<BaseButton> _subscribedButtons = [];
    
    // Track nodes we've subscribed to for OnReady
    private readonly HashSet<Node> _subscribedForReady = [];

    public override void _Ready()
    {
        InitializeDebugSetting();
        
        _root = GetTree().Root;
        
        // Initialize report (editor only)
        BootstrapReport.Reset();
        BootstrapReport.SetNodesInScene(CountNodesInTree(_root));
        
        // Scan for AssignNode entries (executed when /root is ready)
        SetupAssignNodeEntries();
        
        // OnReady hooks (Immediate, RootReady, and PerNode modes)
        SetupOnReadyCallbacks();
        
        // Frame-based hooks
        SetupFrameCallbacks();
        
        // Input hooks
        SetupInputCallbacks();
        
        // Scene tree hooks
        SetupSceneTreeCallbacks();
        
        // Tier 3: Type-filtered signal hooks
        SetupButtonCallbacks();
        
        // Additional control hooks (LineEdit, Range, etc.)
        SetupControlCallbacks();
        
        // Focus, mouse, resize, GUI input hooks
        SetupFocusAndMouseCallbacks();
        
        // Visibility hooks (CanvasItem, Node3D)
        SetupVisibilityCallbacks();
        
        // Animation hooks
        SetupAnimationCallbacks();
        
        // Physics hooks (Area2D/3D body/area enter/exit)
        SetupPhysicsCallbacks();
        
        // Timer hooks
        SetupTimerCallbacks();
        
        // Popup and dialog hooks
        SetupPopupCallbacks();
        
        // Audio hooks
        SetupAudioCallbacks();
        
        // Misc control hooks (slider drag, scroll, container sort)
        SetupMiscControlCallbacks();
        
        // Generate report (debug mode only)
        BootstrapReport.Generate();
    }
    
    private static int CountNodesInTree(Node root)
    {
        var count = 0;
        var stack = new Stack<Node>();
        stack.Push(root);
        
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            count++;
            
            foreach (var child in node.GetChildren())
            {
                stack.Push(child);
            }
        }
        
        return count;
    }

    private static void InitializeDebugSetting()
    {
        if (AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetCustomAttribute<Settings>()).OfType<Settings>().Any(attr => attr.Debug))
        {
            BootstrapReport.IsEnabled = true;
            GD.Print("[Bootstrap] Debug mode enabled.");
            return;
        }

        BootstrapReport.IsEnabled = false;
    }

    private readonly record struct MethodEntry<TAttr>(MethodInfo Method, TAttr Attribute) where TAttr : Attribute;

    private static List<MethodEntry<TAttr>> ScanForMethods<TAttr>() where TAttr : Attribute
    {
        var entries = new List<MethodEntry<TAttr>>();

        foreach (var assembly in GetUserAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var attr = method.GetCustomAttribute<TAttr>();
                        if (attr is null) continue;

                        if (!method.IsStatic)
                        {
                            GD.PushWarning($"[Bootstrap] {type.FullName}.{method.Name} must be static. Skipping.");
                            continue;
                        }

                        entries.Add(new MethodEntry<TAttr>(method, attr));
                    }
                }
            }
            catch (ReflectionTypeLoadException) { /* Skip gracefully */ }
        }

        return entries;
    }

    private static IEnumerable<Assembly> GetUserAssemblies()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name is null
                || name.StartsWith("System", StringComparison.Ordinal)
                || name.StartsWith("Microsoft", StringComparison.Ordinal)
                || name.StartsWith("Godot", StringComparison.Ordinal)
                || name.StartsWith("GodotSharp", StringComparison.Ordinal))
            {
                continue;
            }

            yield return assembly;
        }
    }

    /// <summary>
    /// Returns true if this node is an internal child (added with InternalMode).
    /// Internal nodes are implementation details and should be excluded from user hooks.
    /// </summary>
    private static bool IsInternalNode(Node node)
    {
        var parent = node.GetParent();
        if (parent is null) return false;
        
        // GetChildren(includeInternal: false) returns only regular children
        // If the node isn't in that list, it's internal
        return !parent.GetChildren(includeInternal: false).Contains(node);
    }

    // ReSharper disable once GrammarMistakeInComment
    /// <summary>
    /// Normalizes a node path. Paths starting with '/' are absolute (for autoloads or root).
    /// Paths without leading '/' are relative to /root/.
    /// </summary>
    private static string NormalizePath(string path)
    {
        path = path.Trim();
        
        if (path.StartsWith('/'))
            return path;
        
        if (path.StartsWith("root/", StringComparison.Ordinal) || path == "root")
            return "/" + path;
        
        return "/root/" + path;
    }
}