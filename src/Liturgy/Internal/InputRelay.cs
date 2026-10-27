using Godot;

namespace Liturgy.Internal;

/// <summary>
/// Internal child of the Liturgy node that receives input for the input rites.
/// It always processes, so input still arrives while the tree is paused; each rite decides whether to run.
/// </summary>
internal sealed partial class InputRelay : Node
{
    private LiturgyNode _liturgy = null!;
    private (bool Input, bool UnhandledInput, bool UnhandledKeyInput) _wanted;

    public static InputRelay Create(LiturgyNode liturgy) => new()
    {
        Name = "__LiturgyInputRelay",
        ProcessMode = ProcessModeEnum.Always,
        _liturgy = liturgy
    };

    /// <summary>
    /// Turns each kind of input on only while some rite wants it, so a project without input rites isn't sent every event.
    /// </summary>
    public void Want(bool input, bool unhandledInput, bool unhandledKeyInput)
    {
        _wanted = (input, unhandledInput, unhandledKeyInput);
        if (IsNodeReady()) Apply();
    }

    // Godot turns input on at ready for a script that overrides the input methods, so the wanted state is applied again here.
    public override void _Ready() => Apply();

    private void Apply()
    {
        SetProcessInput(_wanted.Input);
        SetProcessUnhandledInput(_wanted.UnhandledInput);
        SetProcessUnhandledKeyInput(_wanted.UnhandledKeyInput);
    }

    public override void _Input(InputEvent @event) => _liturgy.DispatchInput(@event);

    public override void _UnhandledInput(InputEvent @event) => _liturgy.DispatchUnhandledInput(@event);

    public override void _UnhandledKeyInput(InputEvent @event) => _liturgy.DispatchUnhandledKeyInput(@event);
}
