using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Liturgy;

namespace Sandbox.Checks;

/// <summary>
/// Automated checks for Liturgy, runnable headless:
/// <c>godot --headless --path sandbox res://Checks/Checks.tscn</c>. Prints one line per check and quits with
/// exit code 1 if any failed. Run it after every Godot upgrade.
/// </summary>
public partial class Checks : Node
{
    private static readonly Dictionary<string, int> _counts = [];
    private static readonly List<string> _values = [];
    private int _failures;

    public static void Count(string key) => _counts[key] = Times(key) + 1;
    public static void Value(string value) => _values.Add(value);
    private static int Times(string key) => _counts.GetValueOrDefault(key);

    public override async void _Ready()
    {
        GD.Print("[Checks] Liturgy checks starting");

        // Read before anything else here touches the library, which is what loads it.
        Expect("A library loaded after Liturgy started still has its rites run", SandboxLibrary.LibraryRites.ReadyCount == 1, $"ran {SandboxLibrary.LibraryRites.ReadyCount} times");

        await CheckOnSignal();
        await CheckSignalRites();
        await CheckTreeMovement();
        await CheckInterfaceRites();
        await CheckStaticInterfaceHooks();
        await CheckDedicatedRites();

        GD.Print(_failures == 0 ? "[Checks] ALL PASSED" : $"[Checks] {_failures} FAILED");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private void Expect(string name, bool condition, string detail = "")
    {
        if (!condition) _failures++;
        GD.Print($"[Checks] {(condition ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? $" ({detail})" : "")}");
    }

    private void ExpectCount(string name, string key, int expected) => Expect(name, Times(key) == expected, $"expected {expected}, got {Times(key)}");

    private async Task Frames(int count = 1)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task CheckOnSignal()
    {
        var mob = new Mob { Name = "Mob" };
        var plain = new Node { Name = "Plain" };
        AddChild(mob);
        AddChild(plain);

        mob.EmitSignal(Mob.SignalName.Died, 5);
        ExpectCount("OnSignal: source and argument", "died:source+arg", 1);
        ExpectCount("OnSignal: argument only", "died:arg", 1);
        ExpectCount("OnSignal: no parameters", "died:none", 1);
        Expect("OnSignal: argument converted to int", _values.Contains("died=5"));

        mob.EmitSignal(Mob.SignalName.Touched, plain);
        mob.EmitSignal(Mob.SignalName.Touched, mob);
        ExpectCount("OnSignal: object argument filters by type", "touched:mob", 1);

        var box = new CheckBox { Name = "Box" };
        AddChild(box);
        box.ButtonPressed = true;
        ExpectCount("OnSignal: one node parameter against one bool argument is the source", "toggled:box", 1);

        var first = new Timer { Name = "TargetedTimer", WaitTime = 0.05, OneShot = true };
        var second = new Timer { Name = "OtherTimer", WaitTime = 0.05, OneShot = true };
        AddChild(first);
        AddChild(second);
        first.Start();
        second.Start();
        await ToSignal(second, Timer.SignalName.Timeout);
        await Frames();
        ExpectCount("OnSignal: Target", "timeout:targeted", 1);
        ExpectCount("OnSignal: Exclude", "timeout:excluded", 1);
        ExpectCount("OnSignal: Timer source counts both", "timeout:any", 2);
        Expect("OnSignal: Priority orders calls", _values.IndexOf("priority:-1") < _values.IndexOf("priority:5") && _values.IndexOf("priority:-1") >= 0);

        // Multiple OnSignal attributes on one method
        mob.EmitSignal(Mob.SignalName.Fled);
        ExpectCount("OnSignal: several on one method", "died-or-fled", 2);
    }

    private async Task CheckSignalRites()
    {
        var popup = new PopupMenu { Name = "Popup" };
        var menuButton = new MenuButton { Name = "Menu" };
        var label = new RichTextLabel { Name = "Label" };
        var notifier = new VisibleOnScreenNotifier2D { Name = "Notifier" };
        AddChild(popup);
        AddChild(menuButton);
        AddChild(label);
        AddChild(notifier);
        await Frames();

        popup.EmitSignal(PopupMenu.SignalName.IdPressed, 7);
        menuButton.GetPopup().EmitSignal(PopupMenu.SignalName.IdPressed, 3);
        Expect("OnIdPressed: PopupMenu", _values.Contains("popup:7"));
        Expect("OnIdPressed: MenuButton's menu, MenuButton as source", _values.Contains("menu:3"));
        ExpectCount("OnIdPressed: no parameters, both menus", "id:any", 2);

        label.EmitSignal(RichTextLabel.SignalName.MetaClicked, "https://example.com");
        Expect("OnMetaClicked", _values.Contains("meta:https://example.com"));

        notifier.EmitSignal(VisibleOnScreenNotifier2D.SignalName.ScreenEntered);
        notifier.EmitSignal(VisibleOnScreenNotifier2D.SignalName.ScreenExited);
        ExpectCount("OnScreenEntered", "screen:entered", 1);
        ExpectCount("OnScreenExited", "screen:exited", 1);
    }

    private async Task CheckTreeMovement()
    {
        var holder = new Node { Name = "Holder" };
        var mob = new Mob { Name = "Mover" };
        AddChild(holder);
        AddChild(mob);

        var before = Times("died:none");
        mob.Reparent(holder);
        mob.EmitSignal(Mob.SignalName.Died, 1);
        Expect("Reparent: rite fires once", Times("died:none") == before + 1, $"fired {Times("died:none") - before}");

        await Frames();
        before = Times("died:none");
        mob.EmitSignal(Mob.SignalName.Died, 1);
        Expect("Reparent: still once after the deferred check", Times("died:none") == before + 1, $"fired {Times("died:none") - before}");

        var exits = Times("exited:Mover");
        holder.RemoveChild(mob);
        Expect("tree_exited reaches OnSignal", Times("exited:Mover") == exits + 1, $"fired {Times("exited:Mover") - exits}");
        await Frames();
        before = Times("died:none");
        mob.EmitSignal(Mob.SignalName.Died, 1);
        Expect("Out of the tree: rite doesn't fire", Times("died:none") == before);

        holder.AddChild(mob);
        mob.EmitSignal(Mob.SignalName.Died, 1);
        Expect("Back in the tree: rite fires once", Times("died:none") == before + 1, $"fired {Times("died:none") - before}");

        mob.QueueFree();
        await Frames(2);
        Expect("Freed node: no errors", true);
    }

    private async Task CheckInterfaceRites()
    {
        var probe = new ProbeTimer { Name = "Probe", WaitTime = 0.05, OneShot = true };
        var button = new ProbeButton { Name = "ProbeButton" };
        var generic = new GenericProbe { Name = "Generic" };
        AddChild(probe);
        AddChild(button);
        AddChild(generic);

        ExpectCount("Interface OnReady", "iface:ready:Probe", 1);
        ExpectCount("Interface OnReady on a generic interface", "iface:generic-ready", 1);

        probe.EmitSignal(ProbeTimer.SignalName.Pinged, 4);
        Expect("Interface OnSignal with argument", _values.Contains("iface:pinged=4"));

        probe.Start();
        await ToSignal(probe, Timer.SignalName.Timeout);
        ExpectCount("Interface OnTimeout on the implementing timer", "iface:timeout", 1);

        button.EmitSignal(BaseButton.SignalName.Pressed);
        ExpectCount("Interface default body runs", "iface:default-pressed", 1);

        await Frames(3);
        Expect("Interface OnProcess runs and turned processing on", Times("iface:process") >= 2 && probe.IsProcessing(), $"{Times("iface:process")} frames");

        GetTree().Paused = true;
        await Frames();
        var paused = Times("iface:process");
        await Frames(3);
        Expect("Interface OnProcess stops while paused", Times("iface:process") == paused, $"{Times("iface:process") - paused} frames while paused");
        GetTree().Paused = false;

        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Q, Pressed = true });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        await Frames(3);
        ExpectCount("Interface OnInput filters by event type", "iface:key", 1);

