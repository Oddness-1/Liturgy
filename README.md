# Liturgy

Attribute-based hooks for Godot C#. Write static methods, add attributes, and Liturgy wires them up automatically — globally, across your entire project.

*Liturgy is an independent project, not affiliated with or endorsed by the Godot Foundation.*

## Why Liturgy?

In Godot, applying behavior to many nodes is tedious:

- **Wire signals in the editor?** Manual, error-prone, doesn't scale.
- **Inheritance hierarchies?** Couples everything, limits flexibility.
- **Autoloads that iterate the tree?** You're reinventing Liturgy.
- **Manual registration in every `_Ready`?** Boilerplate everywhere.

Liturgy lets you say *"for all X, do Y"* in one place:

```csharp
// Auto-register every Enemy when it enters the scene
[Rite.OnReady(Mode = Rite.ReadyMode.PerNode)]
private static void Register(Enemy enemy) => EnemyManager.Track(enemy);

// Play a sound when ANY button is pressed
[Rite.OnButtonPressed]
private static void ClickSound(BaseButton btn) => Sounds.Play("click");

// Log every unhandled input (debugging/analytics)
[Rite.OnUnhandledInput]
private static void LogInput(InputEvent e) => Analytics.Record(e);

// Highlight ANY focused control (accessibility)
[Rite.OnFocusEntered]
private static void Highlight(Control c) => c.Modulate = Colors.Yellow;

[Rite.OnFocusExited]
private static void Unhighlight(Control c) => c.Modulate = Colors.White;

// Pause offscreen audio (performance)
[Rite.OnVisibilityChanged]
private static void PauseOffscreen(AudioStreamPlayer2D player) 
    => player.StreamPaused = !player.Visible;
```

No manual wiring. No base classes. No forgetting to connect signals. It just works.

## Installation

Add the package to your game's `.csproj`:

```xml
<PackageReference Include="Liturgy.Godot" Version="0.1.0" />
```

That's all. There's no addon to copy, no plugin to enable and no autoload to register.

Every attribute is a *rite*, nested in the `Rite` class of the `Liturgy` namespace:

```csharp
using Liturgy;

public static class Feedback
{
    [Rite.OnButtonPressed]
    private static void Click(BaseButton button) => Sounds.Play("click");
}
```

**Requirements:** Godot 4.7+ (.NET), .NET 9.

### How it attaches

The package ships a small source generator that puts a hidden attribute on each of your Godot classes, the same way
Godot's own generator adds `[ScriptPath]`. It adds no members and you never see it in your files. When Godot loads your
first C# script, it builds that attribute, and Liturgy adds a `Liturgy` node to `/root`, ahead of your autoloads and
the main scene. Nothing is written to `project.godot`. This works the same when running from the editor and in an
exported game. In the editor itself it does nothing.

Only assemblies that reference Liturgy are scanned for hooks, so large dependencies aren't searched at startup. To find
those assemblies, Liturgy reads the reference lists of your other assemblies, which loads them a little earlier than
.NET otherwise would. A library of rites that your game references is found even if .NET hasn't loaded it yet when
Liturgy starts.

## The Harness

Global hooks have a fair criticism: behavior arrives from somewhere you can't see. Liturgy answers it by showing you
everything. Run your game from the editor and press **Alt+~** to open the harness, an overlay that lists:

- **Rites:** every method carrying a rite, its description, how many nodes it acts on right now, how often it has
  fired and when it last did. Select one to outline its nodes on screen. Untick it to mute it and see what changes.
- **Nodes:** your scene tree with how many rites act on each node. Pick a control on screen to jump to it and see every
  rite acting on it, with how often each has fired for that node.
- **Assigned:** every `AssignNode` member, the path it follows, the node it holds, which nodes hold it, and why it's
  empty when it is.
- **Log:** the latest firings, with the node each ran for.

Click a heading to sort by it, drag the lines between headings, panes and the panel's edge to resize them; the harness
remembers the layout per project.

While it's open, the harness takes all keyboard and mouse input, so your game doesn't react to clicks and keys meant
for it. Code that polls `Input` every frame still sees keys; set `HarnessPausesGame` to pause the game while it's open.
The first time a project runs with Liturgy, and again after each update, a note in the corner says hello and lists
what's new.

