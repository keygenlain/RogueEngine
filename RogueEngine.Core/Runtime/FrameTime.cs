namespace RogueEngine.Core.Runtime;

/// <summary>
/// Immutable timing snapshot passed to runtime update/render steps.
/// </summary>
public readonly record struct FrameTime(
    double DeltaSeconds,
    double TotalSeconds,
    long FrameNumber);
