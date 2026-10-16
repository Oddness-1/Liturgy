using System;

namespace Liturgy;

/// <summary>
/// What every rite has in common. Mark methods with the rites nested in <see cref="Rite"/>, not with this.
/// </summary>
public abstract class RiteAttribute : Attribute
{
    /// <summary>
    /// What the rite is for, in a few words. Liturgy's harness shows it beside the rite and on every node the rite
    /// touches, so a reader can tell what's acting on a node without opening the code.
    /// </summary>
    /// <example>
    /// <code>
    /// [Rite.OnButtonPressed(Description = "Click sound on every button")]
    /// private static void Click() => Sounds.Click.Play();
    /// </code>
    /// </example>
    public string? Description { get; set; }
}
