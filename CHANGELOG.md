# Changelog

Each release gets a section here, newest first. Liturgy shows the sections a project hasn't seen yet in its update greeting, so write them for the people using it: what they can now do, and anything they must change.

## [0.2.0]

New:
- `WhilePaused` on `OnInput`, `OnUnhandledInput` and `OnUnhandledKeyInput`: the rite keeps getting input while the tree is paused, for pause menus. Without it, input rites pause with the game as before.

Fixed:
- A rite whose parameter is a subtype of what its signal passes (`OnBodyEntered(Player player, Area2D area)`, `OnGuiInput(InputEventMouseButton click, Control control)`, a `LineEdit` or `AnimationPlayer` subclass) now runs for that subtype only. Before, it could stop every rite of its kind from running.
- A rite Liturgy can't call is reported and skipped on its own; the rest of its kind still runs.
- `OnProcess` and `OnPhysicsProcess` with `Target` or `PerNode` now run on nodes that don't process on their own (no `_Process`, or `SetProcess(false)`). They still stop while the node is paused, and they don't turn the node's own processing on.
- `OnSortChildren` now fires for `ScrollContainer`s.
- A node moved into or out of the `liturgy_ignore` group with `Reparent` is seen correctly again.
- Per-node `OnReady` no longer keeps every node that ever entered the tree in memory.
- `OnReady` with `Target` follows its path, as `AssignNode` does: a new node that becomes ready at the path (after a scene change, say) gets the rite too. Before, only the first node ever did.

Faster:
- Input reaches C# only when a project has input rites.
- Controls connect only the focus, mouse and resize signals some rite listens for.
- Per-node process rites and interface rites allocate nothing per frame, and interface rites are called through typed delegates instead of reflection.
- The startup scan walks your assemblies once.
- Signal rites allocate less per emission.

Changed:
- Button rites match `Target` and `Exclude` against the path the button had when it entered the tree, like every other signal rite.

## [0.1.0]

First release.

- Rites for the node lifecycle, frames, input, the scene tree, every common UI control, visibility, physics, animation, audio, timers and dialogs.
- `[Rite.OnSignal("name")]` for any signal, including your own `[Signal]`s.
- Interface rites: a rite on an interface member runs for each node that implements it.
- `[Rite.AssignNode]` fills static fields from the tree before the first ready rite, keeps them following their path as nodes come and go, and takes interfaces as well as node types. On an interface property, every implementing node holds its own, filled before its `_Ready`. Paths can be patterns: `**/%Camera` is the camera in whatever scene is loaded.
- Target, Exclude, type filters and Priority on node rites.
- Compile-time checks (LIT001 to LIT008) for rites Liturgy can't run and members it can't assign.
- The harness: press Alt+~ in a run from the editor to see every rite, the nodes it touches, how often it fires and how long it takes, every `AssignNode` member, and a live log. Mute a rite to see what changes without it. Sort by any heading; resize the panel, its panes and its columns, and it remembers.
- `Description` on every rite, shown in the harness.
- The `liturgy_ignore` group hides a node and everything under it from every rite.
- `Rite.Diagnostics`, the data behind the harness, for your own dev tools.
