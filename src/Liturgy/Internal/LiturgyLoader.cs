using Godot;

namespace Liturgy.Internal;

/// <summary>
/// Serves Liturgy's runtime autoload. The autoload entry needs a resource path, and Liturgy's node lives in a DLL
/// with no script file under res://, so this loader answers for that one path with the node's script. It removes
/// itself once Godot has loaded the autoload.
/// </summary>
internal sealed partial class LiturgyLoader : ResourceFormatLoader
{
    internal const string PATH = "res://__liturgy__.liturgy";
    private const string EXTENSION = "liturgy";

    private static LiturgyLoader? _instance;

    internal static void Register()
    {
        _instance = new LiturgyLoader();
        ResourceLoader.AddResourceFormatLoader(_instance, atFront: true);
    }

    private static void Unregister()
    {
        if (_instance is null) return;
        ResourceLoader.RemoveResourceFormatLoader(_instance);
        _instance = null;
    }

    public override string[] _GetRecognizedExtensions() => [EXTENSION];

    public override bool _RecognizePath(string path, StringName type) => path == PATH;

    public override bool _HandlesType(StringName type) => type == "Script";

    public override string _GetResourceType(string path) => path == PATH ? "Script" : "";

    public override bool _Exists(string path) => path == PATH;

    public override Variant _Load(string path, string originalPath, bool useSubThreads, int cacheMode)
    {
        // Godot builds autoloads from a script, so borrow the one Godot made for LiturgyNode.
        var donor = new LiturgyNode();
        var script = donor.GetScript().As<Script>();
        donor.Free();

        Callable.From(Unregister).CallDeferred();
        return script;
    }
}
