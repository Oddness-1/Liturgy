using System;
using System.Reflection;
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

    /// <summary>
    /// <see cref="Delegate.CreateDelegate(Type, MethodInfo)"/>, watched when diagnostics are on.
    /// </summary>
    public static Delegate Create(Type delegateType, MethodInfo method)
    {
        var inner = Delegate.CreateDelegate(delegateType, method);
        if (Rite.Diagnostics.For(method) is not { } rite) return inner;

        rite.Running = true;
        return Wrap(delegateType, inner, rite) ?? inner;
    }

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
