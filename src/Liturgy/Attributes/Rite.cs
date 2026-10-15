namespace Liturgy;

/// <summary>
/// Liturgy's attributes. Each one is a rite: mark a static method with it, and Liturgy performs it for every
/// matching node across the scene tree, e.g. <c>[Rite.OnReady]</c> or <c>[Rite.OnButtonPressed]</c>.
/// </summary>
public static partial class Rite { }
