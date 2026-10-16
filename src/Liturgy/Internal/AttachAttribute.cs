using System;
using System.ComponentModel;

namespace Liturgy.Internal;

/// <summary>
/// Attaches Liturgy when Godot reads a script class's attributes. Liturgy's source generator puts it on every Godot
/// class in an assembly that references Liturgy; not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AttachAttribute : Attribute
{
    public AttachAttribute() => Startup.Attach();
}