The harness is a development tool. Exported games never include it, nor the counting behind it: rites are called
directly. To turn it off in editor runs too:

```csharp
[assembly: Rite.Settings(Harness = false)]
```

To profile a single run without it, add `--liturgy-quiet` to Project Settings > Editor > Run > Main Run Args.

If your project turns off embedded subwindows, the harness opens as its own window instead of an overlay, without
on-screen picking.

### Describing Rites

Every rite takes an optional `Description`, shown in the harness next to what it touches:

```csharp
[Rite.OnButtonPressed(Description = "Plays the click sound for every button.")]
private static void Click(BaseButton button) => Sounds.Play("click");
```

### Keeping Nodes Out

Add a node to the `liturgy_ignore` group (`Rite.IGNORE_GROUP`) and no rite sees it or anything under it. Use it for a
scene that must behave exactly as written wherever it's placed, such as a third-party addon's UI. Add the group in
the scene (Node > Groups) or before the node enters the tree; Liturgy decides when a node arrives. Liturgy's own nodes
are always hidden this way.

### Diagnostics API

The harness is built on `Rite.Diagnostics`, which your own dev console can read:

```csharp
foreach (var rite in Rite.Diagnostics.Rites)
    GD.Print($"{rite.Name}: {rite.Fires} fires. {rite.Description}");

var onThisButton = Rite.Diagnostics.Touching(myButton);
```

It holds `Rites`, `Touching(node)`, `CountTouching()`, `NodesTouchedBy(rite)`, `RecentFirings` and `Reset()`. Each
`RiteInfo` carries its counts, timings, `FiresFor(node)` and a settable `Muted`. Counts are kept in editor runs only.

### When Not to Use Liturgy

A rite is for a rule that holds across the game: *every* button clicks, *every* enemy registers, *every* focused
control highlights. Behavior that belongs to one scene belongs in that scene's script, where someone reading the scene
will find it. If you'd describe a rite with "when this particular node...", it's probably not a rite. Liturgy doesn't
replace signals you'd connect in a single scene; it replaces wiring the same signal into every scene.

## Table of Contents

