using System.Text.Json;
using System.Text.Json.Serialization;
using RogueEngine.Core.Models;

namespace RogueEngine.Core.Engine;

/// <summary>
/// Serialises and deserialises the full runtime game state to/from JSON,
/// enabling save-and-restore across play sessions.
///
/// <para>
/// <b>Save format</b>: each slot is a UTF-8 JSON file (<c>*.rgsave</c>).
/// The file is human-readable and can be edited in any text editor.
/// The root object contains:
/// <list type="bullet">
///   <item><c>engineVersion</c> – for forward-compatibility checks.</item>
///   <item><c>slot</c> / <c>timestamp</c> – slot name and UTC save time.</item>
///   <item><c>gameHour</c> – current in-game clock hour (0–23).</item>
///   <item><c>globalStore</c> – arbitrary string→string persistent variables.</item>
///   <item><c>factionRelations</c> – faction-pair → relation value (-100..100).</item>
///   <item><c>entityFactions</c> – entity-id → faction-name mapping.</item>
///   <item><c>currentLocationId</c> – active overworld location.</item>
///   <item><c>locations</c> – per-location visited flag, persistent data, entities.</item>
///   <item><c>entities</c> – global (non-location) live entities.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>In-memory API</b>: <see cref="Serialize"/> and <see cref="Deserialize"/>
/// work entirely on strings/streams so unit tests never touch the file system.
/// </para>
/// </summary>
public sealed class PersistenceManager
{
    // ── Save-format version ────────────────────────────────────────────────────

    /// <summary>
    /// Semantic version embedded in every save file.
    /// Increment the major component when making breaking schema changes.
    /// </summary>
    public const string EngineVersion = "1.0.0";

    // ── JSON options ───────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // ── Public state ───────────────────────────────────────────────────────────

    /// <summary>
    /// Global key-value persistent store (survives save / load cycles).
    /// Keys and values are arbitrary strings; games can store quest flags,
    /// scores, unlocks, etc. here.
    /// </summary>
    public Dictionary<string, string> GlobalStore { get; private set; } = [];

    // ── In-memory API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Serialises the current game state to a JSON string without touching
    /// the file system. Useful for testing and for in-memory snapshots.
    /// </summary>
    /// <param name="slot">Logical slot name embedded in the JSON.</param>
    /// <param name="overworldMgr">
    /// Optional <see cref="OverworldManager"/>; when supplied its active
    /// overworld, faction relations and entity-faction assignments are captured.
    /// </param>
    /// <param name="entities">Live global entities (not bound to a location).</param>
    /// <param name="gameHour">Current in-game clock hour.</param>
    /// <returns>A pretty-printed UTF-8 JSON string.</returns>
    public string Serialize(
        string slot,
        OverworldManager? overworldMgr = null,
        IEnumerable<Entity>? entities = null,
        int gameHour = 0)
    {
        var dto = BuildDto(slot, overworldMgr, entities, gameHour);
        return JsonSerializer.Serialize(dto, _writeOptions);
    }

