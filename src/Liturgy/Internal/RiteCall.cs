using System;
using Godot;

namespace Liturgy.Internal;

/// <summary>
/// Calls a rite so that an exception it throws is logged without stopping the rites after it, as separate signal
/// connections behave in Godot.
/// </summary>
internal static class RiteCall
{
    public static void Run(Action rite)
    {
        try { rite(); }
        catch (Exception exception) { Report(exception); }
    }

    public static void Run<T>(Action<T> rite, T argument)
    {
        try { rite(argument); }
        catch (Exception exception) { Report(exception); }
    }

    public static void Run<T1, T2>(Action<T1, T2> rite, T1 first, T2 second)
    {
        try { rite(first, second); }
        catch (Exception exception) { Report(exception); }
    }

    public static void Report(Exception exception, string? riteName = null)
    {
        var source = riteName is null ? "A rite" : $"Rite '{riteName}'";
        GD.PushError($"[Liturgy] {source} threw {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
    }
}