- [The Harness](#the-harness)
- [Node Lifecycle](#node-lifecycle)
- [AssignNode](#assignnode)
- [Frame Processing](#frame-processing)
- [Input](#input)
- [UI - Buttons](#ui---buttons)
- [UI - Text Input](#ui---text-input)
- [UI - Value Controls](#ui---value-controls)
- [UI - Selection Controls](#ui---selection-controls)
- [UI - Color & File](#ui---color--file)
- [UI - Focus & Mouse](#ui---focus--mouse)
- [UI - Sliders & Scroll](#ui---sliders--scroll)
- [Visibility](#visibility)
- [Physics](#physics)
- [Animation](#animation)
- [Audio](#audio)
- [Timers](#timers)
- [Popups & Dialogs](#popups--dialogs)
- [Any Signal](#any-signal)
- [Interface Rites](#interface-rites)
- [Filtering & Targeting](#filtering--targeting)
- [Modes](#modes)
- [Parameter Flexibility](#parameter-flexibility)
- [Modifying the Scene Tree](#modifying-the-scene-tree)
- [Debug Mode](#debug-mode)
- [Performance Notes](#performance-notes)
- [Compile-Time Checks](#compile-time-checks)
- [When a Rite Throws](#when-a-rite-throws)
- [Rider and ReSharper](#rider-and-resharper)
- [Suppressing Warnings](#suppressing-warnings)
- [Building from Source](#building-from-source)

---

## Node Lifecycle

| Attribute | When it fires |
|-----------|---------------|
| `[Rite.OnReady]` | RootReady mode (default): once when `/root` is ready. Immediate mode: during Liturgy init. PerNode mode: each node's `_Ready` |
| `[Rite.OnNodeAdded]` | Node added to tree (before `_Ready`) |
| `[Rite.OnNodeRemoved]` | Node removed from tree |
| `[Rite.OnTreeChanged]` | Any tree structure change |

```csharp
// Fire once when /root is ready (default)
[Rite.OnReady]
private static void GameStart() => GD.Print("Game started!");

// Fire before /root is ready (during Liturgy initialization)
[Rite.OnReady(Mode = Rite.ReadyMode.Immediate)]
private static void EarlySetup() => GD.Print("Early setup!");

// Auto-register every Enemy as it enters the scene
[Rite.OnReady(Mode = Rite.ReadyMode.PerNode)]
private static void Register(Enemy enemy) => EnemyManager.Track(enemy);

// Clean up when nodes are removed
[Rite.OnNodeRemoved]
private static void Cleanup(Enemy enemy) => EnemyManager.Untrack(enemy);
```

---

## AssignNode

Automatically assign scene tree nodes to static fields or properties, or to an interface property on every node that
implements it.

```csharp
// Assign an autoload
[Rite.AssignNode("/root/GameManager")]
private static GameManager GM;

// Relative paths resolve from /root/
[Rite.AssignNode("Main/Player")]
private static Player Player;

// Works with properties too
[Rite.AssignNode("/root/AudioManager")]
private static AudioManager Audio { get; set; }

// Or an interface: whatever node at the path implements it
[Rite.AssignNode("/root/Music")]
private static IMusicPlayer Music;

// A pattern: the first match anywhere in the tree, here the camera in whatever scene is loaded
[Rite.AssignNode("**/%Camera")]
private static Camera3D Camera;
```

On an interface property, each node that implements the interface holds its own copy:

```csharp
public interface ISeesCamera
{
    [Rite.AssignNode("**/%Camera")]
    Camera3D Camera { get; set; }
}

public partial class Enemy : CharacterBody3D, ISeesCamera
{
    public Camera3D Camera { get; set; }

    public override void _Ready() => LookAt(Camera.GlobalPosition);
}
```

**Key points:**
- Static members are assigned when `/root` is ready, **before** any `[Rite.OnReady]` callbacks in `RootReady` mode
- This means your `[Rite.OnReady]` methods can safely reference these static fields
- An interface property is filled as each implementing node enters the tree, so it's set by the node's own `_Ready`, even when the node it points at comes later in the same scene (a node already in the tree when Liturgy starts is filled then). It's emptied when the node leaves the tree
- The member follows its path: when its node leaves the tree it's set to null, and when a node arrives at the path again (after a scene change, say) it's assigned that one, so it never holds a freed node
- Paths start at `/root`. A path with `*` is a pattern, matched like `Exclude`: `**` spans any depth, and a last segment written `%Name` needs a scene-unique name. The first match in tree order wins, and when it leaves the next one is picked
- If no node is at the path yet, a warning is logged and the member stays null until one arrives
- The member's type can be a node type, a base type, or an interface the node implements
- An instance member of a class is never assigned (LIT008): make it static, or declare it on an interface the node implements
- The harness lists every assigned member, the node it holds, which nodes hold it, and why it's empty when it is

```csharp
// Safe to use GM here — it's already assigned
[Rite.OnReady]
private static void Init() => GM.StartGame();
```

---

## Frame Processing

| Attribute | When it fires |
|-----------|---------------|
| `[Rite.OnProcess]` | Every frame (`_Process`) |
| `[Rite.OnPhysicsProcess]` | Every physics tick (`_PhysicsProcess`) |

```csharp
// Global game loop
[Rite.OnProcess]
private static void Update(double delta) => GameManager.Tick(delta);

// Update specific node
[Rite.OnProcess(Target = "Player")]
private static void UpdatePlayer(double delta, CharacterBody3D player) { }

// Keep running while the tree is paused (Global mode, no Target)
[Rite.OnProcess(WhilePaused = true)]
private static void AdvanceClock(double delta) => Clock.Tick(delta);
```

`WhilePaused` callbacks run at the start of each frame, before the pausable ones, so `Priority` orders them only among
themselves.

`Target` and `PerNode` rites run for their nodes every frame the node isn't paused, even when the node doesn't process
on its own (no `_Process` override, or `SetProcess(false)`). They don't turn the node's own processing on.

---

## Input

| Attribute | When it fires |
|-----------|---------------|
| `[Rite.OnInput]` | Any input event (`_Input`) |
| `[Rite.OnUnhandledInput]` | Unhandled input (`_UnhandledInput`) |
| `[Rite.OnUnhandledKeyInput]` | Unhandled key input (`_UnhandledKeyInput`) |

```csharp
// Global input logging for analytics
[Rite.OnUnhandledInput]
private static void LogInput(InputEvent e) => Analytics.RecordInput(e);

// Debug: print all key presses
[Rite.OnInput]
private static void DebugKeys(InputEvent e)
{
    if (e is InputEventKey key)
        GD.Print($"Key: {key.Keycode}");
}

// Keep getting input while the tree is paused, e.g. in a pause menu
[Rite.OnInput(WhilePaused = true)]
private static void ReleaseFocus(InputEventMouseButton click) { }
```

Input rites pause with the game, as a pausable node's input does, unless they set `WhilePaused`.

---

## UI - Buttons

Applies to `BaseButton` and all subtypes (Button, CheckBox, CheckButton, etc.)

| Attribute | When it fires |
|-----------|---------------|
| `[Rite.OnButtonPressed]` | Button clicked |
| `[Rite.OnButtonUp]` | Button released |
| `[Rite.OnButtonDown]` | Button pressed down |
| `[Rite.OnButtonToggled]` | Toggle state changed |

```csharp
// Global click feedback for ALL buttons
[Rite.OnButtonPressed]
private static void ClickFeedback(BaseButton btn) => Sounds.Play("click");

// Only for CheckBox nodes
[Rite.OnButtonPressed]
private static void OnCheckBox(CheckBox cb) => GD.Print($"Checkbox: {cb.Name}");

// Track all toggle states
[Rite.OnButtonToggled]
private static void OnToggle(bool pressed, BaseButton btn) 
    => GD.Print($"{btn.Name}: {pressed}");
```

---

## UI - Text Input

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnTextChanged]` | Text content changes | LineEdit, TextEdit |
| `[Rite.OnTextSubmitted]` | Enter pressed | LineEdit |

```csharp
// Log all text input across the game
[Rite.OnTextChanged]
private static void LogText(string text, LineEdit input) 
    => GD.Print($"{input.Name}: {text}");
```

---

## UI - Value Controls

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnValueChanged]` | Numeric value changes | Range (Slider, SpinBox, ProgressBar, ScrollBar) |

```csharp
// React to any slider change
[Rite.OnValueChanged]
private static void OnAnySlider(double value, Slider slider) 
    => GD.Print($"{slider.Name}: {value}");
```

---

## UI - Selection Controls

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnItemSelected]` | Item selected | ItemList, OptionButton |
| `[Rite.OnItemActivated]` | Item double-clicked/Enter | ItemList |
| `[Rite.OnTabChanged]` | Tab switched | TabContainer, TabBar |

Index parameters accept `int` or `long`.

```csharp
// Track all dropdown selections
[Rite.OnItemSelected]
private static void OnDropdown(int index, OptionButton dropdown) 
    => Analytics.Track("dropdown_select", dropdown.Name, index);
```

---

## UI - Color & File

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnColorChanged]` | Color value changes | ColorPicker, ColorPickerButton |
| `[Rite.OnFileSelected]` | File chosen | FileDialog |
| `[Rite.OnDirSelected]` | Directory chosen | FileDialog |

```csharp
// Log all file selections
[Rite.OnFileSelected]
private static void OnFile(string path, FileDialog dialog) 
    => GD.Print($"Selected: {path}");
```

---

## UI - Focus & Mouse

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnFocusEntered]` | Control gains focus | Control |
| `[Rite.OnFocusExited]` | Control loses focus | Control |
| `[Rite.OnMouseEntered]` | Mouse enters | Control, CollisionObject2D, CollisionObject3D |
| `[Rite.OnMouseExited]` | Mouse exits | Control, CollisionObject2D, CollisionObject3D |
| `[Rite.OnResized]` | Control size changes | Control |
| `[Rite.OnMinimumSizeChanged]` | Minimum size changes | Control |
| `[Rite.OnGuiInput]` | GUI input received | Control |

```csharp
// Global focus highlighting for accessibility
[Rite.OnFocusEntered]
private static void Highlight(Control c) => c.Modulate = Colors.Yellow;

[Rite.OnFocusExited]
private static void Unhighlight(Control c) => c.Modulate = Colors.White;

// Hover effects for all buttons
[Rite.OnMouseEntered]
private static void HoverStart(BaseButton btn) => btn.Scale = Vector2.One * 1.1f;

[Rite.OnMouseExited]
private static void HoverEnd(BaseButton btn) => btn.Scale = Vector2.One;
```

---

## UI - Sliders & Scroll

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnDragStarted]` | Slider drag begins | Slider (HSlider, VSlider) |
| `[Rite.OnDragEnded]` | Slider drag ends | Slider |
| `[Rite.OnScrollStarted]` | Scroll begins | ScrollContainer |
| `[Rite.OnScrollEnded]` | Scroll ends | ScrollContainer |
| `[Rite.OnSortChildren]` | Children need sorting | Container |

```csharp
// Haptic feedback when slider drag ends
[Rite.OnDragEnded]
private static void OnDragEnd(bool changed, Slider slider)
{
    if (changed) Input.VibrateHandheld(50);
}
```

---

## Visibility

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnVisibilityChanged]` | Visibility changes | CanvasItem (2D), Node3D (3D) |

```csharp
// Auto-pause offscreen audio for performance
[Rite.OnVisibilityChanged]
private static void ManageAudio(AudioStreamPlayer2D player) 
    => player.StreamPaused = !player.Visible;

// Disable processing for hidden enemies
[Rite.OnVisibilityChanged]
private static void ManageEnemies(Enemy enemy) 
    => enemy.SetProcess(enemy.Visible);
```

---

## Physics

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnBodyEntered]` | Body enters area | Area2D, Area3D |
| `[Rite.OnBodyExited]` | Body exits area | Area2D, Area3D |
| `[Rite.OnAreaEntered]` | Area enters area | Area2D, Area3D |
| `[Rite.OnAreaExited]` | Area exits area | Area2D, Area3D |

```csharp
// Track all physics collisions for analytics
[Rite.OnBodyEntered]
private static void OnCollision(Node2D body, Area2D area) 
    => Analytics.Track("collision", area.Name, body.Name);

// Only react to player entering areas
[Rite.OnBodyEntered]
private static void OnPlayerEnter(Player player, Area2D area) 
    => GD.Print($"Player entered {area.Name}");
```

**Note:** You cannot mix 2D and 3D types in the same method.

---

## Animation

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnAnimationStarted]` | Animation begins | AnimationPlayer |
| `[Rite.OnAnimationFinished]` | Animation completes | AnimationPlayer |

```csharp
// Log all animation activity
[Rite.OnAnimationFinished]
private static void OnAnimEnd(StringName animName, AnimationPlayer player) 
    => GD.Print($"{player.Name} finished: {animName}");
```

---

## Audio

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnAudioFinished]` | Audio playback completes | AudioStreamPlayer, AudioStreamPlayer2D, AudioStreamPlayer3D |

```csharp
// Auto-cleanup one-shot sounds
[Rite.OnAudioFinished]
private static void Cleanup(AudioStreamPlayer2D player)
{
    if (player.IsInGroup("one_shot"))
        player.QueueFree();
}
```

---

## Timers

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnTimeout]` | Timer completes | Timer |

```csharp
// Log all timer completions
[Rite.OnTimeout]
private static void OnTimer(Timer timer) 
    => GD.Print($"Timer fired: {timer.Name}");
```

---

## Popups & Dialogs

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnPopupHide]` | Popup closes | Popup (PopupMenu, PopupPanel, etc.) |
| `[Rite.OnCloseRequested]` | Window close requested | Window |
| `[Rite.OnConfirmed]` | Dialog confirmed (OK) | AcceptDialog, ConfirmationDialog |
| `[Rite.OnCanceled]` | Dialog canceled | AcceptDialog, ConfirmationDialog |

```csharp
// Track all dialog confirmations
[Rite.OnConfirmed]
private static void OnConfirm(AcceptDialog dialog) 
    => Analytics.Track("dialog_confirmed", dialog.Name);
```

---

## Any Signal

`[Rite.OnSignal("name")]` hooks a signal on every node in the tree that has it, your own `[Signal]` delegates included.
It covers every signal the dedicated rites don't.

Parameters are optional. The first may be the source node (any node type or interface), which also filters which
nodes count. The rest are the signal's arguments: all of them or none, converted to the types you declare. An object
argument whose value isn't of the declared type skips the call.

```csharp
// Source node, which also filters to Timers
[Rite.OnSignal("timeout")]
private static void AnyTimeout(Timer timer) { }

// Source and arguments
[Rite.OnSignal("value_changed", Target = "UI/Volume")]
private static void VolumeChanged(Range slider, double value) { }

// Your own signal; nameof keeps the name in step with the delegate
[Rite.OnSignal(nameof(Player.SignalName.Died))]
private static void PlayerDied(Player player) { }

// Arguments only, filtered by the argument's type
[Rite.OnSignal("body_entered")]
private static void PlayerEntered(Player body) { }
```

When the signal has exactly one argument and the method one parameter, that parameter is the argument, unless it's a
node type and the argument isn't an object. Take both `(source, argument)` to be explicit. Signals with up to nine
arguments are supported. Signals on objects outside the tree (a Tween, a Resource, the SceneTree itself) aren't reached.

These rites are shorthand built on it:

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[Rite.OnIdPressed]` | Menu item chosen | PopupMenu, MenuButton (its popup) |
| `[Rite.OnMetaClicked]` | Meta tag (`[url]`) clicked | RichTextLabel |
| `[Rite.OnScreenEntered]` | Enters the screen | VisibleOnScreenNotifier2D, VisibleOnScreenNotifier3D |
| `[Rite.OnScreenExited]` | Leaves the screen | VisibleOnScreenNotifier2D, VisibleOnScreenNotifier3D |

```csharp
[Rite.OnIdPressed]
private static void MenuItemPressed(MenuButton menu, int id) { }

[Rite.OnMetaClicked]
private static void LinkClicked(RichTextLabel label, string meta) => OS.ShellOpen(meta);
```

---

## Interface Rites

Put a rite on an interface member, and every node that implements the interface runs its own implementation (or the
interface's default body) on its own signals and lifecycle.

```csharp
public interface IHoverable
{
    [Rite.OnMouseEntered] void HoverStart();
    [Rite.OnMouseExited] void HoverEnd() { }  // default body: implementers may skip it
}

public interface ITicking
{
    [Rite.OnProcess] void Tick(double delta);
}

public partial class Card : Control, IHoverable
{
    public void HoverStart() => Modulate = Colors.Gold;  // runs when this card's mouse_entered fires
}
```

- **Signal rites** (the dedicated ones and `OnSignal`) fire when the node itself emits the signal. Parameters are the
  signal's arguments, all or none; the node is `this`. `OnIdPressed` on a MenuButton listens to its popup.
- **`OnReady`** runs once per node, when it's ready.
- **`OnProcess` / `OnPhysicsProcess`** run per node every frame, with the delta. They turn processing on for the node,
  as overriding `_Process` would, and stop while the node can't process (paused).
- **`OnInput` / `OnUnhandledInput` / `OnUnhandledKeyInput`** pass the event to each implementing node in the tree,
  filtered by the event type you declare, while the node can process; `WhilePaused` is ignored here, since the node's
  own process mode decides.
- `Exclude` and `Priority` work; `Target` is ignored, since the rite belongs to every implementer. `Mode` doesn't apply.
- `OnNodeAdded`, `OnNodeRemoved` and `OnTreeChanged` aren't supported on interfaces; declare them static.
- Generic interfaces work: each implementation runs with its own type arguments.

A node that implements the interface but lacks the signal (an `IHoverable` that isn't a Control) is reported once.

---

## Filtering & Targeting

By default, hooks fire for **all** matching nodes. You can narrow scope:

### Type Filtering

The parameter type acts as a filter:

```csharp
// ALL buttons
[Rite.OnButtonPressed]
private static void Any(BaseButton btn) { }

// Only CheckBox
[Rite.OnButtonPressed]
private static void OnlyCheckBox(CheckBox cb) { }

// Only your custom Player class
[Rite.OnBodyEntered]
private static void OnlyPlayer(Player player, Area2D area) { }
```

### Target Specific Nodes

```csharp
// Only these specific buttons
[Rite.OnButtonPressed(Target = "UI/Play, UI/Resume")]
private static void OnPlayOrResume(BaseButton btn) { }
```

### Exclude Nodes

```csharp
// All buttons EXCEPT settings
[Rite.OnButtonPressed(Exclude = "UI/Settings")]
private static void MostButtons(BaseButton btn) { }

// Wildcards: '*' matches one path segment, '**' any number of them
[Rite.OnButtonPressed(Exclude = "UI/Menus/**, */Debug*")]
private static void GameplayButtons(BaseButton btn) { }
```

Patterns that start with a wildcard float free; all others are anchored under `/root/` like any other path.

Signal rites (buttons, focus, hover and the rest) match `Target` and `Exclude` against the path a node had when it
entered the tree. Renaming a node that's already in the tree doesn't change which of them it gets; moving it with
`Reparent` does.

### Priority

Control execution order (lower runs first):

```csharp
[Rite.OnReady(Priority = -100)]
private static void FirstThing() { }

[Rite.OnReady(Priority = 100)]
private static void LastThing() { }
```

### Path Shorthand

Paths are relative to `/root/` by default:

```csharp
Target = "UI/Button"        // → /root/UI/Button
Target = "/MyAutoload/Btn"  // → /MyAutoload/Btn (absolute)
```

---

## Modes

### OnReady Modes

| Mode | When it fires |
|------|---------------|
| `RootReady` (default) | Once when `/root` is ready. With `Target`: for the node at each path, and again for each new node that becomes ready there (a node only moved there doesn't count). |
| `Immediate` | During Liturgy initialization, before `/root` is ready. |
| `PerNode` | For each matching node as it becomes ready. |

```csharp
// RootReady mode (default) - fires once when /root is ready
[Rite.OnReady]
private static void GameStart() { }

// RootReady mode with target - fires when specific node is ready
[Rite.OnReady(Target = "Main/Player")]
private static void PlayerReady(Player player) { }

// Immediate mode - fires before /root is ready
[Rite.OnReady(Mode = Rite.ReadyMode.Immediate)]
private static void EarlySetup() { }

// PerNode mode - fires for each matching node
[Rite.OnReady(Mode = Rite.ReadyMode.PerNode)]
private static void EachEnemy(Enemy e) => EnemyManager.Register(e);
```

**Note:** `[Rite.AssignNode]` assignments happen after `/root` is ready but before `RootReady` callbacks fire, so your static references are available in `[Rite.OnReady]` methods.

### OnProcess/OnPhysicsProcess Modes

```csharp
// Global mode (default) - fires once per frame
[Rite.OnProcess]
private static void Update(double delta) { }

// PerNode mode - fires for each matching node
[Rite.OnProcess(Mode = Rite.FrameMode.PerNode)]
private static void UpdateEnemies(double delta, Enemy e) { }
```

---

## Parameter Flexibility

Methods accept various parameter combinations. Order generally doesn't matter:

```csharp
// No parameters
[Rite.OnButtonPressed]
private static void Click() { }

// Just the source node
[Rite.OnButtonPressed]
private static void Click(BaseButton btn) { }

// Signal value + source (either order)
[Rite.OnValueChanged]
private static void OnValue(double value, Slider slider) { }

[Rite.OnValueChanged]
private static void OnValue(Slider slider, double value) { }  // Also valid
```

---

## Modifying the Scene Tree

If you need to add nodes during `[Rite.OnReady]`, use `CallDeferred`:

```csharp
[Rite.OnReady]
private static void Setup(Window root)
{
    var node = new Node();
    root.CallDeferred(Node.MethodName.AddChild, node);
}
```

This is a Godot limitation — nodes can't be added while `_Ready` is processing.

---

## Debug Mode

Enable verbose logging and report generation:

```csharp
[assembly: Rite.Settings(Debug = true)]
```

When enabled and running from the editor (exported builds ignore it), Liturgy writes a timestamped report to `user://Liturgy/Logs/` showing all registered hooks and any
validation errors (the console prints the full path). The ten most recent reports are kept. For a live view of the
same rites while the game runs, open [the harness](#the-harness).

---

## Performance Notes

### PerNode Mode Warning

Using `PerNode` mode without type filtering fires for *every* node:

```csharp
// ⚠️ EXPENSIVE - fires for ALL nodes!
[Rite.OnReady(Mode = Rite.ReadyMode.PerNode)]
private static void EveryNode(Node n) { }

// ✅ BETTER - only fires for Enemy nodes
[Rite.OnReady(Mode = Rite.ReadyMode.PerNode)]
private static void EachEnemy(Enemy e) { }
```

### Best Practices

1. **Use type filtering** — Specify the most specific type you need
2. **Use Target for specific nodes** — More efficient than global + filter
3. **Avoid PerNode OnProcess** — Consider `[Rite.OnNodeAdded]` with manual tracking

---

## Compile-Time Checks

The package's analyzer flags rites that can never run, as you type:

| ID | Severity | Problem |
|----|----------|---------|
| LIT001 | Error | Rite on an instance method that isn't an interface member |
| LIT002 | Error | Parameters don't fit the rite (or, for `OnSignal` on a known signal, the signal's arguments) |
| LIT003 | Error | Rite that isn't supported on interface members |
| LIT004 | Warning | `Target` on an interface rite, where it's ignored |
| LIT005 | Error | `OnSignal` without a signal name |
| LIT006 | Error | A rite or static `AssignNode` in a generic class, which Liturgy can't call or assign |
| LIT007 | Warning | A newer Godot than Liturgy was tested with (see below) |
| LIT008 | Error | `AssignNode` on a member Liturgy can't assign: an instance member of a class, readonly, no setter, or a type that's neither a node type nor an interface |

`OnSignal` is checked against engine signals and your own `[Signal]` delegates when the source parameter's type
declares the signal; otherwise it's checked at runtime and reported in the debug report.

**After a Godot upgrade:** Liturgy starts by having Godot build an attribute while it loads scripts. If a Godot
release ever stops doing that, Liturgy never starts, and none of its code runs to tell you. LIT007 warns at build time
when your Godot version is newer than the one Liturgy was tested with; run the game once and check that
`/root/Liturgy` exists.

It also silences warnings that are wrong about Liturgy's members: `CS0649` (never assigned) and `IDE0044` (make
readonly) on `AssignNode` fields, and `IDE0051`/`IDE0052` (unused) on rites and `AssignNode` members.

---

## When a Rite Throws

An exception in a rite is logged with its stack trace, and the other rites on the same event still run, as they would
as separate signal connections. A rite or `AssignNode` that Liturgy can't use at all (a generic class, a bad signature)
is skipped and reported; the rest of Liturgy carries on.

---

## Rider and ReSharper

Every hook attribute is marked as an implicit use, so methods and fields that carry one are never reported as unused.

---

## Suppressing Warnings

If Liturgy warns about a pattern you're intentionally using, suppress it per-method:

```csharp
// This would normally warn about firing every frame for every node
[Rite.OnProcess(Mode = Rite.FrameMode.PerNode)]
[Rite.SuppressWarnings]
private static void UpdateAllNodes(double delta, Node node) { }
```

---

## Building from Source

Open `Liturgy.sln`. It holds the library (`src/Liturgy`), the source generator (`src/Liturgy.Generators`) and
`sandbox/`, a Godot project whose test bed exercises every hook category (open `sandbox/project.godot` and run it).

`sandbox/Checks/Checks.tscn` runs automated checks for `OnSignal`, interface rites, reparenting, pausing, a rite
library loaded late (`sandbox/Library`), diagnostics, the ignore group and the harness. Run that scene; it prints each check, ends with `ALL PASSED`, and exits with
code 1 on a failure, so it also runs headless:

```
godot --headless --path sandbox res://Checks/Checks.tscn
```

Pack with:

```
dotnet pack src/Liturgy -c ExportRelease
```

Before each release, raise `<Version>` in `src/Liturgy/Liturgy.csproj` and add a section for it at the top of
`CHANGELOG.md`. The changelog is built into the package, and the harness shows each project the sections it hasn't
seen yet.

---

## License

MIT. See [LICENSE](https://github.com/Oddness-1/Liturgy/blob/master/LICENSE).