        RemoveChild(probe);
        AddChild(probe);
        await Frames(2);
        ExpectCount("Interface OnReady: re-entering the tree doesn't repeat it", "iface:ready:Probe", 1);
        var before = Times("iface:process");
        await Frames(2);
        Expect("Interface OnProcess: re-entering doesn't double it", Times("iface:process") - before <= 2, $"{Times("iface:process") - before} calls in 2 frames");
    }

    private async Task CheckStaticInterfaceHooks()
    {
        var before = Times("static:iface-ready");
        AddChild(new ProbeTimer { Name = "Probe2" });
        await Frames();
        ExpectCount("Static OnReady(PerNode) taking an interface", "static:iface-ready", before + 1);
        Expect("Static OnNodeAdded taking an interface", Times("static:iface-added") >= 2);
    }

    private async Task CheckDedicatedRites()
    {
        var holder = new Node { Name = "ButtonHolder" };
        var button = new Button { Name = "MovingButton" };
        AddChild(holder);
        AddChild(button);
        await Frames();

        button.Reparent(holder);
        await Frames();
        button.EmitSignal(BaseButton.SignalName.Pressed);
        ExpectCount("Dedicated rite: OnButtonPressed fires once after a reparent", "dedicated:pressed", 1);

        var spinner = new Spinner { Name = "Spinner" };
        AddChild(spinner);
        await Frames(2);
        GetTree().Paused = true;
        await Frames();
        var paused = Times("dedicated:pernode-process");
        await Frames(3);
        Expect("Dedicated rite: PerNode OnProcess stops while paused", Times("dedicated:pernode-process") == paused, $"{Times("dedicated:pernode-process") - paused} frames while paused");
        GetTree().Paused = false;
    }
}

public partial class Spinner : Node
{
    public override void _Process(double delta) { }
}

