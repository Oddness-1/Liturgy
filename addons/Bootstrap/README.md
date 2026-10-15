# Bootstrap

Attribute-based hooks for Godot C#. Write static methods, add attributes, and Bootstrap wires them up automatically — globally, across your entire project.

## Why Bootstrap?

In Godot, applying behavior to many nodes is tedious:

- **Wire signals in the editor?** Manual, error-prone, doesn't scale.
- **Inheritance hierarchies?** Couples everything, limits flexibility.
- **Autoloads that iterate the tree?** You're reinventing Bootstrap.
- **Manual registration in every `_Ready`?** Boilerplate everywhere.

Bootstrap lets you say *"for all X, do Y"* in one place:

```csharp
// Auto-register every Enemy when it enters the scene
[OnReady(Mode = ReadyMode.PerNode)]
private static void Register(Enemy enemy) => EnemyManager.Track(enemy);

// Play a sound when ANY button is pressed
[OnButtonPressed]
private static void ClickSound(BaseButton btn) => Sounds.Play("click");

// Log every unhandled input (debugging/analytics)
[OnUnhandledInput]
private static void LogInput(InputEvent e) => Analytics.Record(e);

// Highlight ANY focused control (accessibility)
[OnFocusEntered]
private static void Highlight(Control c) => c.Modulate = Colors.Yellow;

[OnFocusExited]
private static void Unhighlight(Control c) => c.Modulate = Colors.White;

// Pause offscreen audio (performance)
[OnVisibilityChanged]
private static void PauseOffscreen(AudioStreamPlayer2D player) 
    => player.StreamPaused = !player.Visible;
```

No manual wiring. No base classes. No forgetting to connect signals. It just works.

## Installation

1. Copy the `addons/Bootstrap` folder to your project
2. Enable the plugin in Project Settings → Plugins

