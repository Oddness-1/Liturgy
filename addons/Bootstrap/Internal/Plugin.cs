#if TOOLS
using Godot;

namespace Bootstrap.Internal;

[Tool]
public partial class Plugin : EditorPlugin
{
    private const string AUTOLOAD_NAME = "BootstrapManager";
    private const string AUTOLOAD_PATH = "res://addons/Bootstrap/Internal/Autoload/Autoload.cs";
    
    public override void _EnterTree()
    {
        AddAutoloadSingleton(AUTOLOAD_NAME, AUTOLOAD_PATH);
        GetUndoRedo().ClearHistory((int)EditorUndoRedoManager.SpecialHistory.GlobalHistory, false);
    }
    
    public override void _ExitTree() => RemoveAutoloadSingleton(AUTOLOAD_NAME);
}
#endif