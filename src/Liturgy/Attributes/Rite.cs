namespace Liturgy;

/// <summary>
/// Liturgy's attributes. Each one is a rite: mark a static method with it, and Liturgy performs it for every
/// matching node across the scene tree, e.g. <c>[Rite.OnReady]</c> or <c>[Rite.OnButtonPressed]</c>. Mark an
/// interface member instead, and each node that implements the interface runs its own implementation.
/// </summary>
public static partial class Rite { }