public partial class Mob : Node
{
    [Signal] public delegate void DiedEventHandler(int amount);
    [Signal] public delegate void TouchedEventHandler(Node other);
    [Signal] public delegate void FledEventHandler();
}

public interface IProbe
{
    [Rite.OnReady] void ProbeReady();
    [Rite.OnSignal("Pinged")] void WasPinged(int count);
    [Rite.OnTimeout] void Ticked();
    [Rite.OnProcess] void Tick(double delta);
    [Rite.OnInput] void Key(InputEventKey key);
}

public interface IPressable
{
    [Rite.OnButtonPressed] void Pressed() => Checks.Count("iface:default-pressed");
}

public interface IGenericProbe<T>
{
    [Rite.OnReady] void GenericReady();
}

public partial class ProbeTimer : Timer, IProbe
{
    [Signal] public delegate void PingedEventHandler(int count);

    public void ProbeReady() => Checks.Count($"iface:ready:{Name}");
    public void WasPinged(int count) => Checks.Value($"iface:pinged={count}");
    public void Ticked() => Checks.Count("iface:timeout");
    public void Tick(double delta) => Checks.Count("iface:process");
    public void Key(InputEventKey key) => Checks.Count("iface:key");
}

public partial class ProbeButton : Button, IPressable;

public partial class GenericProbe : Node, IGenericProbe<int>
{
    public void GenericReady() => Checks.Count("iface:generic-ready");
}

internal static class CheckRites
{
    [Rite.OnSignal(nameof(Mob.SignalName.Died))]
    private static void DiedWithSource(Mob mob, int amount) => Checks.Count("died:source+arg");

    [Rite.OnSignal(nameof(Mob.SignalName.Died))]
    private static void DiedArgument(int amount)
    {
        Checks.Count("died:arg");
        Checks.Value($"died={amount}");
    }

    [Rite.OnSignal(nameof(Mob.SignalName.Died))]
    private static void Died() => Checks.Count("died:none");

    [Rite.OnSignal(nameof(Mob.SignalName.Touched))]
    private static void TouchedMob(Mob other) => Checks.Count("touched:mob");

    [Rite.OnSignal("toggled")]
    private static void Toggled(CheckBox box) => Checks.Count("toggled:box");

    [Rite.OnSignal("timeout", Target = "Checks/TargetedTimer")]
    private static void TargetedTimeout() => Checks.Count("timeout:targeted");

    [Rite.OnSignal("timeout", Exclude = "Checks/TargetedTimer")]
    private static void ExcludedTimeout(Timer timer)
    {
        if (timer.Name == "OtherTimer") Checks.Count("timeout:excluded");
    }

    [Rite.OnSignal("timeout")]
    private static void AnyTimeout(Timer timer)
    {
        if (timer.Name.ToString().EndsWith("Timer")) Checks.Count("timeout:any");
    }

    [Rite.OnSignal("timeout", Target = "Checks/OtherTimer", Priority = 5)]
    private static void Late() => Checks.Value("priority:5");

    [Rite.OnSignal("timeout", Target = "Checks/OtherTimer", Priority = -1)]
    private static void Early() => Checks.Value("priority:-1");

    [Rite.OnSignal(nameof(Mob.SignalName.Died))]
    [Rite.OnSignal(nameof(Mob.SignalName.Fled))]
    private static void DiedOrFled(Mob mob)
    {
        if (mob.Name == "Mob") Checks.Count("died-or-fled");
    }

    [Rite.OnSignal("tree_exited")]
    private static void Exited(Mob mob) => Checks.Count($"exited:{mob.Name}");

    [Rite.OnIdPressed]
    private static void PopupId(PopupMenu menu, int id) => Checks.Value($"popup:{id}");

    [Rite.OnIdPressed]
    private static void MenuId(MenuButton menu, long id) => Checks.Value($"menu:{id}");

    [Rite.OnIdPressed]
    private static void AnyId() => Checks.Count("id:any");

    [Rite.OnMetaClicked]
    private static void Meta(RichTextLabel label, string meta) => Checks.Value($"meta:{meta}");

    [Rite.OnScreenEntered]
    private static void Entered(VisibleOnScreenNotifier2D notifier) => Checks.Count("screen:entered");

    [Rite.OnScreenExited]
    private static void LeftScreen() => Checks.Count("screen:exited");

    [Rite.OnReady(Mode = Rite.ReadyMode.PerNode)]
    private static void AnyProbeReady(IProbe probe) => Checks.Count("static:iface-ready");

    [Rite.OnNodeAdded]
    private static void ProbeAdded(IProbe probe) => Checks.Count("static:iface-added");

    [Rite.OnButtonPressed(Target = "Checks/ButtonHolder/MovingButton, Checks/MovingButton")]
    private static void DedicatedPressed() => Checks.Count("dedicated:pressed");

    [Rite.OnProcess(Mode = Rite.FrameMode.PerNode)]
    private static void SpinnerProcess(Spinner spinner) => Checks.Count("dedicated:pernode-process");
}
