# Changelog

Each release gets a section here, newest first. Liturgy shows the sections a project hasn't seen yet in its update greeting, so write them for the people using it: what they can now do, and anything they must change.

## [0.1.0]

First release.

- Rites for the node lifecycle, frames, input, the scene tree, every common UI control, visibility, physics, animation, audio, timers and dialogs.
- `[Rite.OnSignal("name")]` for any signal, including your own `[Signal]`s.
- Interface rites: a rite on an interface member runs for each node that implements it.
- `[Rite.AssignNode]` fills static fields from the tree before the first ready rite, keeps them following their path as nodes come and go, and takes interfaces as well as node types.
- Target, Exclude, type filters and Priority on node rites.
- Compile-time checks (LIT001 to LIT007) for rites Liturgy can't run.
- The harness: press Alt+~ in a run from the editor to see every rite, the nodes it touches, how often it fires and how long it takes, every `AssignNode` member, and a live log. Mute a rite to see what changes without it. Sort by any heading; resize the panel, its panes and its columns, and it remembers.
- `Description` on every rite, shown in the harness.
- The `liturgy_ignore` group hides a node and everything under it from every rite.
- `Rite.Diagnostics`, the data behind the harness, for your own dev tools.
