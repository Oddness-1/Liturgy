using System;
using Godot;

namespace Liturgy.Internal;

/// <summary>
/// What the harness remembers between runs: the greeted version and the layout. Kept in the project's .godot folder,
/// so it's per machine, never committed, and deleting the folder just resets it.
/// </summary>
internal static class HarnessPrefs
{
    public const string PATH = "res://.godot/liturgy/harness.cfg";

    private static ConfigFile? _config;

    private static ConfigFile Config
    {
        get
        {
            if (_config is not null) return _config;
            _config = new ConfigFile();
            _config.Load(PATH);
            return _config;
        }
    }

    public static Variant Get(string section, string key, Variant fallback) => Config.GetValue(section, key, fallback);

    public static bool Has(string section, string key) => Config.HasSectionKey(section, key);

    public static void Set(string section, string key, Variant value)
    {
        Config.SetValue(section, key, value);

        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(PATH.GetBaseDir()));
            Config.Save(PATH);
        }
        catch (Exception exception) { GD.PushWarning($"[Liturgy] Couldn't save the harness's settings: {exception.Message}"); }
    }
}
