using RogueEngine.Core.Models;

namespace RogueEngine.Core.Engine;

/// <summary>
/// Manages the game's overworld: creation, location generation, and player travel.
/// The <see cref="OverworldManager"/> is owned by a <see cref="ScriptExecutor"/>
/// and is updated as overworld-related nodes execute.
/// </summary>
public sealed class OverworldManager
{
    private readonly Dictionary<string, int> _factionRelations = [];
    private readonly Dictionary<string, string> _entityFactions = [];
    private int _gameHour;

    /// <summary>
    /// The active overworld for this game session.
    /// Set when a <see cref="NodeType.CreateOverworld"/> node executes.
    /// </summary>
    public Overworld? ActiveOverworld { get; private set; }

    // ── Overworld lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="Overworld"/> with the given <paramref name="name"/>,
    /// assigns it as the <see cref="ActiveOverworld"/>, and returns it.
    /// </summary>
    public Overworld CreateOverworld(string name)
    {
        var world = new Overworld { Name = name };
        // Wire event callbacks so callers can receive them.
        ActiveOverworld = world;
        return world;
    }

    /// <summary>
    /// Creates a new <see cref="OverworldLocation"/>, adds it to
    /// <paramref name="world"/>, and returns it.
    /// </summary>
    public OverworldLocation AddLocation(
        Overworld world, string name, int worldX, int worldY,
        string description = "")
    {
        ArgumentNullException.ThrowIfNull(world);
        var loc = new OverworldLocation
        {
            Name = name,
            WorldX = worldX,
            WorldY = worldY,
            Description = description,
        };
        world.AddLocation(loc);
        return loc;
    }

    /// <summary>
    /// Connects <paramref name="from"/> to <paramref name="to"/> using
    /// <paramref name="exitName"/> and (optionally) a reverse exit.
    /// </summary>
    public void ConnectLocations(
        OverworldLocation from,
        OverworldLocation to,
        string exitName,
        string? reverseExitName = null)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        from.AddExit(exitName, to.Id);
        if (!string.IsNullOrWhiteSpace(reverseExitName))
            to.AddExit(reverseExitName, from.Id);
    }

    /// <summary>
    /// Generates or re-generates the <see cref="AsciiMap"/> for
    /// <paramref name="location"/> using the given algorithm.
    /// </summary>
    /// <param name="location">The location to generate a map for.</param>
    /// <param name="algorithm">
    /// One of "Cave", "BSP", or "Drunkard". Defaults to "Cave".
    /// </param>
    /// <param name="width">Map width in cells.</param>
    /// <param name="height">Map height in cells.</param>
    /// <param name="seed">Optional RNG seed for reproducibility.</param>
    /// <returns>The newly generated <see cref="AsciiMap"/>.</returns>
    public AsciiMap GenerateLocationMap(
        OverworldLocation location,
        string algorithm = "Cave",
        int width = 60, int height = 20,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        var map = new AsciiMap(Math.Max(1, width), Math.Max(1, height));

        switch (algorithm.ToUpperInvariant())
        {
            case "BSP":
                MapGenerator.GenerateRoomsBSP(map, seed: seed);
                break;
            case "DRUNKARD":
            case "DRUNKARDWALK":
                MapGenerator.GenerateDrunkardWalk(map, seed: seed);
                break;
            default:
                MapGenerator.GenerateCave(map, seed: seed);
                break;
        }

        location.Map = map;
        return map;
    }

    /// <summary>
    /// Renders all overworld locations as glyph dots on the provided
    /// <paramref name="screen"/> buffer (2-D char array [col, row]).
    /// </summary>
    /// <param name="world">The overworld to render.</param>
    /// <param name="screen">Target character grid.</param>
    /// <param name="fg">Foreground colour grid (0xRRGGBB).</param>
    /// <param name="bg">Background colour grid (0xRRGGBB).</param>
    /// <param name="screenWidth">Width of the screen buffer.</param>
    /// <param name="screenHeight">Height of the screen buffer.</param>
    public void RenderOverworld(
        Overworld world,
        AsciiMap screen,
        int screenWidth, int screenHeight)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(screen);

        // Clear map first.
        screen.Fill(new AsciiCell { Character = ' ' });

        // Draw border.
        for (var x = 0; x < screenWidth; x++)
        {
            if (screen.IsInBounds(x, 0)) screen[x, 0] = new AsciiCell { Character = '─', ForegroundColor = 0x555555 };
            if (screen.IsInBounds(x, screenHeight - 1)) screen[x, screenHeight - 1] = new AsciiCell { Character = '─', ForegroundColor = 0x555555 };
        }

        // Plot each location as a symbol.
        foreach (var loc in world.Locations)
        {
            var sx = Math.Clamp(loc.WorldX, 1, screenWidth - 2);
            var sy = Math.Clamp(loc.WorldY, 1, screenHeight - 2);
            var glyph = world.CurrentLocationId == loc.Id ? '@' : (loc.HasBeenVisited ? '*' : '?');
            var color = world.CurrentLocationId == loc.Id ? 0xFFFF00 : (loc.HasBeenVisited ? 0x00FF00 : 0x666666);
            screen[sx, sy] = new AsciiCell { Character = glyph, ForegroundColor = color };
        }
    }

    // ── Factions & Relationships ───────────────────────────────────────────────

    private static string FactionKey(string a, string b) =>
        string.Compare(a, b, StringComparison.Ordinal) <= 0
            ? $"{a}|{b}"
            : $"{b}|{a}";

    /// <summary>Sets the relationship value between two factions (-100 to 100).</summary>
    public void SetFactionRelation(string factionA, string factionB, int value)
    {
        var key = FactionKey(factionA, factionB);
        _factionRelations[key] = Math.Clamp(value, -100, 100);
    }

    /// <summary>Gets the relationship value between two factions (0 if never set).</summary>
    public int GetFactionRelation(string factionA, string factionB)
    {
        var key = FactionKey(factionA, factionB);
        return _factionRelations.GetValueOrDefault(key, 0);
    }

    /// <summary>Assigns an entity to a faction by entity ID string.</summary>
    public void AssignEntityFaction(string entityId, string faction) =>
        _entityFactions[entityId] = faction;

    /// <summary>Returns the faction of the entity, or empty string if unassigned.</summary>
    public string GetEntityFaction(string entityId) =>
        _entityFactions.GetValueOrDefault(entityId, string.Empty);

    // ── Day / Night cycle ──────────────────────────────────────────────────────

    /// <summary>Current in-game hour (0–23).</summary>
    public int GameHour => _gameHour % 24;

    /// <summary>
    /// Advances the in-game clock by <paramref name="amount"/> hours,
    /// wrapping at 24.
    /// </summary>
    /// <returns>The new hour after advancing.</returns>
    public int AdvanceTime(int amount = 1)
    {
        _gameHour = (_gameHour + amount) % 24;
        return _gameHour;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the current in-game hour is between
    /// <paramref name="nightStart"/> and <paramref name="nightEnd"/> (wraps midnight).
    /// </summary>
    public bool IsNight(int nightStart = 20, int nightEnd = 6)
    {
        var h = GameHour;
        if (nightStart > nightEnd)
            return h >= nightStart || h < nightEnd;
        return h >= nightStart && h < nightEnd;
    }
}