## Table of Contents

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
- [Filtering & Targeting](#filtering--targeting)
- [Modes](#modes)
- [Parameter Flexibility](#parameter-flexibility)
- [Modifying the Scene Tree](#modifying-the-scene-tree)
- [Debug Mode](#debug-mode)
- [Performance Notes](#performance-notes)

---

## Node Lifecycle

| Attribute | When it fires |
|-----------|---------------|
| `[OnReady]` | RootReady mode (default): once when `/root` is ready. Immediate mode: during Bootstrap init. PerNode mode: each node's `_Ready` |
| `[OnNodeAdded]` | Node added to tree (before `_Ready`) |
| `[OnNodeRemoved]` | Node removed from tree |
| `[OnTreeChanged]` | Any tree structure change |

```csharp
// Fire once when /root is ready (default)
[OnReady]
private static void GameStart() => GD.Print("Game started!");

// Fire before /root is ready (during Bootstrap initialization)
[OnReady(Mode = ReadyMode.Immediate)]
private static void EarlySetup() => GD.Print("Early setup!");

// Auto-register every Enemy as it enters the scene
[OnReady(Mode = ReadyMode.PerNode)]
private static void Register(Enemy enemy) => EnemyManager.Track(enemy);

// Clean up when nodes are removed
[OnNodeRemoved]
private static void Cleanup(Enemy enemy) => EnemyManager.Untrack(enemy);
```

---

## AssignNode

Automatically assign scene tree nodes to static fields or properties when `/root` is ready.

```csharp
// Assign an autoload
[AssignNode("/root/GameManager")]
private static GameManager GM;

// Relative paths resolve from /root/
[AssignNode("Main/Player")]
private static Player Player;

// Works with properties too
[AssignNode("/root/AudioManager")]
private static AudioManager Audio { get; set; }
```

**Key points:**
- Assignments happen when `/root` is ready, **before** any `[OnReady]` callbacks in `RootReady` mode
- This means your `[OnReady]` methods can safely reference these static fields
- If a node isn't found at the path, a warning is logged and the field is skipped
- The field/property type must match the node type (or be a base type)

```csharp
// Safe to use GM here — it's already assigned
[OnReady]
private static void Init() => GM.StartGame();
```

---

## Frame Processing

| Attribute | When it fires |
|-----------|---------------|
| `[OnProcess]` | Every frame (`_Process`) |
| `[OnPhysicsProcess]` | Every physics tick (`_PhysicsProcess`) |

```csharp
// Global game loop
[OnProcess]
private static void Update(double delta) => GameManager.Tick(delta);

// Update specific node
[OnProcess(Target = "Player")]
private static void UpdatePlayer(double delta, CharacterBody3D player) { }
```

---

## Input

| Attribute | When it fires |
|-----------|---------------|
| `[OnInput]` | Any input event (`_Input`) |
| `[OnUnhandledInput]` | Unhandled input (`_UnhandledInput`) |
| `[OnUnhandledKeyInput]` | Unhandled key input (`_UnhandledKeyInput`) |

```csharp
// Global input logging for analytics
[OnUnhandledInput]
private static void LogInput(InputEvent e) => Analytics.RecordInput(e);

// Debug: print all key presses
[OnInput]
private static void DebugKeys(InputEvent e)
{
    if (e is InputEventKey key)
        GD.Print($"Key: {key.Keycode}");
}
```

---

## UI - Buttons

Applies to `BaseButton` and all subtypes (Button, CheckBox, CheckButton, etc.)

| Attribute | When it fires |
|-----------|---------------|
| `[OnButtonPressed]` | Button clicked |
| `[OnButtonUp]` | Button released |
| `[OnButtonDown]` | Button pressed down |
| `[OnButtonToggled]` | Toggle state changed |

```csharp
// Global click feedback for ALL buttons
[OnButtonPressed]
private static void ClickFeedback(BaseButton btn) => Sounds.Play("click");

// Only for CheckBox nodes
[OnButtonPressed]
private static void OnCheckBox(CheckBox cb) => GD.Print($"Checkbox: {cb.Name}");

// Track all toggle states
[OnButtonToggled]
private static void OnToggle(bool pressed, BaseButton btn) 
    => GD.Print($"{btn.Name}: {pressed}");
```

---

## UI - Text Input

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnTextChanged]` | Text content changes | LineEdit, TextEdit |
| `[OnTextSubmitted]` | Enter pressed | LineEdit |

```csharp
// Log all text input across the game
[OnTextChanged]
private static void LogText(string text, LineEdit input) 
    => GD.Print($"{input.Name}: {text}");
```

---

## UI - Value Controls

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnValueChanged]` | Numeric value changes | Range (Slider, SpinBox, ProgressBar, ScrollBar) |

```csharp
// React to any slider change
[OnValueChanged]
private static void OnAnySlider(double value, Slider slider) 
    => GD.Print($"{slider.Name}: {value}");
```

---

## UI - Selection Controls

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnItemSelected]` | Item selected | ItemList, OptionButton |
| `[OnItemActivated]` | Item double-clicked/Enter | ItemList |
| `[OnTabChanged]` | Tab switched | TabContainer, TabBar |

Index parameters accept `int` or `long`.

```csharp
// Track all dropdown selections
[OnItemSelected]
private static void OnDropdown(int index, OptionButton dropdown) 
    => Analytics.Track("dropdown_select", dropdown.Name, index);
```

---

## UI - Color & File

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnColorChanged]` | Color value changes | ColorPicker, ColorPickerButton |
| `[OnFileSelected]` | File chosen | FileDialog |
| `[OnDirSelected]` | Directory chosen | FileDialog |

```csharp
// Log all file selections
[OnFileSelected]
private static void OnFile(string path, FileDialog dialog) 
    => GD.Print($"Selected: {path}");
```

---

## UI - Focus & Mouse

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnFocusEntered]` | Control gains focus | Control |
| `[OnFocusExited]` | Control loses focus | Control |
| `[OnMouseEntered]` | Mouse enters | Control, CollisionObject2D, CollisionObject3D |
| `[OnMouseExited]` | Mouse exits | Control, CollisionObject2D, CollisionObject3D |
| `[OnResized]` | Control size changes | Control |
| `[OnMinimumSizeChanged]` | Minimum size changes | Control |
| `[OnGuiInput]` | GUI input received | Control |

```csharp
// Global focus highlighting for accessibility
[OnFocusEntered]
private static void Highlight(Control c) => c.Modulate = Colors.Yellow;

[OnFocusExited]
private static void Unhighlight(Control c) => c.Modulate = Colors.White;

// Hover effects for all buttons
[OnMouseEntered]
private static void HoverStart(BaseButton btn) => btn.Scale = Vector2.One * 1.1f;

[OnMouseExited]
private static void HoverEnd(BaseButton btn) => btn.Scale = Vector2.One;
```

---

## UI - Sliders & Scroll

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnDragStarted]` | Slider drag begins | Slider (HSlider, VSlider) |
| `[OnDragEnded]` | Slider drag ends | Slider |
| `[OnScrollStarted]` | Scroll begins | ScrollContainer |
| `[OnScrollEnded]` | Scroll ends | ScrollContainer |
| `[OnSortChildren]` | Children need sorting | Container |

```csharp
// Haptic feedback when slider drag ends
[OnDragEnded]
private static void OnDragEnd(bool changed, Slider slider)
{
    if (changed) Input.VibrateHandheld(50);
}
```

---

## Visibility

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnVisibilityChanged]` | Visibility changes | CanvasItem (2D), Node3D (3D) |

```csharp
// Auto-pause offscreen audio for performance
[OnVisibilityChanged]
private static void ManageAudio(AudioStreamPlayer2D player) 
    => player.StreamPaused = !player.Visible;

// Disable processing for hidden enemies
[OnVisibilityChanged]
private static void ManageEnemies(Enemy enemy) 
    => enemy.SetProcess(enemy.Visible);
```

---

## Physics

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnBodyEntered]` | Body enters area | Area2D, Area3D |
| `[OnBodyExited]` | Body exits area | Area2D, Area3D |
| `[OnAreaEntered]` | Area enters area | Area2D, Area3D |
| `[OnAreaExited]` | Area exits area | Area2D, Area3D |

```csharp
// Track all physics collisions for analytics
[OnBodyEntered]
private static void OnCollision(Node2D body, Area2D area) 
    => Analytics.Track("collision", area.Name, body.Name);

// Only react to player entering areas
[OnBodyEntered]
private static void OnPlayerEnter(Player player, Area2D area) 
    => GD.Print($"Player entered {area.Name}");
```

**Note:** You cannot mix 2D and 3D types in the same method.

---

## Animation

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnAnimationStarted]` | Animation begins | AnimationPlayer |
| `[OnAnimationFinished]` | Animation completes | AnimationPlayer |

```csharp
// Log all animation activity
[OnAnimationFinished]
private static void OnAnimEnd(StringName animName, AnimationPlayer player) 
    => GD.Print($"{player.Name} finished: {animName}");
```

---

## Audio

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnAudioFinished]` | Audio playback completes | AudioStreamPlayer, AudioStreamPlayer2D, AudioStreamPlayer3D |

```csharp
// Auto-cleanup one-shot sounds
[OnAudioFinished]
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
| `[OnTimeout]` | Timer completes | Timer |

```csharp
// Log all timer completions
[OnTimeout]
private static void OnTimer(Timer timer) 
    => GD.Print($"Timer fired: {timer.Name}");
```

---

## Popups & Dialogs

| Attribute | When it fires | Controls |
|-----------|---------------|----------|
| `[OnPopupHide]` | Popup closes | Popup (PopupMenu, PopupPanel, etc.) |
| `[OnCloseRequested]` | Window close requested | Window |
| `[OnConfirmed]` | Dialog confirmed (OK) | AcceptDialog, ConfirmationDialog |
| `[OnCanceled]` | Dialog canceled | AcceptDialog, ConfirmationDialog |

```csharp
// Track all dialog confirmations
[OnConfirmed]
private static void OnConfirm(AcceptDialog dialog) 
    => Analytics.Track("dialog_confirmed", dialog.Name);
```

---

## Filtering & Targeting

By default, hooks fire for **all** matching nodes. You can narrow scope:

### Type Filtering

The parameter type acts as a filter:

```csharp
// ALL buttons
[OnButtonPressed]
private static void Any(BaseButton btn) { }

// Only CheckBox
[OnButtonPressed]
private static void OnlyCheckBox(CheckBox cb) { }

// Only your custom Player class
[OnBodyEntered]
private static void OnlyPlayer(Player player, Area2D area) { }
```

### Target Specific Nodes

```csharp
// Only these specific buttons
[OnButtonPressed(Target = "UI/Play, UI/Resume")]
private static void OnPlayOrResume(BaseButton btn) { }
```

### Exclude Nodes

```csharp
// All buttons EXCEPT settings
[OnButtonPressed(Exclude = "UI/Settings")]
private static void MostButtons(BaseButton btn) { }
```

### Priority

Control execution order (lower runs first):

```csharp
[OnReady(Priority = -100)]
private static void FirstThing() { }

[OnReady(Priority = 100)]
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
| `RootReady` (default) | Once when `/root` is ready. With `Target`: when each specified node is ready. |
| `Immediate` | During Bootstrap initialization, before `/root` is ready. |
| `PerNode` | For each matching node as it becomes ready. |

```csharp
// RootReady mode (default) - fires once when /root is ready
[OnReady]
private static void GameStart() { }

// RootReady mode with target - fires when specific node is ready
[OnReady(Target = "Main/Player")]
private static void PlayerReady(Player player) { }

// Immediate mode - fires before /root is ready
[OnReady(Mode = ReadyMode.Immediate)]
private static void EarlySetup() { }

// PerNode mode - fires for each matching node
[OnReady(Mode = ReadyMode.PerNode)]
private static void EachEnemy(Enemy e) => EnemyManager.Register(e);
```

**Note:** `[AssignNode]` assignments happen after `/root` is ready but before `RootReady` callbacks fire, so your static references are available in `[OnReady]` methods.

### OnProcess/OnPhysicsProcess Modes

```csharp
// Global mode (default) - fires once per frame
[OnProcess]
private static void Update(double delta) { }

// PerNode mode - fires for each matching node
[OnProcess(Mode = FrameMode.PerNode)]
private static void UpdateEnemies(double delta, Enemy e) { }
```

---

## Parameter Flexibility

Methods accept various parameter combinations. Order generally doesn't matter:

```csharp
// No parameters
[OnButtonPressed]
private static void Click() { }

// Just the source node
[OnButtonPressed]
private static void Click(BaseButton btn) { }

// Signal value + source (either order)
[OnValueChanged]
private static void OnValue(double value, Slider slider) { }

[OnValueChanged]
private static void OnValue(Slider slider, double value) { }  // Also valid
```

---

## Modifying the Scene Tree

If you need to add nodes during `[OnReady]`, use `CallDeferred`:

```csharp
[OnReady]
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
[assembly: Bootstrap.Settings(Debug = true)]
```

When enabled, Bootstrap generates `Logs/bootstrap_report.md` showing all registered hooks and any validation errors.

---

## Performance Notes

### PerNode Mode Warning

Using `PerNode` mode without type filtering fires for *every* node:

```csharp
// ⚠️ EXPENSIVE - fires for ALL nodes!
[OnReady(Mode = ReadyMode.PerNode)]
private static void EveryNode(Node n) { }

// ✅ BETTER - only fires for Enemy nodes
[OnReady(Mode = ReadyMode.PerNode)]
private static void EachEnemy(Enemy e) { }
```

### Best Practices

1. **Use type filtering** — Specify the most specific type you need
2. **Use Target for specific nodes** — More efficient than global + filter
3. **Avoid PerNode OnProcess** — Consider `[OnNodeAdded]` with manual tracking

---

## Suppressing Warnings

If Bootstrap warns about a pattern you're intentionally using, suppress it per-method:

```csharp
// This would normally warn about firing every frame for every node
[OnProcess(Mode = FrameMode.PerNode)]
[SuppressBootstrapWarnings]
private static void UpdateAllNodes(double delta, Node node) { }
```

---

## License

MIT License - Copyright (c) 2025 Oddness

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files, to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, subject to the above copyright notice being included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.
