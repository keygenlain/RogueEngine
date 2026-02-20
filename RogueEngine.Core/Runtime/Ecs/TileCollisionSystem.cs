namespace RogueEngine.Core.Runtime.Ecs;

/// <summary>
/// Prevents entities from moving into blocked map cells.
/// </summary>
public sealed class TileCollisionSystem : ISimulationSystem
{
    private readonly Func<int, int, bool> _isBlocked;

    public TileCollisionSystem(Func<int, int, bool> isBlocked)
    {
        _isBlocked = isBlocked ?? throw new ArgumentNullException(nameof(isBlocked));
    }

    public string Name => nameof(TileCollisionSystem);

    public void Update(World world, FrameTime time)
    {
        var ids = world.QueryEntitiesWith<Position, Velocity>().ToList();
        foreach (var entityId in ids)
        {
            if (!world.TryGet<Position>(entityId, out var pos)) continue;
            if (!world.TryGet<Velocity>(entityId, out var vel)) continue;

            var candidateX = pos.X + vel.DX;
            var candidateY = pos.Y + vel.DY;
            if (_isBlocked(candidateX, candidateY))
            {
                world.Set(entityId, new Velocity(0, 0));
                continue;
            }

            world.Set(entityId, new Position(candidateX, candidateY));
            world.Set(entityId, new Velocity(0, 0));
        }
    }
}
