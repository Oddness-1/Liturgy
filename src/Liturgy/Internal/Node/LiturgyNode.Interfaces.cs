using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

/// <summary>
/// Rites on interface members. A node implementing the interface runs its own implementation (or the interface's
/// default body) when it emits the rite's signal itself, becomes ready, processes, or receives input.
/// </summary>
internal sealed partial class LiturgyNode
{
    /// <summary>
    /// The signal each signal rite stands for, when it's declared on an interface and the node is its own source.
    /// </summary>
    private static readonly Dictionary<Type, string> INTERFACE_SIGNALS = new()
    {
        [typeof(OnAnimationFinished)] = "animation_finished",
        [typeof(OnAnimationStarted)] = "animation_started",
        [typeof(OnAreaEntered)] = "area_entered",
        [typeof(OnAreaExited)] = "area_exited",
        [typeof(OnAudioFinished)] = "finished",
        [typeof(OnBodyEntered)] = "body_entered",
        [typeof(OnBodyExited)] = "body_exited",
        [typeof(OnButtonDown)] = "button_down",
        [typeof(OnButtonPressed)] = "pressed",
        [typeof(OnButtonToggled)] = "toggled",
        [typeof(OnButtonUp)] = "button_up",
        [typeof(OnCanceled)] = "canceled",
        [typeof(OnCloseRequested)] = "close_requested",
        [typeof(OnColorChanged)] = "color_changed",
        [typeof(OnConfirmed)] = "confirmed",
        [typeof(OnDirSelected)] = "dir_selected",
        [typeof(OnDragEnded)] = "drag_ended",
        [typeof(OnDragStarted)] = "drag_started",
        [typeof(OnFileSelected)] = "file_selected",
        [typeof(OnFocusEntered)] = "focus_entered",
        [typeof(OnFocusExited)] = "focus_exited",
        [typeof(OnGuiInput)] = "gui_input",
        [typeof(OnIdPressed)] = "id_pressed",
        [typeof(OnItemActivated)] = "item_activated",
        [typeof(OnItemSelected)] = "item_selected",
        [typeof(OnMetaClicked)] = "meta_clicked",
        [typeof(OnMinimumSizeChanged)] = "minimum_size_changed",
        [typeof(OnMouseEntered)] = "mouse_entered",
        [typeof(OnMouseExited)] = "mouse_exited",
        [typeof(OnPopupHide)] = "popup_hide",
        [typeof(OnResized)] = "resized",
        [typeof(OnScreenEntered)] = "screen_entered",
        [typeof(OnScreenExited)] = "screen_exited",
        [typeof(OnScrollEnded)] = "scroll_ended",
        [typeof(OnScrollStarted)] = "scroll_started",
        [typeof(OnSortChildren)] = "sort_children",
        [typeof(OnTabChanged)] = "tab_changed",
        [typeof(OnTextChanged)] = "text_changed",
        [typeof(OnTextSubmitted)] = "text_submitted",
        [typeof(OnTimeout)] = "timeout",
        [typeof(OnValueChanged)] = "value_changed",
        [typeof(OnVisibilityChanged)] = "visibility_changed"
    };

    private sealed class InterfaceRites
    {
        public List<SignalRite>? Signals;
        public readonly List<InstanceCall> Ready = [];
        public readonly List<InstanceCall> Process = [];
        public readonly List<InstanceCall> PhysicsProcess = [];
        public readonly List<InstanceCall> Input = [];
        public readonly List<InstanceCall> UnhandledInput = [];
        public readonly List<InstanceCall> UnhandledKeyInput = [];

        public bool HasInput => Input.Count > 0 || UnhandledInput.Count > 0 || UnhandledKeyInput.Count > 0;
    }

    /// <summary>
    /// A lifecycle rite (ready, process, input) on an interface member, resolved for one implementing type.
    /// <c>Parameter</c> is the delta or input event type the method takes, or null for none.
    /// </summary>
    private sealed record InstanceCall(MethodInfo Method, string MethodName, int Priority, HashSet<string>? Excludes, Type? Parameter)
    {
        public void Invoke(Node node, object? argument)
        {
            if (Excludes is not null && PathMatchesAnyExclude(node.GetPath().ToString(), Excludes)) return;
            Method.Invoke(node, BindingFlags.DoNotWrapExceptions, null, Parameter is null ? [] : [argument], CultureInfo.InvariantCulture);
        }
    }

    private enum InterfaceRiteKind { Signal, Ready, Process, PhysicsProcess, Input, UnhandledInput, UnhandledKeyInput }

    /// <summary>
    /// Rite-bearing members per interface (generic interfaces by their definition).
    /// </summary>
    private readonly Dictionary<Type, List<(MethodInfo Method, Attribute Attribute)>> _interfaceMembers = [];
    private readonly Dictionary<Type, InterfaceRites?> _interfaceRitesByType = [];

