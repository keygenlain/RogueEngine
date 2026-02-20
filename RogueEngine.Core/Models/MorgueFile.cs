namespace RogueEngine.Core.Models;

/// <summary>
/// A post-mortem record of a completed (or ended) roguelike run.
///
/// <para>
/// Morgue files are a classic roguelike convention originating with NetHack.
/// When a character dies – or a run ends – a detailed plain-text file is
/// generated and archived.  The file records everything about the character's
/// journey: their name and stats, how they died, every creature they killed,
/// every location they visited, and a condensed game log.
/// </para>
///
/// <para>
/// Use <see cref="Engine.MorgueFileWriter"/> to serialise a
/// <see cref="MorgueFile"/> to the traditional plain-text format or to write
/// it to disk.  Use <see cref="Engine.MorgueFileWriter.BuildFromRunState"/>
/// to create one from live game state.
/// </para>
/// </summary>
public sealed class MorgueFile
{
    // ── Identity ───────────────────────────────────────────────────────────────

    /// <summary>The character's display name (usually the player entity name).</summary>
    public string CharacterName { get; set; } = "Unknown";

    /// <summary>
    /// Human-readable cause of death or run-end reason.
    /// Examples: "Killed by a goblin", "Fell into a pit", "Retired victorious".
    /// </summary>
    public string Cause { get; set; } = "Unknown cause";

    // ── Scores & Progress ──────────────────────────────────────────────────────

    /// <summary>Final score for the run.</summary>
    public int Score { get; set; }

    /// <summary>Number of game turns / ticks played.</summary>
    public int TurnsPlayed { get; set; }

    /// <summary>
    /// Deepest dungeon level or overworld depth reached (0 = surface).
    /// </summary>
    public int MaxDepth { get; set; }

    // ── Timing ─────────────────────────────────────────────────────────────────

    /// <summary>UTC wall-clock time the run started.</summary>
    public DateTime RunStarted { get; set; } = DateTime.UtcNow;

    /// <summary>UTC wall-clock time the run ended (character died or quit).</summary>
    public DateTime RunEnded { get; set; } = DateTime.UtcNow;

    /// <summary>Elapsed real time for the run.</summary>
    public TimeSpan Duration => RunEnded - RunStarted;

    // ── Character stats ────────────────────────────────────────────────────────

    /// <summary>
    /// Character statistics at the time of death.
    /// Mirrors <see cref="Entity.Properties"/> (HP, MaxHP, STR, DEX, …).
    /// </summary>
    public Dictionary<string, string> Stats { get; set; } = [];

    // ── History ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ordered list of creature names killed during the run.
    /// Duplicates are allowed; callers may group and count as needed.
    /// </summary>
    public List<string> KillLog { get; set; } = [];

    /// <summary>
    /// Names of overworld locations (or dungeon level descriptions) visited
    /// during the run, in the order first visited.
    /// </summary>
    public List<string> VisitedLocations { get; set; } = [];

    /// <summary>
    /// Condensed game-log messages (e.g. combat results, item pickups,
    /// significant events).  Populated from the script executor log.
    /// </summary>
    public List<string> Notes { get; set; } = [];

    // ── Engine metadata ────────────────────────────────────────────────────────

    /// <summary>RogueEngine version that produced this morgue file.</summary>
    public string EngineVersion { get; set; } = Engine.MorgueFileWriter.EngineVersion;

    /// <inheritdoc/>
    public override string ToString() =>
        $"Morgue: {CharacterName} – {Cause} (score {Score}, turns {TurnsPlayed})";
}
