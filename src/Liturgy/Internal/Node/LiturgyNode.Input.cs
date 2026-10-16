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
        SetupInputHook<OnInput>(_inputCallbacks, attr => attr.Priority, "OnInput");
        SetupInputHook<OnUnhandledInput>(_unhandledInputCallbacks, attr => attr.Priority, "OnUnhandledInput");
        SetupInputHook<OnUnhandledKeyInput>(_unhandledKeyInputCallbacks, attr => attr.Priority, "OnUnhandledKeyInput");
    }

    private void SetupInputHook<TAttr>(
        List<Action<InputEvent>> callbacks,
        Func<TAttr, int> getPriority,
        string attrName) where TAttr : Attribute
    {
        var methods = ScanForMethods<TAttr>();
        var validEntries = new List<(MethodInfo Method, int Priority)>();

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

            validEntries.Add((method, priority));
        }

        foreach (var (method, _) in validEntries.OrderBy(e => e.Priority))
        {
            var callback = CreateInputCallback(method);
            if (callback is not null)
            {
                callbacks.Add(callback);
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

    public override void _Input(InputEvent @event)
    {
        foreach (var callback in _inputCallbacks)
        {
            RiteCall.Run(callback, @event);
        }

        DispatchInterfaceInput(_interfaceInput, @event);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        foreach (var callback in _unhandledInputCallbacks)
        {
            RiteCall.Run(callback, @event);
        }

        DispatchInterfaceInput(_interfaceUnhandledInput, @event);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        foreach (var callback in _unhandledKeyInputCallbacks)
        {
            RiteCall.Run(callback, @event);
        }

        DispatchInterfaceInput(_interfaceUnhandledKeyInput, @event);
    }
}
