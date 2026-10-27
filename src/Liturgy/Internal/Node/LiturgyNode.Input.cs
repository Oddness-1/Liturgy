using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

internal sealed partial class LiturgyNode
{
    private void SetupInputCallbacks()
    {
        SetupInputHook<OnInput>(_inputCallbacks, attr => attr.Priority, attr => attr.WhilePaused, "OnInput");
        SetupInputHook<OnUnhandledInput>(_unhandledInputCallbacks, attr => attr.Priority, attr => attr.WhilePaused, "OnUnhandledInput");
        SetupInputHook<OnUnhandledKeyInput>(_unhandledKeyInputCallbacks, attr => attr.Priority, attr => attr.WhilePaused, "OnUnhandledKeyInput");
        UpdateInputRelay();
    }

    private void SetupInputHook<TAttr>(
        List<(Action<InputEvent> Callback, bool WhilePaused)> callbacks,
        Func<TAttr, int> getPriority,
        Func<TAttr, bool> getWhilePaused,
        string attrName) where TAttr : Attribute
    {
        var methods = ScanForMethods<TAttr>();
        var validEntries = new List<(MethodInfo Method, int Priority, bool WhilePaused)>();

        foreach (var (method, attr) in methods)
        {
            if (!ValidateInputMethod(method, attrName))
                continue;

            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            var priority = getPriority(attr);
            var parameters = method.GetParameters();
            var typeFilter = parameters.Length > 0 ? parameters[0].ParameterType.Name : null;
            
            // Report to markdown log
            switch (attrName)
            {
                case "OnInput":
                    LiturgyReport.AddOnInputHook(methodName, priority, typeFilter);
                    break;
                case "OnUnhandledInput":
                    LiturgyReport.AddOnUnhandledInputHook(methodName, priority, typeFilter);
                    break;
                case "OnUnhandledKeyInput":
                    LiturgyReport.AddOnUnhandledKeyInputHook(methodName, priority, typeFilter);
                    break;
            }

            validEntries.Add((method, priority, getWhilePaused(attr)));
        }

        foreach (var (method, _, whilePaused) in validEntries.OrderBy(e => e.Priority))
        {
            var callback = CreateInputCallback(method);
            if (callback is not null)
            {
                callbacks.Add((callback, whilePaused));
            }
        }
    }

    private static bool ValidateInputMethod(MethodInfo method, string attrName)
    {
        var parameters = method.GetParameters();
        var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";

        if (parameters.Length > 1)
        {
            LiturgyReport.AddMethodError(methodName, attrName, "Must have zero or one parameter.");
            return false;
        }

        if (parameters.Length == 1)
        {
            var paramType = parameters[0].ParameterType;
            if (!typeof(InputEvent).IsAssignableFrom(paramType))
            {
                LiturgyReport.AddInvalidParameterError(
                    methodName, attrName,
                    parameters[0].Name ?? "parameters[0]", paramType,
                    "InputEvent (or derived type like InputEventKey, InputEventMouse, etc.)");
                return false;
            }
        }

        return true;
    }

    private static Action<InputEvent>? CreateInputCallback(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var paramType = parameters.Length > 0 ? parameters[0].ParameterType : null;
        return DelegateFactory.CreateCallback<InputEvent>(method, paramType);
    }

    /// <summary>
    /// Asks the input relay for each kind of input that has a static or interface rite, and for no other.
    /// </summary>
    private void UpdateInputRelay() => _inputRelay?.Want(
        _inputCallbacks.Count > 0 || _interfaceInputCount.Input > 0,
        _unhandledInputCallbacks.Count > 0 || _interfaceInputCount.UnhandledInput > 0,
        _unhandledKeyInputCallbacks.Count > 0 || _interfaceInputCount.UnhandledKeyInput > 0);

    internal void DispatchInput(InputEvent @event)
    {
        DispatchStaticInput(_inputCallbacks, @event);
        DispatchInterfaceInput(InterfaceInput.Input, @event);
    }

    internal void DispatchUnhandledInput(InputEvent @event)
    {
        DispatchStaticInput(_unhandledInputCallbacks, @event);
        DispatchInterfaceInput(InterfaceInput.UnhandledInput, @event);
    }

    internal void DispatchUnhandledKeyInput(InputEvent @event)
    {
        DispatchStaticInput(_unhandledKeyInputCallbacks, @event);
        DispatchInterfaceInput(InterfaceInput.UnhandledKeyInput, @event);
    }

    // The relay always processes; a rite without WhilePaused runs only when the Liturgy node itself could, as it did before.
    private void DispatchStaticInput(List<(Action<InputEvent> Callback, bool WhilePaused)> callbacks, InputEvent @event)
    {
        if (callbacks.Count == 0) return;

        var canProcess = CanProcess();
        foreach (var (callback, whilePaused) in callbacks)
        {
            if (whilePaused || canProcess) RiteCall.Run(callback, @event);
        }
    }
}
