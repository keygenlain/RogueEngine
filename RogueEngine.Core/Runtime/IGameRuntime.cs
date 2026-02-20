using RogueEngine.Core.Runtime.Platform;

namespace RogueEngine.Core.Runtime;

/// <summary>
/// Minimal runtime contract executed by <see cref="EngineLoop"/>.
/// </summary>
public interface IGameRuntime
{
    void Initialize(IEnginePlatform platform);

    void FixedUpdate(FrameTime time);

    void Render(FrameTime time);

    void Shutdown();
}