    /// <summary>
    /// Deserialises a JSON string previously produced by <see cref="Serialize"/>
    /// and restores state into the supplied managers, without touching disk.
    /// </summary>
    /// <param name="json">The JSON string to deserialise.</param>
    /// <param name="overworldMgr">Optional manager to restore overworld/faction state into.</param>
    /// <returns>
    /// A <see cref="LoadResult"/> on success, or <see langword="null"/> if the
    /// JSON is null / empty or fails to parse.
    /// </returns>
    public LoadResult? Deserialize(string json, OverworldManager? overworldMgr = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var dto = JsonSerializer.Deserialize<SaveDto>(json, _readOptions);
            return dto is null ? null : ApplyDto(dto, overworldMgr);
        }
        catch
        {
            return null;
        }
    }

    // ── File-based API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the current game state to a <c>*.rgsave</c> JSON file in
    /// <paramref name="saveDirectory"/> (created if absent).
    /// </summary>
    /// <param name="slot">
    /// Slot identifier, used as the file-name stem.
    /// Illegal file-name characters are replaced with underscores.
    /// </param>
    /// <param name="overworldMgr">
    /// Optional manager; when supplied its overworld, faction relations and
    /// entity-faction assignments are included in the save.
    /// </param>
    /// <param name="entities">Live global entities to persist.</param>
    /// <param name="gameHour">Current in-game clock hour (0–23).</param>
    /// <param name="saveDirectory">
    /// Directory for save files.
    /// Defaults to <c>saves/</c> relative to the application base directory.
    /// </param>
    /// <returns><see langword="true"/> on success, <see langword="false"/> on I/O error.</returns>
    public bool Save(
        string slot,
        OverworldManager? overworldMgr = null,
        IEnumerable<Entity>? entities = null,
        int gameHour = 0,
        string? saveDirectory = null)
    {
        try
        {
            var dir = ResolveDir(saveDirectory);
            Directory.CreateDirectory(dir);
            File.WriteAllText(SlotPath(dir, slot), Serialize(slot, overworldMgr, entities, gameHour));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Loads a save slot from disk and restores state.
    /// </summary>
    /// <param name="slot">Slot identifier.</param>
    /// <param name="overworldMgr">Optional manager to restore overworld/faction data into.</param>
    /// <param name="saveDirectory">Directory containing save files.</param>
    /// <returns>
    /// A <see cref="LoadResult"/> on success, or <see langword="null"/> if the
    /// slot does not exist or the file cannot be parsed.
    /// </returns>
    public LoadResult? Load(
        string slot,
        OverworldManager? overworldMgr = null,
        string? saveDirectory = null)
    {
        var dir  = ResolveDir(saveDirectory);
        var path = SlotPath(dir, slot);
        return File.Exists(path)
            ? Deserialize(File.ReadAllText(path), overworldMgr)
            : null;
    }

    // ── Slot management ────────────────────────────────────────────────────────

    /// <summary>Returns <see langword="true"/> if the named slot file exists on disk.</summary>
    public bool SlotExists(string slot, string? saveDirectory = null) =>
        File.Exists(SlotPath(ResolveDir(saveDirectory), slot));

    /// <summary>
    /// Deletes the named slot file.
    /// </summary>
    /// <returns><see langword="true"/> if the file existed and was deleted.</returns>
    public bool DeleteSlot(string slot, string? saveDirectory = null)
    {
        var path = SlotPath(ResolveDir(saveDirectory), slot);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    /// <summary>
    /// Returns the slot names of all save files found in
    /// <paramref name="saveDirectory"/> (without the <c>.rgsave</c> extension).
    /// Returns an empty array if the directory does not exist.
    /// </summary>
    public IReadOnlyList<string> ListSlots(string? saveDirectory = null)
    {
        var dir = ResolveDir(saveDirectory);
        if (!Directory.Exists(dir)) return [];
        return Directory
            .EnumerateFiles(dir, "*.rgsave")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(s => s)
            .ToList();
    }

    // ── Global store helpers ───────────────────────────────────────────────────

    /// <summary>Writes a key-value pair into the global persistent store.</summary>
    public void SetValue(string key, string value) => GlobalStore[key] = value;

    /// <summary>
    /// Reads a value from the global persistent store.
    /// Returns <paramref name="defaultValue"/> when the key is absent.
    /// </summary>
    public string GetValue(string key, string defaultValue = "") =>
        GlobalStore.GetValueOrDefault(key, defaultValue);

    // ── DTO construction ───────────────────────────────────────────────────────

    private SaveDto BuildDto(
        string slot,
        OverworldManager? mgr,
        IEnumerable<Entity>? entities,
        int gameHour)
    {
        var overworld = mgr?.ActiveOverworld;
        return new SaveDto
        {
            EngineVersion     = EngineVersion,
            Slot              = slot,
            Timestamp         = DateTime.UtcNow,
            GameHour          = gameHour,
            GlobalStore       = new Dictionary<string, string>(GlobalStore),
            FactionRelations  = mgr is null ? [] : CopyDict(GetFactionRelations(mgr)),
            EntityFactions    = mgr is null ? [] : CopyDict(GetEntityFactions(mgr)),
            CurrentLocationId = overworld?.CurrentLocationId,
            Locations         = overworld?.Locations
                .Select(LocationToDto).ToList() ?? [],
            Entities          = entities?.Select(EntityToDto).ToList() ?? [],
        };
    }

    // ── DTO application ────────────────────────────────────────────────────────

    private LoadResult ApplyDto(SaveDto dto, OverworldManager? mgr)
    {
        // 1. Global store.
        GlobalStore = new Dictionary<string, string>(dto.GlobalStore ?? []);

        // 2. Faction data → OverworldManager.
        if (mgr is not null)
        {
            foreach (var kv in dto.FactionRelations ?? [])
            {
                var parts = kv.Key.Split('|');
                if (parts.Length == 2 && int.TryParse(kv.Value, out var rel))
                    mgr.SetFactionRelation(parts[0], parts[1], rel);
            }
            foreach (var kv in dto.EntityFactions ?? [])
                mgr.AssignEntityFaction(kv.Key, kv.Value);
        }

        // 3. Overworld location states.
        var overworld = mgr?.ActiveOverworld;
        if (overworld is not null)
        {
            foreach (var ldto in dto.Locations ?? [])
            {
                var loc = overworld.FindLocation(ldto.Id);
                if (loc is null) continue;
                loc.HasBeenVisited = ldto.HasBeenVisited;
                loc.PersistentData.Clear();
                foreach (var kv in ldto.PersistentData ?? [])
                    loc.PersistentData[kv.Key] = kv.Value;
                loc.Entities.Clear();
                loc.Entities.AddRange((ldto.Entities ?? []).Select(DtoToEntity));
            }
            if (dto.CurrentLocationId.HasValue)
                overworld.SetStartLocation(dto.CurrentLocationId.Value);
        }

        // 4. Global entities.
        var entities = (dto.Entities ?? []).Select(DtoToEntity).ToList();
        return new LoadResult(entities, dto.GameHour);
    }

    // ── Reflection helpers for OverworldManager private fields ────────────────
    // OverworldManager keeps its dictionaries internal; we read them via the
    // public API where possible, using the accessor methods already exposed.

    private static Dictionary<string, string> GetFactionRelations(OverworldManager mgr)
    {
        // Access via the public GetFactionRelation probe is not practical for bulk export,
        // so we expose a snapshot method on OverworldManager below.
        return mgr.SnapshotFactionRelations();
    }

    private static Dictionary<string, string> GetEntityFactions(OverworldManager mgr) =>
        mgr.SnapshotEntityFactions();

    // ── DTO/entity mappings ────────────────────────────────────────────────────

    private static LocationSaveDto LocationToDto(OverworldLocation l) => new()
    {
        Id              = l.Id,
        Name            = l.Name,
        WorldX          = l.WorldX,
        WorldY          = l.WorldY,
        HasBeenVisited  = l.HasBeenVisited,
        PersistentData  = new Dictionary<string, string>(l.PersistentData),
        Entities        = l.Entities.Select(EntityToDto).ToList(),
    };

    private static EntitySaveDto EntityToDto(Entity e) => new()
    {
        Id             = e.Id,
        Name           = e.Name,
        Glyph          = e.Glyph,
        ForegroundColor = e.ForegroundColor,
        X              = e.X,
        Y              = e.Y,
        BlocksMovement = e.BlocksMovement,
        Properties     = new Dictionary<string, string>(e.Properties),
    };

    private static Entity DtoToEntity(EntitySaveDto dto)
    {
        var e = new Entity
        {
            Name           = dto.Name,
            Glyph          = dto.Glyph,
            ForegroundColor = dto.ForegroundColor,
            X              = dto.X,
            Y              = dto.Y,
            BlocksMovement = dto.BlocksMovement,
        };
        typeof(Entity).GetProperty(nameof(Entity.Id))?.SetValue(e, dto.Id);
        foreach (var kv in dto.Properties ?? [])
            e.Properties[kv.Key] = kv.Value;
        return e;
    }

    private static Dictionary<string, string> CopyDict(Dictionary<string, string> src) =>
        new(src);

    // ── Path helpers ───────────────────────────────────────────────────────────

    private static string ResolveDir(string? dir) =>
        dir ?? Path.Combine(AppContext.BaseDirectory, "saves");

    internal static string SlotPath(string dir, string slot) =>
        Path.Combine(dir, $"{SanitiseName(slot)}.rgsave");

    private static string SanitiseName(string s) =>
        new string(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray());

    // ── Save-file DTO (public fields for JSON round-trip) ──────────────────────

    /// <summary>
    /// Root object of a <c>.rgsave</c> JSON file.
    /// All fields use camelCase names in the serialised output.
    /// </summary>
    public sealed class SaveDto
    {
        /// <summary>RogueEngine version that wrote this file.</summary>
        public string EngineVersion { get; set; } = PersistenceManager.EngineVersion;

        /// <summary>Logical slot name.</summary>
        public string Slot { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the save.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>In-game clock hour (0–23).</summary>
        public int GameHour { get; set; }

        /// <summary>Global persistent key-value store.</summary>
        public Dictionary<string, string> GlobalStore { get; set; } = [];

        /// <summary>
        /// Faction relationship pairs.
        /// Keys use the canonical form <c>"FactionA|FactionB"</c> (alphabetical);
        /// values are the relation integer as a string.
        /// </summary>
        public Dictionary<string, string> FactionRelations { get; set; } = [];

        /// <summary>Maps entity ID strings to faction name strings.</summary>
        public Dictionary<string, string> EntityFactions { get; set; } = [];

        /// <summary>Current overworld location, if any.</summary>
        public Guid? CurrentLocationId { get; set; }

        /// <summary>Per-location persistent state.</summary>
        public List<LocationSaveDto> Locations { get; set; } = [];

        /// <summary>Global (non-location) live entities.</summary>
        public List<EntitySaveDto> Entities { get; set; } = [];
    }

    /// <summary>Per-location save data embedded inside <see cref="SaveDto"/>.</summary>
    public sealed class LocationSaveDto
    {
        /// <summary>Location ID (must match an existing <see cref="OverworldLocation"/>).</summary>
        public Guid Id { get; set; }
        /// <summary>Location name at save time (informational).</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Overworld column.</summary>
        public int WorldX { get; set; }
        /// <summary>Overworld row.</summary>
        public int WorldY { get; set; }
        /// <summary>Whether the player has ever visited this location.</summary>
        public bool HasBeenVisited { get; set; }
        /// <summary>Location-scoped persistent key-value data.</summary>
        public Dictionary<string, string> PersistentData { get; set; } = [];
        /// <summary>Entities that reside in this location.</summary>
        public List<EntitySaveDto> Entities { get; set; } = [];
    }

    /// <summary>Entity save data embedded inside location and global lists.</summary>
    public sealed class EntitySaveDto
    {
        /// <summary>Entity ID (preserved across save/load for consistent references).</summary>
        public Guid Id { get; set; }
        /// <summary>Display name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>ASCII glyph character.</summary>
        public char Glyph { get; set; } = '?';
        /// <summary>Foreground colour (0xRRGGBB).</summary>
        public int ForegroundColor { get; set; } = 0xFFFFFF;
        /// <summary>Column position.</summary>
        public int X { get; set; }
        /// <summary>Row position.</summary>
        public int Y { get; set; }
        /// <summary>Whether this entity blocks movement.</summary>
        public bool BlocksMovement { get; set; }
        /// <summary>Custom game-defined properties (health, damage, etc.).</summary>
        public Dictionary<string, string> Properties { get; set; } = [];
    }
}

/// <summary>
/// Result of a successful <see cref="PersistenceManager.Load"/> or
/// <see cref="PersistenceManager.Deserialize"/> call.
/// </summary>
/// <param name="Entities">Global entities restored from the save.</param>
/// <param name="GameHour">In-game clock hour at the time of the save.</param>
public sealed record LoadResult(IReadOnlyList<Entity> Entities, int GameHour);
