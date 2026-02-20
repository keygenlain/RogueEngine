using System.Text;
using RogueEngine.Core.Models;

namespace RogueEngine.Core.Engine;

/// <summary>
/// Generates and writes roguelike morgue files.
///
/// <para>
/// <b>What is a morgue file?</b>
/// Originating with NetHack, a morgue file is a plain-text post-mortem
/// automatically written when a character dies (or a run ends).  It records
/// the character's name, stats, cause of death, creatures killed, locations
/// visited, and a condensed game log so that players can review – or share –
/// their run after the fact.
/// </para>
///
/// <para>
/// <b>Usage</b>:
/// <code>
/// var morgue = MorgueFileWriter.BuildFromRunState(
///     player: heroEntity,
///     cause:  "Killed by a cave troll",
///     turnsPlayed: 412,
///     visitedLocations: [ "Town", "Dark Cave", "Goblin Lair" ],
///     killLog: [ "rat", "goblin", "goblin", "cave troll" ],
///     notes: executionResult.Log);
///
/// var writer = new MorgueFileWriter();
/// writer.WriteToFile(morgue, "morgues/");
/// </code>
/// </para>
/// </summary>
public sealed class MorgueFileWriter
{
    // ── Version ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Version string embedded in every generated morgue file.
    /// </summary>
    public const string EngineVersion = "1.0.0";

    // ── Factory ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="MorgueFile"/> from live game-state objects.
    /// </summary>
    /// <param name="player">The player <see cref="Entity"/> at the time of death.</param>
    /// <param name="cause">Human-readable cause of death or run-end reason.</param>
    /// <param name="turnsPlayed">Number of game ticks elapsed.</param>
    /// <param name="visitedLocations">
    /// Ordered list of location names visited during the run.
    /// </param>
    /// <param name="killLog">
    /// Ordered list of entity names killed during the run.
    /// </param>
    /// <param name="notes">
    /// Condensed game-log lines (e.g. from
    /// <see cref="ExecutionResult.Log"/>).
    /// </param>
    /// <param name="score">Optional final score; defaults to 0.</param>
    /// <param name="maxDepth">Deepest level reached; defaults to 0.</param>
    /// <param name="runStarted">
    /// UTC time the run started; defaults to <see cref="DateTime.UtcNow"/>.
    /// </param>
    /// <returns>A fully populated <see cref="MorgueFile"/>.</returns>
    public static MorgueFile BuildFromRunState(
        Entity? player,
        string cause = "Unknown cause",
        int turnsPlayed = 0,
        IEnumerable<string>? visitedLocations = null,
        IEnumerable<string>? killLog = null,
        IEnumerable<string>? notes = null,
        int score = 0,
        int maxDepth = 0,
        DateTime? runStarted = null)
    {
        var started = runStarted ?? DateTime.UtcNow;
        var ended   = DateTime.UtcNow;

        var morgue = new MorgueFile
        {
            CharacterName    = player?.Name ?? "Unknown",
            Cause            = cause,
            Score            = score,
            TurnsPlayed      = turnsPlayed,
            MaxDepth         = maxDepth,
            RunStarted       = started,
            RunEnded         = ended,
            VisitedLocations = visitedLocations?.ToList() ?? [],
            KillLog          = killLog?.ToList() ?? [],
            Notes            = notes?.ToList() ?? [],
        };

        // Copy entity properties as stats.
        if (player is not null)
        {
            foreach (var kv in player.Properties)
                morgue.Stats[kv.Key] = kv.Value;

            // Always capture glyph and colour even if not in Properties.
            morgue.Stats.TryAdd("Glyph",  player.Glyph.ToString());
            morgue.Stats.TryAdd("Colour", $"#{player.ForegroundColor:X6}");
        }

        return morgue;
    }

    // ── Text serialisation ─────────────────────────────────────────────────────

