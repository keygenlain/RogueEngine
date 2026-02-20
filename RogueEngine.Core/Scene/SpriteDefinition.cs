namespace RogueEngine.Core.Scene;

/// <summary>
/// How a <see cref="SpriteDefinition"/> is rendered when the engine draws a cell.
/// </summary>
public enum SpriteRenderMode
{
    /// <summary>
    /// Always render the ASCII <see cref="SpriteDefinition.Glyph"/>.
    /// The graphical image is ignored even if one is set.
    /// </summary>
    AsciiOnly,

    /// <summary>
    /// Prefer the graphical image when one is available;
    /// fall back to the ASCII glyph when no image is set.
    /// This is the default mode.
    /// </summary>
    Auto,

    /// <summary>
    /// Always render the graphical image.
    /// If no image is configured the glyph is still used as a placeholder
    /// so the game stays functional.
    /// </summary>
    GraphicPreferred,
}

/// <summary>
/// Describes how a single sprite is rendered on the ASCII grid.
///
/// <para>
/// Every sprite has an ASCII representation (a <see cref="Glyph"/>,
/// foreground/background colours) that is always available.
/// Optionally a graphical tile can be layered on top of the cell when the
/// WPF renderer is in graphic mode.  The two representations share the same
/// grid position so the game layout is identical in both modes.
/// </para>
/// </summary>
public sealed class SpriteDefinition
{
    // ── Identity ───────────────────────────────────────────────────────────────

    /// <summary>Unique name used to look up this sprite in a <see cref="SpriteLibrary"/>.</summary>
    public string Name { get; set; } = "unnamed";

    // ── ASCII representation (always present) ──────────────────────────────────

    /// <summary>ASCII glyph character rendered in the grid cell.</summary>
    public char Glyph { get; set; } = '?';

    /// <summary>Foreground colour in 0xRRGGBB format.</summary>
    public int ForegroundColor { get; set; } = 0xFFFFFF;

    /// <summary>Background colour in 0xRRGGBB format.</summary>
    public int BackgroundColor { get; set; } = 0x000000;

    // ── Graphical representation (optional) ────────────────────────────────────

    /// <summary>
    /// Path to the image file used for graphic rendering.
    /// Absolute, or relative to the project file.
    /// <see langword="null"/> means no graphical tile is defined.
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// Pixel X offset of this tile within <see cref="ImagePath"/>
    /// (used for sprite-sheet atlas).  Ignored when <see cref="ImagePath"/>
    /// is <see langword="null"/>.
    /// </summary>
    public int TileX { get; set; }

    /// <summary>Pixel Y offset within the source image.</summary>
    public int TileY { get; set; }

    /// <summary>
    /// Pixel width of the tile region.  0 means "use the full image width".
    /// </summary>
    public int TileWidth { get; set; }

    /// <summary>
    /// Pixel height of the tile region.  0 means "use the full image height".
    /// </summary>
    public int TileHeight { get; set; }

    /// <summary>
    /// Name of the <see cref="SpriteSheet"/> this sprite was extracted from,
    /// or <see langword="null"/> if it is a standalone image.
    /// </summary>
    public string? SheetName { get; set; }

    // ── Render mode ────────────────────────────────────────────────────────────

    /// <summary>How the sprite should be rendered (ASCII, graphic, or auto).</summary>
    public SpriteRenderMode RenderMode { get; set; } = SpriteRenderMode.Auto;

    // ── Animation ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Optional sequence of <see cref="SpriteDefinition"/> names that form
    /// a looping animation.  When set, the <see cref="SpriteNode"/> cycles
    /// through the listed frames at <see cref="FramesPerSecond"/>.
    /// </summary>
    public List<string> AnimationFrames { get; set; } = [];

    /// <summary>Animation playback speed in frames per second.</summary>
    public float FramesPerSecond { get; set; } = 4f;

    /// <summary>
    /// <see langword="true"/> if this sprite has a graphical image configured.
    /// </summary>
    public bool HasGraphic => !string.IsNullOrWhiteSpace(ImagePath);

    /// <summary>
    /// Returns the effective <see cref="SpriteRenderMode"/> resolved for the
    /// current runtime: if <see cref="RenderMode"/> is
    /// <see cref="SpriteRenderMode.Auto"/> and no graphic is available, falls
    /// back to <see cref="SpriteRenderMode.AsciiOnly"/>.
    /// </summary>
    public SpriteRenderMode EffectiveMode =>
        RenderMode == SpriteRenderMode.Auto && !HasGraphic
            ? SpriteRenderMode.AsciiOnly
            : RenderMode;

    /// <inheritdoc/>
    public override string ToString() =>
        $"[Sprite '{Name}' '{Glyph}' {(HasGraphic ? "+" : "-")}gfx]";
}
