using System;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

/// <summary>
/// Creates strongly typed delegate wrappers without using dynamic.
/// Uses MakeGenericMethod at setup time to avoid DLR overhead at runtime.
/// </summary>
internal static class DelegateFactory
{
    /// <summary>
    /// Creates an Action&lt;TBase&gt; that wraps a method expecting TActual (or no params).
    /// At runtime, performs a type check and direct delegate call - no dynamic/DLR.
    /// </summary>
    public static Action<TBase>? CreateCallback<TBase>(MethodInfo method, Type? actualType) where TBase : class
    {
        var parameters = method.GetParameters();
        
        // No parameters - just invoke the method
        if (parameters.Length == 0)
        {
            var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
            return _ => del();
        }
        
        // Parameter is exactly TBase - direct delegate
        if (actualType is null || actualType == typeof(TBase))
        {
            return (Action<TBase>)Delegate.CreateDelegate(typeof(Action<TBase>), method);
        }
        
        // Parameter is a subtype - use generic wrapper
        return CreateTypedWrapper<TBase>(method, actualType);
    }
    
    private static Action<TBase>? CreateTypedWrapper<TBase>(MethodInfo method, Type actualType) where TBase : class
    {
        try
        {
            var wrapperMethod = typeof(DelegateFactory)
                .GetMethod(nameof(BuildWrapper1), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(typeof(TBase), actualType);
            
            return (Action<TBase>?)wrapperMethod.Invoke(null, [method]);
        }
        catch (Exception ex)
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            GD.PushError($"[Liturgy] Failed to create callback for {methodName}: {ex.Message}");
            return null;
        }
    }
    
    private static Action<TBase> BuildWrapper1<TBase, TActual>(MethodInfo method) where TBase : class where TActual : class, TBase
    {
        var del = (Action<TActual>)Delegate.CreateDelegate(typeof(Action<TActual>), method);
        return arg =>
        {
            if (arg is TActual typed)
                del(typed);
        };
    }

    /// <summary>
    /// Creates an Action&lt;TSignal, TBase&gt; that wraps a method with signal and node params.
    /// Handles both orderings: (signal, node) and (node, signal).
    /// </summary>
    public static Action<TSignal, TBase>? CreateSignalCallback<TSignal, TBase>(MethodInfo method, Type? actualNodeType, int? signalParamIndex, int? nodeParamIndex) where TBase : class
    {
        var parameters = method.GetParameters();
        
        // No parameters
        if (parameters.Length == 0)
        {
            var del = (Action)Delegate.CreateDelegate(typeof(Action), method);
            return (_, _) => del();
        }
        
        // Signal only
        if (signalParamIndex is not null && nodeParamIndex is null)
        {
            var del = (Action<TSignal>)Delegate.CreateDelegate(typeof(Action<TSignal>), method);
            return (signal, _) => del(signal);
        }
        
        // Node only
        if (signalParamIndex is null && nodeParamIndex is not null)
        {
            var nodeCallback = CreateCallback<TBase>(method, actualNodeType);
            if (nodeCallback is null) return null;
            return (_, node) => nodeCallback(node);
        }
        
        // Both parameters
        var signalFirst = signalParamIndex < nodeParamIndex;
        
        // Node is a subtype - use generic wrapper
        if (actualNodeType is not null && actualNodeType != typeof(TBase)) return CreateTypedSignalWrapper<TSignal, TBase>(method, actualNodeType, signalFirst);
        
        // Node type is exactly TBase - direct delegate
        
        if (signalFirst)
        {
            var del = (Action<TSignal, TBase>)Delegate.CreateDelegate(typeof(Action<TSignal, TBase>), method);
            return del;
        }
        else
        {
            var del = (Action<TBase, TSignal>)Delegate.CreateDelegate(typeof(Action<TBase, TSignal>), method);
            return (signal, node) => del(node, signal);
        }
    }
    
    private static Action<TSignal, TBase>? CreateTypedSignalWrapper<TSignal, TBase>(MethodInfo method, Type actualNodeType, bool signalFirst) where TBase : class
    {
        try
        {
            var wrapperMethodName = signalFirst 
                ? nameof(BuildWrapper2_SignalFirst) 
                : nameof(BuildWrapper2_NodeFirst);
            
            var wrapperMethod = typeof(DelegateFactory)
                .GetMethod(wrapperMethodName, BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(typeof(TSignal), typeof(TBase), actualNodeType);
            
            return (Action<TSignal, TBase>?)wrapperMethod.Invoke(null, [method]);
        }
        catch (Exception ex)
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            GD.PushError($"[Liturgy] Failed to create signal callback for {methodName}: {ex.Message}");
            return null;
        }
    }
    
    private static Action<TSignal, TBase> BuildWrapper2_SignalFirst<TSignal, TBase, TActual>(MethodInfo method) where TBase : class where TActual : class, TBase
    {
        var del = (Action<TSignal, TActual>)Delegate.CreateDelegate(typeof(Action<TSignal, TActual>), method);
        return (signal, node) =>
        {
            if (node is TActual typed) del(signal, typed);
        };
    }
    
    private static Action<TSignal, TBase> BuildWrapper2_NodeFirst<TSignal, TBase, TActual>(MethodInfo method) where TBase : class where TActual : class, TBase
    {
        var del = (Action<TActual, TSignal>)Delegate.CreateDelegate(typeof(Action<TActual, TSignal>), method);
        return (signal, node) =>
        {
            if (node is TActual typed) del(typed, signal);
        };
    }
}