    /// <summary>
    /// Formats a <see cref="MorgueFile"/> as a traditional plain-text morgue
    /// file and returns it as a string.
    /// </summary>
    /// <param name="morgue">The morgue data to format.</param>
    /// <returns>Multi-line plain-text content.</returns>
    public string WriteMorgue(MorgueFile morgue)
    {
        ArgumentNullException.ThrowIfNull(morgue);

        var sb = new StringBuilder();
        var sep = new string('=', 72);
        var thin = new string('-', 72);

        // ── Header ──────────────────────────────────────────────────────────────
        sb.AppendLine(sep);
        sb.AppendLine($"  RogueEngine Morgue File  (engine {morgue.EngineVersion})");
        sb.AppendLine(sep);
        sb.AppendLine();
        sb.AppendLine($"  {morgue.CharacterName}");
        sb.AppendLine($"  {morgue.Cause}");
        sb.AppendLine();

        // ── Run summary ─────────────────────────────────────────────────────────
        sb.AppendLine(thin);
        sb.AppendLine("  RUN SUMMARY");
        sb.AppendLine(thin);
        sb.AppendLine($"  Score        : {morgue.Score}");
        sb.AppendLine($"  Turns played : {morgue.TurnsPlayed}");
        sb.AppendLine($"  Max depth    : {morgue.MaxDepth}");
        sb.AppendLine($"  Started      : {morgue.RunStarted:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"  Ended        : {morgue.RunEnded:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"  Duration     : {morgue.Duration:hh\\:mm\\:ss}");
        sb.AppendLine();

        // ── Character stats ──────────────────────────────────────────────────────
        if (morgue.Stats.Count > 0)
        {
            sb.AppendLine(thin);
            sb.AppendLine("  CHARACTER STATS");
            sb.AppendLine(thin);
            foreach (var kv in morgue.Stats.OrderBy(k => k.Key))
                sb.AppendLine($"  {kv.Key,-16}: {kv.Value}");
            sb.AppendLine();
        }

        // ── Visited locations ────────────────────────────────────────────────────
        if (morgue.VisitedLocations.Count > 0)
        {
            sb.AppendLine(thin);
            sb.AppendLine("  VISITED LOCATIONS");
            sb.AppendLine(thin);
            for (int i = 0; i < morgue.VisitedLocations.Count; i++)
                sb.AppendLine($"  {i + 1,3}. {morgue.VisitedLocations[i]}");
            sb.AppendLine();
        }

        // ── Kill log ─────────────────────────────────────────────────────────────
        if (morgue.KillLog.Count > 0)
        {
            sb.AppendLine(thin);
            sb.AppendLine("  KILLS");
            sb.AppendLine(thin);

            // Group and count kills.
            var grouped = morgue.KillLog
                .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key);

            foreach (var g in grouped)
                sb.AppendLine($"  {g.Count(),4}x  {g.Key}");
            sb.AppendLine($"       Total: {morgue.KillLog.Count}");
            sb.AppendLine();
        }

        // ── Notes / game log ─────────────────────────────────────────────────────
        if (morgue.Notes.Count > 0)
        {
            sb.AppendLine(thin);
            sb.AppendLine("  GAME LOG");
            sb.AppendLine(thin);
            foreach (var note in morgue.Notes)
                sb.AppendLine($"  {note}");
            sb.AppendLine();
        }

        sb.AppendLine(sep);
        sb.AppendLine("  End of morgue file.");
        sb.AppendLine(sep);

        return sb.ToString();
    }

    // ── File output ────────────────────────────────────────────────────────────

    /// <summary>
    /// Formats the morgue file and writes it to
    /// <paramref name="directory"/> as a timestamped <c>*.txt</c> file.
    ///
    /// <para>
    /// The file name follows the pattern:
    /// <c>{CharacterName}-{yyyy-MM-dd_HH-mm-ss}.txt</c>
    /// </para>
    /// </summary>
    /// <param name="morgue">The morgue data to write.</param>
    /// <param name="directory">
    /// Directory where the file is created.  Created if it does not exist.
    /// Defaults to <c>morgues/</c> relative to the application base directory.
    /// </param>
    /// <returns>
    /// The full path of the written file on success, or
    /// <see langword="null"/> on I/O error.
    /// </returns>
    public string? WriteToFile(MorgueFile morgue, string? directory = null)
    {
        ArgumentNullException.ThrowIfNull(morgue);
        try
        {
            var dir = directory ?? Path.Combine(AppContext.BaseDirectory, "morgues");
            Directory.CreateDirectory(dir);

            var safeName = new string(
                morgue.CharacterName.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            var stamp = morgue.RunEnded.ToString("yyyy-MM-dd_HH-mm-ss");
            var path  = Path.Combine(dir, $"{safeName}-{stamp}.txt");

            File.WriteAllText(path, WriteMorgue(morgue), Encoding.UTF8);
            return path;
        }
        catch
        {
            return null;
        }
    }
}
