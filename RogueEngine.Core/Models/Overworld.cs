namespace RogueEngine.Core.Models;

/// <summary>
/// The overworld: a named collection of <see cref="OverworldLocation"/> objects
/// connected by exits.  The engine tracks which location is currently active and
/// raises transition events when the player travels between locations.
/// </summary>
public sealed class Overworld
{
    /// <summary>Unique identifier for this overworld.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Display name of the game world (e.g. "The Shattered Realm").</summary>
    public string Name { get; set; } = "World";

    /// <summary>All locations that exist in this world.</summary>
    public List<OverworldLocation> Locations { get; } = [];

    /// <summary>
    /// The <see cref="OverworldLocation.Id"/> of the location the player is
    /// currently in.  <see langword="null"/> before the first
    /// <see cref="TravelTo"/> call.
    /// </summary>
    public Guid? CurrentLocationId { get; private set; }

    // ── Events ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the player leaves a location.
    /// Arguments are the departed location and the direction / exit name used.
    /// </summary>
    public event Action<OverworldLocation, string>? LocationLeft;

    /// <summary>
    /// Raised when the player arrives at a new location.
    /// </summary>
    public event Action<OverworldLocation>? LocationEntered;

    // ── Accessors ──────────────────────────────────────────────────────────────

    /// <summary>Returns the currently active location, or <see langword="null"/>.</summary>
    public OverworldLocation? CurrentLocation =>
        CurrentLocationId.HasValue ? FindLocation(CurrentLocationId.Value) : null;

    /// <summary>
    /// Finds the location with the given <paramref name="id"/>,
    /// or <see langword="null"/> if it does not exist.
    /// </summary>
    public OverworldLocation? FindLocation(Guid id) =>
        Locations.FirstOrDefault(l => l.Id == id);

    /// <summary>
    /// Finds the first location whose <see cref="OverworldLocation.Name"/> matches
    /// <paramref name="name"/> (case-insensitive), or <see langword="null"/>.
    /// </summary>
    public OverworldLocation? FindLocationByName(string name) =>
        Locations.FirstOrDefault(l =>
            string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

    // ── Mutation ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a new <paramref name="location"/> to the world.
    /// </summary>
    public void AddLocation(OverworldLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        Locations.Add(location);
    }

    /// <summary>
    /// Travels to the location identified by <paramref name="destinationId"/>,
    /// raising <see cref="LocationLeft"/> on the current location and
    /// <see cref="LocationEntered"/> on the destination.
    /// </summary>
    /// <param name="exitName">
    /// The exit / direction used for the transition (used in the departed event).
    /// Pass an empty string if not applicable.
    /// </param>
    /// <returns>
    /// The destination <see cref="OverworldLocation"/>, or
    /// <see langword="null"/> if no location with that id exists.
    /// </returns>
    public OverworldLocation? TravelTo(Guid destinationId, string exitName = "")
    {
        var dest = FindLocation(destinationId);
        if (dest is null) return null;

        if (CurrentLocation is { } prev)
            LocationLeft?.Invoke(prev, exitName);

        CurrentLocationId = destinationId;
        dest.HasBeenVisited = true;
        LocationEntered?.Invoke(dest);
        return dest;
    }

    /// <summary>
    /// Convenience overload: travels via a named exit from the current location.
    /// </summary>
    /// <returns>
    /// The destination, or <see langword="null"/> when the exit does not exist
    /// or no current location is set.
    /// </returns>
    public OverworldLocation? TravelViaExit(string exitName)
    {
        if (CurrentLocation is not { } cur) return null;
        if (!cur.Exits.TryGetValue(exitName, out var destId)) return null;
        return TravelTo(destId, exitName);
    }

    /// <summary>
    /// Sets the starting location without raising events (used during setup).
    /// </summary>
    public void SetStartLocation(Guid locationId) =>
        CurrentLocationId = locationId;

    /// <inheritdoc/>
    public override string ToString() =>
        $"Overworld '{Name}' ({Locations.Count} locations)";
}
