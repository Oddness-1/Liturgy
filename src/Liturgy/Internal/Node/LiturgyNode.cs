using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
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
    
    // Callbacks for input hooks, run from the input relay so that WhilePaused ones still get input while the tree is paused
    private readonly List<(Action<InputEvent> Callback, bool WhilePaused)> _inputCallbacks = [];
    private readonly List<(Action<InputEvent> Callback, bool WhilePaused)> _unhandledInputCallbacks = [];
    private readonly List<(Action<InputEvent> Callback, bool WhilePaused)> _unhandledKeyInputCallbacks = [];
    private InputRelay? _inputRelay;
    
    // Callbacks for scene tree hooks
    private readonly List<Action<Node>> _nodeAddedCallbacks = [];
    private readonly List<Action<Node>> _nodeRemovedCallbacks = [];
    private readonly List<Action> _treeChangedCallbacks = [];
    
    // OnReady RootReady mode: callbacks for specific targeted nodes
    private readonly Dictionary<string, List<Action<Node>>> _onReadyTargetedCallbacks = new();

    // OnReady RootReady mode: target paths by the name of the node they end in, to rule out added nodes cheaply
    private readonly Dictionary<StringName, List<string>> _onReadyTargetNames = new();
    
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
    
    // Nodes waiting for their ready for PerNode OnReady rites, by id; each leaves the set when it's ready.
    private readonly HashSet<ulong> _pendingPerNodeReady = [];

    public override void _Ready()
    {
        Instance = this;
        InitializeDebugSetting();

        _root = GetTree().Root;
        GetTree().NodeRemoved += OnNodeRemovedForListeners;

        // Counting, timing and the harness are for editor runs; an exported game calls rites directly.
        Rite.Diagnostics.IsEnabled = Harness.IsWanted();
        RiteInfo.RootId = _root.GetInstanceId();
        GetTree().NodeAdded += OnNodeAddedForShield;
        GetTree().NodeRemoved += OnNodeRemovedForShield;

        // Initialize report (only when Debug is set and running from the editor)
        LiturgyReport.Reset();
        LiturgyReport.SetNodesInScene(CountNodesInTree(_root));

        // Before the rites are wired, so building it is never seen by them.
        if (Rite.Diagnostics.IsEnabled) AddChild(new Harness());

        // First child, so the harness (a later sibling) still sees input before any rite; it starts with no input on.
        _inputRelay = InputRelay.Create(this);
        AddChild(_inputRelay);
        MoveChild(_inputRelay, 0);

        // Each category is set up on its own, so a failure in one is reported and the rest still work.
        Action[] setups =
        [
            SetupAssignNodeEntries,
            SetupOnReadyCallbacks,
            SetupFrameCallbacks,
            SetupInputCallbacks,
            SetupSceneTreeCallbacks,
            SetupButtonCallbacks,
            SetupControlCallbacks,
            SetupFocusAndMouseCallbacks,
            SetupVisibilityCallbacks,
            SetupAnimationCallbacks,
            SetupPhysicsCallbacks,
            SetupTimerCallbacks,
            SetupPopupCallbacks,
            SetupAudioCallbacks,
            SetupMiscControlCallbacks,
            SetupSignalCallbacks
        ];

        foreach (var setup in setups)
        {
            try { setup(); }
            catch (Exception exception) { GD.PushError($"[Liturgy] {setup.Method.Name} failed; those rites won't run. {exception}"); }
        }

        // Generate report (debug mode only)
        LiturgyReport.Generate();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
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
    private static List<MethodInfo>? _interfaceHookMethods;

    /// <summary>
    /// Every static method in a user assembly that carries a Liturgy attribute, collected once.
    /// </summary>
    private static List<MethodInfo> HookMethods
    {
        get
        {
            if (_hookMethods is null) (_hookMethods, _interfaceHookMethods) = CollectHookMethods();
            return _hookMethods;
        }
    }

    /// <summary>
    /// Every instance member of an interface that carries a Liturgy attribute. Implementing nodes run them.
    /// </summary>
    private static List<MethodInfo> InterfaceHookMethods
    {
        get
        {
            if (_interfaceHookMethods is null) (_hookMethods, _interfaceHookMethods) = CollectHookMethods();
            return _interfaceHookMethods;
        }
    }

    private static List<MethodEntry<TAttr>> ScanForMethods<TAttr>() where TAttr : Attribute
    {
        var entries = new List<MethodEntry<TAttr>>();

        foreach (var method in HookMethods)
        {
            foreach (var attribute in method.GetCustomAttributes<TAttr>()) entries.Add(new MethodEntry<TAttr>(method, attribute));
        }

        return entries;
    }

    private static (List<MethodInfo> Static, List<MethodInfo> Interface) CollectHookMethods()
    {
        const BindingFlags ALL_DECLARED = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        var methods = new List<MethodInfo>();
        var interfaceMethods = new List<MethodInfo>();

        foreach (var type in GetUserTypes())
        {
            foreach (var method in type.GetMethods(ALL_DECLARED))
            {
                if (!HasHookAttribute(method)) continue;

                if (IsOpenGenericClass(type))
                {
                    GD.PushError($"[Liturgy] {type.FullName}.{method.Name}: rites can't be declared in a generic class, which has no type arguments to call them with. Skipping.");
                    continue;
                }

                if (method.IsStatic) methods.Add(method);
                else if (type.IsInterface) interfaceMethods.Add(method);
                else
                {
                    GD.PushWarning($"[Liturgy] {type.FullName}.{method.Name} must be static, or an interface member. Skipping.");
                    continue;
                }

                Rite.Diagnostics.Register(method);
            }
        }

        return (methods, interfaceMethods);
    }

    /// <summary>
    /// A class (or a class nested in one) with unbound type parameters. Generic interfaces are fine: each implementation closes them.
    /// </summary>
    private static bool IsOpenGenericClass(Type type) => type.ContainsGenericParameters && !type.IsInterface;

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
    /// Starts from what's loaded and follows references, loading any not yet loaded: a library the game references
    /// may not load until its code first runs, which can be after Liturgy starts.
    /// </summary>
    private static IEnumerable<Assembly> GetUserAssemblies()
    {
        var liturgyName = typeof(LiturgyNode).Assembly.GetName().Name;
        var seen = new HashSet<string>();
        var pending = new Queue<Assembly>(AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && !IsFrameworkAssembly(assembly.GetName())));

        while (pending.TryDequeue(out var assembly))
        {
            if (!seen.Add(assembly.FullName ?? assembly.GetName().Name ?? "")) continue;

            var references = assembly.GetReferencedAssemblies();
            if (references.Any(reference => reference.Name == liturgyName)) yield return assembly;

            foreach (var reference in references)
            {
                if (IsFrameworkAssembly(reference) || seen.Contains(reference.FullName)) continue;
                if (LoadReference(assembly, reference) is { } loaded) pending.Enqueue(loaded);
            }
        }
    }

    private static Assembly? LoadReference(Assembly referencer, AssemblyName reference)
    {
        try
        {
            // Through the referencing assembly's load context, so the library resolves Liturgy to this same copy.
            var context = AssemblyLoadContext.GetLoadContext(referencer) ?? AssemblyLoadContext.Default;
            return context.LoadFromAssemblyName(reference);
        }
        catch (Exception) { return null; }
    }

    private static bool IsFrameworkAssembly(AssemblyName name) => name.Name is { } simple && (
        simple.StartsWith("System", StringComparison.Ordinal) ||
        simple.StartsWith("Microsoft", StringComparison.Ordinal) ||
        simple is "mscorlib" or "netstandard" or "GodotSharp" or "GodotSharpEditor" or "GodotPlugins" or "GodotTools" or "Liturgy");

    private static List<Type>? _userTypes;

    // Walked once and kept: both the rite scan and the AssignNode scan read it.
    private static List<Type> GetUserTypes()
    {
        if (_userTypes is not null) return _userTypes;

        _userTypes = [];
        foreach (var assembly in GetUserAssemblies())
        {
            try { _userTypes.AddRange(assembly.GetTypes()); }
            catch (ReflectionTypeLoadException exception) { _userTypes.AddRange(exception.Types.OfType<Type>()); }
        }

        return _userTypes;
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
