using RogueEngine.Core.Models;

namespace RogueEngine.Core.Runtime.Rendering;

public interface IRenderer
{
    void BeginFrame(FrameTime time);

    void DrawMap(AsciiMap map);

    void DrawEntities(IEnumerable<Entity> entities);

    void EndFrame();
}