    // Interface input rites, per node, in priority order. Arrays are rebuilt when membership changes.
    private readonly Dictionary<ulong, (Node Node, InterfaceRites Rites)> _interfaceInputNodes = [];
    private (Node Node, InstanceCall Call)[] _interfaceInput = [];
    private (Node Node, InstanceCall Call)[] _interfaceUnhandledInput = [];
    private (Node Node, InstanceCall Call)[] _interfaceUnhandledKeyInput = [];

    private readonly HashSet<ulong> _pendingInterfaceReady = [];

    private bool HasInterfaceRites => _interfaceMembers.Count > 0;

    private void SetupInterfaceRites()
    {
        foreach (var method in InterfaceHookMethods)
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";

            foreach (var attr in method.GetCustomAttributes().Where(IsRiteAttribute))
            {
                if (ClassifyInterfaceRite(attr) is null)
                {
                    LiturgyReport.AddMethodError(methodName, attr.GetType().Name, "Isn't supported on interface members; declare it static instead.");
                    continue;
                }

                if (ReadRiteFilters(attr).Target is not null)
                    LiturgyReport.AddConsoleWarning($"[Liturgy] {methodName}: Target is ignored on interface rites; they fire on each implementing node. Use Exclude to skip nodes.");

                var owner = method.DeclaringType!;
                if (owner.IsGenericType) owner = owner.GetGenericTypeDefinition();

                if (!_interfaceMembers.TryGetValue(owner, out var members))
                {
                    members = [];
                    _interfaceMembers[owner] = members;
                }

                members.Add((method, attr));

                var (_, exclude, priority) = ReadRiteFilters(attr);
                LiturgyReport.AddControlHook($"{attr.GetType().Name} (interface)", methodName, null, exclude, priority, owner.Name);
            }
        }
    }

    private static bool IsRiteAttribute(Attribute attr) => attr.GetType().Assembly == typeof(LiturgyNode).Assembly && attr is not SuppressWarnings;

    private static InterfaceRiteKind? ClassifyInterfaceRite(Attribute attr) => attr switch
    {
        OnReady => InterfaceRiteKind.Ready,
        OnProcess => InterfaceRiteKind.Process,
        OnPhysicsProcess => InterfaceRiteKind.PhysicsProcess,
        OnInput => InterfaceRiteKind.Input,
        OnUnhandledInput => InterfaceRiteKind.UnhandledInput,
        OnUnhandledKeyInput => InterfaceRiteKind.UnhandledKeyInput,
        OnSignal => InterfaceRiteKind.Signal,
        _ when INTERFACE_SIGNALS.ContainsKey(attr.GetType()) => InterfaceRiteKind.Signal,
        _ => null
    };

    private InterfaceRites? InterfaceRitesFor(Type type)
    {
        if (!HasInterfaceRites) return null;
        if (_interfaceRitesByType.TryGetValue(type, out var cached)) return cached;

        InterfaceRites? rites = null;

        foreach (var implemented in type.GetInterfaces())
        {
            var definition = implemented.IsGenericType ? implemented.GetGenericTypeDefinition() : implemented;
            if (!_interfaceMembers.TryGetValue(definition, out var members)) continue;

            foreach (var (declared, attr) in members)
            {
                // A generic interface's member, closed over this implementation's type arguments.
                var method = implemented.IsGenericType ? (MethodInfo)MethodBase.GetMethodFromHandle(declared.MethodHandle, implemented.TypeHandle)! : declared;

                rites ??= new InterfaceRites();
                AddInterfaceRite(rites, method, attr);
            }
        }

        if (rites is not null)
        {
            rites.Signals = rites.Signals?.OrderBy(rite => rite.Priority).ToList();
            foreach (var calls in new[] { rites.Ready, rites.Process, rites.PhysicsProcess, rites.Input, rites.UnhandledInput, rites.UnhandledKeyInput })
            {
                var ordered = calls.OrderBy(call => call.Priority).ToList();
                calls.Clear();
                calls.AddRange(ordered);
            }
        }

        _interfaceRitesByType[type] = rites;
        return rites;
    }

    private static void AddInterfaceRite(InterfaceRites rites, MethodInfo method, Attribute attr)
    {
        var (_, exclude, priority) = ReadRiteFilters(attr);
        var excludes = ParseExcludeSet(exclude);
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
        var hookName = $"{attr.GetType().Name} (interface)";

        switch (ClassifyInterfaceRite(attr))
        {
            case InterfaceRiteKind.Signal:
                rites.Signals ??= [];
                rites.Signals.Add(new SignalRite
                {
                    HookName = hookName,
                    Method = method,
                    Signal = attr is OnSignal onSignal ? onSignal.Signal : INTERFACE_SIGNALS[attr.GetType()],
                    Emitter = attr is OnIdPressed ? node => node is MenuButton menuButton ? menuButton.GetPopup() : node : null,
                    Excludes = excludes,
                    Priority = priority,
                    OnInstance = true
                });
                break;

            case InterfaceRiteKind.Ready:
                if (ValidateInstanceCall(method, methodName, hookName, null, out _)) rites.Ready.Add(new InstanceCall(method, methodName, priority, excludes, null));
                break;

            case InterfaceRiteKind.Process:
            case InterfaceRiteKind.PhysicsProcess:
                if (!ValidateInstanceCall(method, methodName, hookName, typeof(double), out var delta)) break;
                var frameCall = new InstanceCall(method, methodName, priority, excludes, delta);
                (attr is OnProcess ? rites.Process : rites.PhysicsProcess).Add(frameCall);
                break;

            case InterfaceRiteKind.Input:
            case InterfaceRiteKind.UnhandledInput:
            case InterfaceRiteKind.UnhandledKeyInput:
                if (!ValidateInstanceCall(method, methodName, hookName, typeof(InputEvent), out var eventType)) break;
                var inputCall = new InstanceCall(method, methodName, priority, excludes, eventType);
                (attr switch { OnInput => rites.Input, OnUnhandledInput => rites.UnhandledInput, _ => rites.UnhandledKeyInput }).Add(inputCall);
                break;
        }
    }

    /// <summary>
    /// Lifecycle rites take nothing, or one parameter of the given type (delta, or an input event type that filters events).
    /// </summary>
    private static bool ValidateInstanceCall(MethodInfo method, string methodName, string hookName, Type? allowed, out Type? parameter)
    {
        parameter = null;
        var parameters = method.GetParameters();

        if (parameters.Length == 0) return true;

        if (parameters.Length == 1 && allowed is not null && allowed.IsAssignableFrom(parameters[0].ParameterType))
        {
            parameter = parameters[0].ParameterType;
            return true;
        }

        var expected = allowed is null ? "no parameters" : $"no parameters or one {allowed.Name}";
        LiturgyReport.AddMethodError(methodName, hookName, $"Must take {expected}.");
        return false;
    }

    private void AttachInterfaceLifecycle(Node node, ulong id, InterfaceRites rites, bool initial)
    {
        if (rites.Ready.Count > 0)
        {
            // Like _Ready: once per node. Nodes already ready when Liturgy starts get it now; a node re-entering the tree doesn't.
            if (!node.IsNodeReady())
            {
                if (_pendingInterfaceReady.Add(id))
                {
                    node.Connect(Node.SignalName.Ready, Callable.From(() =>
                    {
                        _pendingInterfaceReady.Remove(id);
                        foreach (var call in rites.Ready) call.Invoke(node, null);
                    }), (uint)ConnectFlags.OneShot);
                }
            }
            else if (initial)
            {
                foreach (var call in rites.Ready) call.Invoke(node, null);
            }
        }

        if (rites.Process.Count > 0 || rites.PhysicsProcess.Count > 0) AttachInterfaceProcess(node, rites);

        if (rites.HasInput && _interfaceInputNodes.TryAdd(id, (node, rites))) RebuildInterfaceInput();
    }

    private static void AttachInterfaceProcess(Node node, InterfaceRites rites)
    {
        var process = rites.Process.Count > 0;
        var physics = rites.PhysicsProcess.Count > 0;

        var hook = ProcessHook.GetOrCreate(node, process, physics);
        if (hook.InterfaceRitesAttached) return;
        hook.InterfaceRitesAttached = true;

        // Godot turns processing on for a script that overrides _Process; an interface rite stands in for that override.
        if (process && !node.IsProcessing()) node.SetProcess(true);
        if (physics && !node.IsPhysicsProcessing()) node.SetPhysicsProcess(true);

        if (process) hook.InstanceProcessed += delta => { foreach (var call in rites.Process) call.Invoke(node, delta); };
        if (physics) hook.InstancePhysicsProcessed += delta => { foreach (var call in rites.PhysicsProcess) call.Invoke(node, delta); };
    }

    private bool IsInterfaceInputNode(ulong id) => _interfaceInputNodes.ContainsKey(id);

    private void DetachInterfaceInput(ulong id)
    {
        if (_interfaceInputNodes.Remove(id)) RebuildInterfaceInput();
    }

    private void RebuildInterfaceInput()
    {
        _interfaceInput = Flatten(rites => rites.Input);
        _interfaceUnhandledInput = Flatten(rites => rites.UnhandledInput);
        _interfaceUnhandledKeyInput = Flatten(rites => rites.UnhandledKeyInput);
        return;

        (Node, InstanceCall)[] Flatten(Func<InterfaceRites, List<InstanceCall>> select) => _interfaceInputNodes.Values
            .SelectMany(entry => select(entry.Rites).Select(call => (entry.Node, call)))
            .OrderBy(pair => pair.call.Priority)
            .ToArray();
    }

    private static void DispatchInterfaceInput((Node Node, InstanceCall Call)[] entries, InputEvent @event)
    {
        foreach (var (node, call) in entries)
        {
            if (!IsInstanceValid(node) || !node.IsInsideTree() || !node.CanProcess()) continue;
            if (call.Parameter is not null && !call.Parameter.IsInstanceOfType(@event)) continue;
            call.Invoke(node, @event);
        }
    }
}
