namespace RogueEngine.Core.Runtime.Ecs;

/// <summary>
/// Applies velocity to position every fixed update.
/// </summary>
public sealed class MovementSystem : ISimulationSystem
{
    public string Name => nameof(MovementSystem);

    public void Update(World world, FrameTime time)
    {
        var ids = world.QueryEntitiesWith<Position, Velocity>().ToList();
        foreach (var entityId in ids)
        {
            if (!world.TryGet<Position>(entityId, out var pos)) continue;
            if (!world.TryGet<Velocity>(entityId, out var vel)) continue;

            world.Set(entityId, new Position(pos.X + vel.DX, pos.Y + vel.DY));
        }
    }
}
