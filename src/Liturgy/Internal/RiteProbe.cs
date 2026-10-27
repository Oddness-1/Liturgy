using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;

namespace Liturgy.Internal;

/// <summary>
/// Builds the delegates rites are called through. In editor runs each one is wrapped so the harness can count, time,
/// log and mute it; in exported games it's the plain delegate, with nothing in between.
/// </summary>
internal static class RiteProbe
{
    private static readonly MethodInfo WRAP_1 = typeof(RiteProbe).GetMethod(nameof(Wrap1), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo WRAP_2 = typeof(RiteProbe).GetMethod(nameof(Wrap2), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo WRAP_3 = typeof(RiteProbe).GetMethod(nameof(Wrap3), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo NARROW_1 = typeof(RiteProbe).GetMethod(nameof(Narrow1), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo NARROW_2 = typeof(RiteProbe).GetMethod(nameof(Narrow2), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo NARROW_3 = typeof(RiteProbe).GetMethod(nameof(Narrow3), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo IGNORE_1 = typeof(RiteProbe).GetMethod(nameof(Ignore1), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo IGNORE_2 = typeof(RiteProbe).GetMethod(nameof(Ignore2), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo IGNORE_3 = typeof(RiteProbe).GetMethod(nameof(Ignore3), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// <see cref="Delegate.CreateDelegate(Type, MethodInfo)"/>, watched when diagnostics are on. A rite may take a
    /// narrower type than the delegate passes (a <c>Player</c> where the signal gives a <c>Node2D</c>): it's then called
    /// only with arguments of its own types. A rite that can't be bound at all is reported and skipped, never thrown, so
    /// the rest of its category still runs.
    /// </summary>
    public static Delegate Create(Type delegateType, MethodInfo method)
    {
        Type innerType;
        Delegate inner;
        try
        {
            innerType = Narrowed(delegateType, method);
            inner = Delegate.CreateDelegate(innerType, method);
        }
        catch (Exception exception)
        {
            GD.PushError($"[Liturgy] {method.DeclaringType?.FullName}.{method.Name} can't be called as this rite, so it won't run: {exception.Message}");
            return Ignore(delegateType);
        }

        if (Rite.Diagnostics.For(method) is { } rite)
        {
            rite.Running = true;
            inner = Wrap(innerType, inner, rite) ?? inner;
        }

        return innerType == delegateType ? inner : Narrow(delegateType, innerType, inner);
    }

    // The delegate type the method can be bound to as it's written: the requested one, with each reference argument the
    // method narrows (a parameter of a subtype) swapped for the method's own type.
    private static Type Narrowed(Type delegateType, MethodInfo method)
    {
        if (!delegateType.IsGenericType) return delegateType;

        var arguments = delegateType.GetGenericArguments();
        var parameters = method.GetParameters();
        if (parameters.Length != arguments.Length) return delegateType;

        var narrowed = false;
        for (var i = 0; i < arguments.Length; i++)
        {
            var own = parameters[i].ParameterType;
            if (own == arguments[i] || arguments[i].IsValueType || own.IsValueType || !arguments[i].IsAssignableFrom(own)) continue;
            arguments[i] = own;
            narrowed = true;
        }

        return narrowed ? delegateType.GetGenericTypeDefinition().MakeGenericType(arguments) : delegateType;
    }

    private static Delegate Narrow(Type delegateType, Type innerType, Delegate inner)
    {
        var narrow = delegateType.GetGenericArguments().Length switch { 1 => NARROW_1, 2 => NARROW_2, 3 => NARROW_3, _ => null }
            ?? throw new NotSupportedException($"Liturgy can't narrow a rite with {delegateType.GetGenericArguments().Length} parameters.");
        return (Delegate)narrow.MakeGenericMethod([.. delegateType.GetGenericArguments(), .. innerType.GetGenericArguments()]).Invoke(null, [inner])!;
    }

    private static Action<T> Narrow1<T, TOwn>(Action<TOwn> inner) => argument =>
    {
        if (Fits(argument, out TOwn own)) inner(own);
    };

    private static Action<T1, T2> Narrow2<T1, T2, TOwn1, TOwn2>(Action<TOwn1, TOwn2> inner) => (first, second) =>
    {
        if (Fits(first, out TOwn1 own1) && Fits(second, out TOwn2 own2)) inner(own1, own2);
    };

    private static Action<T1, T2, T3> Narrow3<T1, T2, T3, TOwn1, TOwn2, TOwn3>(Action<TOwn1, TOwn2, TOwn3> inner) => (first, second, third) =>
    {
        if (Fits(first, out TOwn1 own1) && Fits(second, out TOwn2 own2) && Fits(third, out TOwn3 own3)) inner(own1, own2, own3);
    };

    // An argument the rite didn't narrow passes as it is, null included; a narrowed one passes only if it's of the rite's
    // own type.
    private static bool Fits<T, TOwn>(T argument, out TOwn own)
    {
        if (typeof(T) == typeof(TOwn))
        {
            own = Unsafe.As<T, TOwn>(ref argument);
            return true;
        }

        if (argument is TOwn typed)
        {
            own = typed;
            return true;
        }

        own = default!;
        return false;
    }

    // Stands in for a rite that couldn't be bound, so whatever subscribes it still gets a delegate of the right type.
    private static Delegate Ignore(Type delegateType)
    {
        if (delegateType == typeof(Action)) return new Action(() => { });

        var arguments = delegateType.GetGenericArguments();
        var ignore = arguments.Length switch { 1 => IGNORE_1, 2 => IGNORE_2, 3 => IGNORE_3, _ => null };
        return ignore is null
            ? throw new NotSupportedException($"Liturgy has no stand-in for a rite with {arguments.Length} parameters.")
            : (Delegate)ignore.MakeGenericMethod(arguments).Invoke(null, null)!;
    }

    private static Action<T> Ignore1<T>() => _ => { };
    private static Action<T1, T2> Ignore2<T1, T2>() => (_, _) => { };
    private static Action<T1, T2, T3> Ignore3<T1, T2, T3>() => (_, _, _) => { };

    /// <summary>
    /// Runs a rite Liturgy calls by reflection (signal and interface rites), watched when diagnostics are on.
    /// </summary>
    public static void Run<TState>(MethodInfo method, Node node, TState state, Action<TState> call)
    {
        if (Rite.Diagnostics.For(method) is not { } rite)
        {
            call(state);
            return;
        }

        if (rite.Muted) return;

        var entered = rite.Enter(node);
        try { call(state); }
        catch { rite.Threw(); throw; }
        finally { rite.Exit(entered); }
    }

    // The node Liturgy is dispatching for. Rites often take no node, or take the other party (the body entering an
    // area), so the dispatcher says whose rite it is.
    [ThreadStatic] private static Node? _dispatching;

    public static Node? EnterNode(Node? node)
    {
        var outer = _dispatching;
        if (node is not null && Rite.Diagnostics.IsEnabled) _dispatching = node;
        return outer;
    }

    public static void ExitNode(Node? outer) => _dispatching = outer;

    /// <summary>
    /// Marks a rite Liturgy calls by reflection as wired up.
    /// </summary>
    public static void MarkRunning(MethodInfo method)
    {
        if (Rite.Diagnostics.For(method) is { } rite) rite.Running = true;
    }

    private static Delegate? Wrap(Type delegateType, Delegate inner, RiteInfo rite)
    {
        if (delegateType == typeof(Action)) return Wrap0((Action)inner, rite);
        if (!delegateType.IsGenericType) return null;

        var definition = delegateType.GetGenericTypeDefinition();
        var wrapper = definition == typeof(Action<>) ? WRAP_1 : definition == typeof(Action<,>) ? WRAP_2 : definition == typeof(Action<,,>) ? WRAP_3 : null;
        return (Delegate?)wrapper?.MakeGenericMethod(delegateType.GetGenericArguments()).Invoke(null, [inner, rite]);
    }

    private static Action Wrap0(Action inner, RiteInfo rite) => () =>
    {
        if (rite.Muted) return;

        var entered = rite.Enter(_dispatching);
        try { inner(); }
        catch { rite.Threw(); throw; }
        finally { rite.Exit(entered); }
    };

    private static Action<T> Wrap1<T>(Action<T> inner, RiteInfo rite) => argument =>
    {
        if (rite.Muted) return;

        var entered = rite.Enter(_dispatching ?? argument as Node);
        try { inner(argument); }
        catch { rite.Threw(); throw; }
        finally { rite.Exit(entered); }
    };

    private static Action<T1, T2> Wrap2<T1, T2>(Action<T1, T2> inner, RiteInfo rite) => (first, second) =>
    {
        if (rite.Muted) return;

        var entered = rite.Enter(_dispatching ?? first as Node ?? second as Node);
        try { inner(first, second); }
        catch { rite.Threw(); throw; }
        finally { rite.Exit(entered); }
    };

    private static Action<T1, T2, T3> Wrap3<T1, T2, T3>(Action<T1, T2, T3> inner, RiteInfo rite) => (first, second, third) =>
    {
        if (rite.Muted) return;

        var entered = rite.Enter(_dispatching ?? first as Node ?? second as Node ?? third as Node);
        try { inner(first, second, third); }
        catch { rite.Threw(); throw; }
        finally { rite.Exit(entered); }
    };
}
