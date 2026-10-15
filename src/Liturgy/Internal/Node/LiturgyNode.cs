using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

/// <summary>
/// The node Liturgy attaches to <c>/root</c> at startup. It scans for Liturgy attributes and invokes them.
/// </summary>
internal sealed partial class LiturgyNode : Node
{
    internal const string NODE_NAME = "Liturgy";

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

        // Initialize report (only when Debug is set and running from the editor)
        LiturgyReport.Reset();
        LiturgyReport.SetNodesInScene(CountNodesInTree(_root));

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
        LiturgyReport.Generate();
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
        // Reports are a development aid: exported builds never write them, whatever the setting says.
        if (OS.HasFeature("editor") && AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetCustomAttribute<Settings>()).OfType<Settings>().Any(attr => attr.Debug))
        {
            LiturgyReport.IsEnabled = true;
            GD.Print("[Liturgy] Debug mode enabled.");
            return;
        }

        LiturgyReport.IsEnabled = false;
    }

    private readonly record struct MethodEntry<TAttr>(MethodInfo Method, TAttr Attribute) where TAttr : Attribute;

    private static List<MethodInfo>? _hookMethods;

    /// <summary>
    /// Every static method in a user assembly that carries a Liturgy attribute, collected once.
    /// Instance methods with a hook attribute are reported and skipped.
    /// </summary>
    private static List<MethodInfo> HookMethods => _hookMethods ??= CollectHookMethods();

    private static List<MethodEntry<TAttr>> ScanForMethods<TAttr>() where TAttr : Attribute
    {
        var entries = new List<MethodEntry<TAttr>>();

        foreach (var method in HookMethods)
        {
            if (method.GetCustomAttribute<TAttr>() is { } attribute) entries.Add(new MethodEntry<TAttr>(method, attribute));
        }

        return entries;
    }

    private static List<MethodInfo> CollectHookMethods()
    {
        const BindingFlags ALL_DECLARED = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        var methods = new List<MethodInfo>();

        foreach (var type in GetUserTypes())
        {
            foreach (var method in type.GetMethods(ALL_DECLARED))
            {
                if (!HasHookAttribute(method)) continue;

                if (!method.IsStatic)
                {
                    GD.PushWarning($"[Liturgy] {type.FullName}.{method.Name} must be static. Skipping.");
                    continue;
                }

                methods.Add(method);
            }
        }

        return methods;
    }

    private static bool HasHookAttribute(MethodInfo method)
    {
        try
        {
            // CustomAttributeData reads metadata without constructing every attribute on every method.
            return method.CustomAttributes.Any(data => data.AttributeType.Assembly == typeof(LiturgyNode).Assembly && data.AttributeType != typeof(SuppressWarnings));
        }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// Assemblies that reference Liturgy. Any assembly that declares a hook must, since the hook's attribute lives here.
    /// </summary>
    private static IEnumerable<Assembly> GetUserAssemblies()
    {
        var liturgyName = typeof(LiturgyNode).Assembly.GetName().Name;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;
            if (assembly.GetReferencedAssemblies().Any(reference => reference.Name == liturgyName)) yield return assembly;
        }
    }

    private static IEnumerable<Type> GetUserTypes()
    {
        foreach (var assembly in GetUserAssemblies())
        {
            Type[] types;

            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { types = exception.Types.OfType<Type>().ToArray(); }

            foreach (var type in types) yield return type;
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

        // Fast path: most parents have no internal children, so skip allocating the child array.
        if (parent.GetChildCount(includeInternal: true) == parent.GetChildCount(includeInternal: false)) return false;

        // GetChildren(includeInternal: false) returns only regular children
        // If the node isn't in that list, it's internal
        return !parent.GetChildren(includeInternal: false).Contains(node);
    }

    // ReSharper disable once GrammarMistakeInComment
    /// <summary>
    /// Normalizes a node path. Paths starting with '/' are absolute (for autoloads or /root itself).
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
