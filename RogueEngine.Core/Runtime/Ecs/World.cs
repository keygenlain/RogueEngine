namespace RogueEngine.Core.Runtime.Ecs;

/// <summary>
/// Lightweight entity-component world for runtime systems.
/// </summary>
public sealed class World
{
    private readonly HashSet<Guid> _entities = [];
    private readonly Dictionary<Type, object> _componentStores = [];

    public IReadOnlyCollection<Guid> Entities => _entities;

    public Guid CreateEntity()
    {
        var id = Guid.NewGuid();
        _entities.Add(id);
        return id;
    }

    public bool DestroyEntity(Guid entityId)
    {
        if (!_entities.Remove(entityId))
            return false;

        foreach (var store in _componentStores.Values)
        {
            var remove = store.GetType().GetMethod("Remove");
            remove?.Invoke(store, [entityId]);
        }

        return true;
    }

    public void Set<T>(Guid entityId, T component) where T : struct
    {
        EnsureEntity(entityId);
        var store = GetStore<T>();
        store[entityId] = component;
    }

    public bool TryGet<T>(Guid entityId, out T component) where T : struct
    {
        var store = GetStore<T>();
        return store.TryGetValue(entityId, out component);
    }

    public bool Has<T>(Guid entityId) where T : struct => GetStore<T>().ContainsKey(entityId);

    public IEnumerable<(Guid Entity, T Component)> Query<T>() where T : struct
    {
        foreach (var pair in GetStore<T>())
            yield return (pair.Key, pair.Value);
    }

    public IEnumerable<Guid> QueryEntitiesWith<TA, TB>() where TA : struct where TB : struct
    {
        var a = GetStore<TA>();
        var b = GetStore<TB>();
        foreach (var id in _entities)
            if (a.ContainsKey(id) && b.ContainsKey(id))
                yield return id;
    }

    private Dictionary<Guid, T> GetStore<T>() where T : struct
    {
        if (!_componentStores.TryGetValue(typeof(T), out var store))
        {
            store = new Dictionary<Guid, T>();
            _componentStores[typeof(T)] = store;
        }
        return (Dictionary<Guid, T>)store;
    }

    private void EnsureEntity(Guid entityId)
    {
        if (!_entities.Contains(entityId))
            throw new InvalidOperationException($"Entity {entityId} does not exist.");
    }
}
