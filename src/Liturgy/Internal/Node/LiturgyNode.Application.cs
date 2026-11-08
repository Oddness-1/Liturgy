using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static Liturgy.Rite;

namespace Liturgy.Internal;

/// <summary>
/// Rites for what happens to the game as a whole: focus, OS pause, language, tree pause and shutdown. Godot only sends
/// these as notifications, which reach Liturgy's node like any other node, so static code hears them without a node of
/// its own. There's no source and nothing to filter: each fires once for the game.
/// </summary>
internal sealed partial class LiturgyNode
{
    // By notification; a notification no rite listens for has no entry.
    private readonly Dictionary<long, List<Action>> _applicationCallbacks = [];

    private void SetupApplicationCallbacks()
    {
        SetupApplicationHook<OnApplicationFocusEntered>(NotificationApplicationFocusIn, attr => attr.Priority);
        SetupApplicationHook<OnApplicationFocusExited>(NotificationApplicationFocusOut, attr => attr.Priority);
        SetupApplicationHook<OnApplicationPaused>(NotificationApplicationPaused, attr => attr.Priority);
        SetupApplicationHook<OnApplicationResumed>(NotificationApplicationResumed, attr => attr.Priority);
        SetupApplicationHook<OnLanguageChanged>(NotificationTranslationChanged, attr => attr.Priority);
        SetupApplicationHook<OnTreePaused>(NotificationPaused, attr => attr.Priority);
        SetupApplicationHook<OnTreeUnpaused>(NotificationUnpaused, attr => attr.Priority);

        // Liturgy's node is /root's first child, so it leaves the tree last, after the scene and the autoloads.
        SetupApplicationHook<OnApplicationQuit>(NotificationExitTree, attr => attr.Priority);
    }

    private void SetupApplicationHook<TAttr>(long notification, Func<TAttr, int> getPriority) where TAttr : Attribute
    {
        var name = typeof(TAttr).Name;
        var valid = new List<(MethodInfo Method, int Priority)>();

        foreach (var (method, attr) in ScanForMethods<TAttr>())
        {
            var methodName = $"{method.DeclaringType?.FullName}.{method.Name}";
            if (method.GetParameters().Length > 0)
            {
                LiturgyReport.AddMethodError(methodName, name, "Must be parameterless.");
                continue;
            }

            LiturgyReport.AddApplicationHook(name, methodName, getPriority(attr));
            valid.Add((method, getPriority(attr)));
        }

        if (valid.Count == 0) return;

        _applicationCallbacks[notification] = valid
            .OrderBy(entry => entry.Priority)
            .Select(entry => (Action)RiteProbe.Create(typeof(Action), entry.Method))
            .ToList();
    }

    public override void _Notification(int what)
    {
        if (!_applicationCallbacks.TryGetValue(what, out var callbacks)) return;
        foreach (var callback in callbacks) RiteCall.Run(callback);
    }
}
