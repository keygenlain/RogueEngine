using System.Text.Json;
using RogueEngine.Core.Models;

namespace RogueEngine.Core.Engine;

/// <summary>
/// Serialises and deserialises full game state to/from disk, allowing games
/// to be saved and loaded across sessions.
///
/// <para>
/// The <em>global persistent store</em> is a simple string→string dictionary
/// that survives save/load (e.g. quest flags, high scores).
/// Individual <see cref="OverworldLocation"/> objects carry their own
/// <see cref="OverworldLocation.PersistentData"/> dictionaries which are
/// included in the save.
/// </para>
/// </summary>
public sealed class PersistenceManager
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Global key-value persistent store.</summary>
    public Dictionary<string, string> GlobalStore { get; private set; } = [];

    // ── Save ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the current game state to a named slot.
    /// Each slot is a JSON file in <paramref name="saveDirectory"/>.
    /// </summary>
    /// <param name="slot">Slot identifier (used as the file name stem).</param>
    /// <param name="overworld">Optional overworld state to persist.</param>
    /// <param name="entities">Live entities to include in the save.</param>
    /// <param name="gameHour">Current in-game hour.</param>
    /// <param name="saveDirectory">
    /// Directory where save files are written. Defaults to
    /// <c>./saves</c> relative to the process working directory.
    /// </param>
    /// <returns><see langword="true"/> on success.</returns>
    public bool Save(
        string slot,
        Overworld? overworld = null,
        IEnumerable<Entity>? entities = null,
        int gameHour = 0,
        string? saveDirectory = null)
    {
        try
        {
            var dir = saveDirectory ?? Path.Combine(AppContext.BaseDirectory, "saves");
            Directory.CreateDirectory(dir);
            var path = SlotPath(dir, slot);

            var dto = new SaveDto(
                Slot: slot,
                Timestamp: DateTime.UtcNow,
                GameHour: gameHour,
                GlobalStore: new Dictionary<string, string>(GlobalStore),
                Locations: overworld?.Locations
                    .Select(l => new LocationSaveDto(
                        l.Id, l.Name, l.WorldX, l.WorldY,
                        l.HasBeenVisited,
                        new Dictionary<string, string>(l.PersistentData),
                        l.Entities.Select(EntityToDto).ToList()))
                    .ToList() ?? [],
                CurrentLocationId: overworld?.CurrentLocationId,
                Entities: entities?.Select(EntityToDto).ToList() ?? []);

            File.WriteAllText(path, JsonSerializer.Serialize(dto, _json));
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Load ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a save slot back into memory, restoring the global store,
    /// overworld location states, and entity list.
    /// </summary>
    /// <param name="slot">Slot identifier.</param>
    /// <param name="overworld">
    /// Overworld to restore location states into.
    /// Location references are matched by <see cref="OverworldLocation.Id"/>.
    /// Pass <see langword="null"/> to skip overworld restoration.
    /// </param>
    /// <param name="saveDirectory">Directory of save files.</param>
    /// <returns>
    /// A <see cref="LoadResult"/> with the restored entities and game hour,
    /// or <see langword="null"/> if the slot does not exist.
    /// </returns>
    public LoadResult? Load(
        string slot,
        Overworld? overworld = null,
        string? saveDirectory = null)
    {
        var dir = saveDirectory ?? Path.Combine(AppContext.BaseDirectory, "saves");
        var path = SlotPath(dir, slot);

        if (!File.Exists(path)) return null;

        try
        {
            var dto = JsonSerializer.Deserialize<SaveDto>(
                File.ReadAllText(path), _json);
            if (dto is null) return null;

            // Restore global store.
            GlobalStore = new Dictionary<string, string>(dto.GlobalStore);

            // Restore overworld location states.
            if (overworld is not null)
            {
                foreach (var ldto in dto.Locations)
                {
                    var loc = overworld.FindLocation(ldto.Id);
                    if (loc is null) continue;
                    loc.HasBeenVisited = ldto.HasBeenVisited;
                    loc.PersistentData.Clear();
                    foreach (var kv in ldto.PersistentData)
                        loc.PersistentData[kv.Key] = kv.Value;
                    loc.Entities.Clear();
                    loc.Entities.AddRange(ldto.Entities.Select(DtoToEntity));
                }
                if (dto.CurrentLocationId.HasValue)
                    overworld.SetStartLocation(dto.CurrentLocationId.Value);
            }

            var entities = dto.Entities.Select(DtoToEntity).ToList();
            return new LoadResult(entities, dto.GameHour);
        }
        catch
        {
            return null;
        }
    }

    // ── Slot management ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> if the named slot exists on disk.
    /// </summary>
    public bool SlotExists(string slot, string? saveDirectory = null)
    {
        var dir = saveDirectory ?? Path.Combine(AppContext.BaseDirectory, "saves");
        return File.Exists(SlotPath(dir, slot));
    }

    /// <summary>
    /// Deletes a save slot.
    /// </summary>
    /// <returns><see langword="true"/> if the file was found and deleted.</returns>
    public bool DeleteSlot(string slot, string? saveDirectory = null)
    {
        var dir = saveDirectory ?? Path.Combine(AppContext.BaseDirectory, "saves");
        var path = SlotPath(dir, slot);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    // ── Global store helpers ───────────────────────────────────────────────────

    /// <summary>Sets a key in the global persistent store.</summary>
    public void SetValue(string key, string value) => GlobalStore[key] = value;

    /// <summary>
    /// Gets a value from the global persistent store, returning
    /// <paramref name="defaultValue"/> when the key is absent.
    /// </summary>
    public string GetValue(string key, string defaultValue = "") =>
        GlobalStore.GetValueOrDefault(key, defaultValue);

    // ── DTO mappings ───────────────────────────────────────────────────────────

    private static EntitySaveDto EntityToDto(Entity e) =>
        new(e.Id, e.Name, e.Glyph, e.ForegroundColor, e.X, e.Y, e.BlocksMovement,
            new Dictionary<string, string>(e.Properties));

    private static Entity DtoToEntity(EntitySaveDto dto)
    {
        var e = new Entity
        {
            Name = dto.Name,
            Glyph = dto.Glyph,
            ForegroundColor = dto.ForegroundColor,
            X = dto.X,
            Y = dto.Y,
            BlocksMovement = dto.BlocksMovement,
        };
        // Restore the original Id through reflection (same pattern as serializer).
        typeof(Entity).GetProperty("Id")?.SetValue(e, dto.Id);
        foreach (var kv in dto.Properties)
            e.Properties[kv.Key] = kv.Value;
        return e;
    }

    private static string SlotPath(string dir, string slot) =>
        Path.Combine(dir, $"{SanitiseName(slot)}.rgsave");

    private static string SanitiseName(string s) =>
        new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    // ── DTO types ──────────────────────────────────────────────────────────────

    private sealed record SaveDto(
        string Slot,
        DateTime Timestamp,
        int GameHour,
        Dictionary<string, string> GlobalStore,
        List<LocationSaveDto> Locations,
        Guid? CurrentLocationId,
        List<EntitySaveDto> Entities);

    private sealed record LocationSaveDto(
        Guid Id, string Name, int WorldX, int WorldY,
        bool HasBeenVisited,
        Dictionary<string, string> PersistentData,
        List<EntitySaveDto> Entities);

    private sealed record EntitySaveDto(
        Guid Id, string Name, char Glyph, int ForegroundColor,
        int X, int Y, bool BlocksMovement,
        Dictionary<string, string> Properties);
}

/// <summary>Result of a successful <see cref="PersistenceManager.Load"/> call.</summary>
/// <param name="Entities">Entities restored from the save file (global scope).</param>
/// <param name="GameHour">In-game hour at the time of the save.</param>
public sealed record LoadResult(IReadOnlyList<Entity> Entities, int GameHour);
