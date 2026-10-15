namespace Liturgy;

public static partial class Rite
{
    /// <summary>
    /// Determines when an <c>[Rite.OnReady]</c> callback fires.
    /// </summary>
    public enum ReadyMode
    {
        /// <summary>
        /// Fires once when <c>/root</c> is ready (default).
        /// With <c>Target</c>: fires once when each specified node is ready.
        /// </summary>
        RootReady,

        /// <summary>
        /// Fires immediately during Liturgy initialization, before <c>/root</c> is ready.
        /// Use this when you need to set up state before the scene tree is fully constructed.
        /// </summary>
        Immediate,

        /// <summary>
        /// Fires each time any node becomes ready.
        /// Use a typed parameter to filter which nodes trigger the callback.
        /// <para>
        /// <br/><b>Note:</b> Target is ignored in this mode. Use <c>Exclude</c> for filtering
        /// or use RootReady mode with <c>Target</c> to hook specific nodes.
        /// </para>
        /// </summary>
        PerNode
    }

    /// <summary>
    /// Determines when an <c>[Rite.OnProcess]</c> or <c>[Rite.OnPhysicsProcess]</c> callback fires.<br/>
    /// </summary>
    public enum FrameMode
    {
        /// <summary>
        /// Fires once per frame/tick (default).
        /// With <c>Target</c>: fires once per frame/tick for each specified node.
        /// </summary>
        Global,

        /// <summary>
        /// Fires for each processing node per frame/tick.
        /// Use a typed parameter to filter which nodes trigger the callback.
        /// <para>
        /// <br/><b>WARNING:</b> This creates an internal helper node for EACH matching node.
        /// Without type filtering, this effectively doubles your node count.
        /// Use <c>Target</c> with Global mode to specify exact nodes, or always filter by type.
        /// Consider whether [Rite.OnNodeAdded] with manual tracking might suit your needs better.
        /// </para>
        /// <para>
        /// <b>Note:</b> <c>Target</c> is ignored in this mode. Use <c>Exclude</c> for filtering.
        /// </para>
        /// </summary>
        PerNode
    }
}
