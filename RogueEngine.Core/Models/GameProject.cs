namespace RogueEngine.Core.Models;

/// <summary>
/// A complete RogueEngine game project.
/// Holds all <see cref="ScriptGraph"/> objects that make up the game,
/// plus project-level metadata used during export.
/// The project can be serialised to / deserialised from JSON using
/// <see cref="Engine.ScriptGraphSerializer"/>.
/// </summary>
public sealed class GameProject
{
    /// <summary>Project display name (becomes the executable / page title).</summary>
    public string Name { get; set; } = "My Roguelike";

    /// <summary>Author / studio name.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Semantic version string (e.g. "1.0.0").</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Brief description shown in the HTML5 export metadata.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Width of the ASCII display in characters.
    /// Each character cell is rendered at a fixed pixel size in the HTML5 export.
    /// </summary>
    public int DisplayWidth { get; set; } = 80;

    /// <summary>Height of the ASCII display in characters.</summary>
    public int DisplayHeight { get; set; } = 25;

    /// <summary>
    /// Name of the monospace font to use in the HTML5 export.
    /// Must be available on the target browser (e.g. "Courier New", "Consolas").
    /// </summary>
    public string FontFamily { get; set; } = "Courier New";

    /// <summary>
    /// Optional custom font source used by HTML5 export.
    /// Supports local file paths (embedded as base64) and http/https/data URLs.
    /// When set, the exported page will register an <c>@font-face</c> and prefer it.
    /// </summary>
    public string? CustomFontPath { get; set; }

    /// <summary>Font size (in pixels) for each character in the HTML5 export.</summary>
    public int FontSizePx { get; set; } = 16;

    /// <summary>All script graphs that belong to this project.</summary>
    public List<ScriptGraph> Graphs { get; set; } = [];

    /// <summary>
    /// The <see cref="ScriptGraph.Id"/> of the graph to run on game start.
    /// </summary>
    public Guid? StartGraphId { get; set; }

    /// <summary>
    /// Returns the start graph, or <see langword="null"/> if
    /// <see cref="StartGraphId"/> has not been set.
    /// </summary>
    public ScriptGraph? GetStartGraph() =>
        StartGraphId.HasValue
            ? Graphs.FirstOrDefault(g => g.Id == StartGraphId.Value)
            : Graphs.FirstOrDefault();
}
