namespace RogueEngine.Core.Runtime;

/// <summary>
/// Options that control update and render cadence for <see cref="EngineLoop"/>.
/// </summary>
public sealed class EngineLoopOptions
{
    /// <summary>
    /// Target fixed simulation step in hertz (updates per second).
    /// </summary>
    public int FixedUpdateHz { get; set; } = 60;

    /// <summary>
    /// Maximum number of fixed updates to process in a single frame.
    /// Guards against spiral-of-death under heavy load.
    /// </summary>
    public int MaxUpdatesPerFrame { get; set; } = 5;
}
