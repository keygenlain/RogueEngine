namespace RogueEngine.Core.Runtime.Ecs;

public interface ISimulationSystem
{
    string Name { get; }

    void Update(World world, FrameTime time);
}
