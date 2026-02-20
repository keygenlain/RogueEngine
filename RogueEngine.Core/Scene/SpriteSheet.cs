namespace RogueEngine.Core.Scene;

/// <summary>
/// A sprite-sheet (atlas) that slices a single image file into a grid of
/// named tiles.  Individual tiles are referenced by name and exposed as
/// <see cref="SpriteDefinition"/> objects through <see cref="SpriteLibrary"/>.
/// </summary>
public sealed class SpriteSheet
{
    // ── Identity ───────────────────────────────────────────────────────────────

    /// <summary>Unique name used to reference this sheet in a <see cref="SpriteLibrary"/>.</summary>
    public string Name { get; set; } = "unnamed";

    /// <summary>
    /// Path to the source image file (PNG / BMP / JPEG).
    /// Absolute, or relative to the project file.
    /// </summary>
    public string ImagePath { get; set; } = string.Empty;

    // ── Grid parameters ────────────────────────────────────────────────────────

    /// <summary>Width of each tile in pixels.</summary>
    public int TileWidth { get; set; } = 16;

    /// <summary>Height of each tile in pixels.</summary>
    public int TileHeight { get; set; } = 16;

    /// <summary>Horizontal gap between tiles in pixels (default 0).</summary>
    public int SpacingX { get; set; }

    /// <summary>Vertical gap between tiles in pixels (default 0).</summary>
    public int SpacingY { get; set; }

    /// <summary>Left margin of the atlas in pixels (default 0).</summary>
    public int MarginX { get; set; }

    /// <summary>Top margin of the atlas in pixels (default 0).</summary>
    public int MarginY { get; set; }

    // ── Named tiles ────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps a tile name to its (column, row) zero-based grid coordinates
    /// within the atlas.
    /// </summary>
    public Dictionary<string, (int Col, int Row)> Tiles { get; } = [];

    // ── API ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a named tile at grid coordinates (<paramref name="col"/>,
    /// <paramref name="row"/>).
    /// </summary>
    public void RegisterTile(string name, int col, int row) =>
        Tiles[name] = (col, row);

    /// <summary>
    /// Registers tiles in sequence starting from the given grid origin,
    /// naming them <c>{prefix}0</c>, <c>{prefix}1</c>, … across columns first.
    /// </summary>
    /// <param name="prefix">Name prefix (e.g. "wall_").</param>
    /// <param name="count">Total number of tiles to register.</param>
    /// <param name="startCol">Column of the first tile (default 0).</param>
    /// <param name="startRow">Row of the first tile (default 0).</param>
    /// <param name="columns">Sheet width in tiles (used for wrapping; default 16).</param>
    public void RegisterRange(
        string prefix, int count,
        int startCol = 0, int startRow = 0,
        int columns = 16)
    {
        for (var i = 0; i < count; i++)
        {
            var linearIndex = startRow * columns + startCol + i;
            Tiles[$"{prefix}{i}"] = (linearIndex % columns, linearIndex / columns);
        }
    }

    /// <summary>
    /// Calculates the pixel region (X, Y, Width, Height) of the named tile
    /// within the source image.
    /// </summary>
    /// <param name="tileName">Name of the tile.</param>
    /// <returns>Pixel rectangle, or <see langword="null"/> if the name is unknown.</returns>
    public (int X, int Y, int Width, int Height)? GetTileRegion(string tileName)
    {
        if (!Tiles.TryGetValue(tileName, out var pos)) return null;
        var x = MarginX + pos.Col * (TileWidth  + SpacingX);
        var y = MarginY + pos.Row * (TileHeight + SpacingY);
        return (x, y, TileWidth, TileHeight);
    }

    /// <summary>
    /// Creates a <see cref="SpriteDefinition"/> for a named tile, combining
    /// the sheet's image information with the supplied ASCII fallback.
    /// </summary>
    /// <param name="tileName">Name of the tile within this sheet.</param>
    /// <param name="glyph">ASCII fallback character.</param>
    /// <param name="fg">Foreground colour (0xRRGGBB).</param>
    /// <param name="bg">Background colour (0xRRGGBB).</param>
    /// <returns>A fully populated <see cref="SpriteDefinition"/>.</returns>
    public SpriteDefinition CreateSprite(
        string tileName,
        char glyph = '?',
        int fg = 0xFFFFFF,
        int bg = 0x000000)
    {
        var region = GetTileRegion(tileName);
        return new SpriteDefinition
        {
            Name            = $"{Name}/{tileName}",
            Glyph           = glyph,
            ForegroundColor = fg,
            BackgroundColor = bg,
            ImagePath       = ImagePath,
            TileX           = region?.X    ?? 0,
            TileY           = region?.Y    ?? 0,
            TileWidth       = region?.Width  ?? TileWidth,
            TileHeight      = region?.Height ?? TileHeight,
            SheetName       = Name,
            RenderMode      = SpriteRenderMode.Auto,
        };
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"[SpriteSheet '{Name}' {Tiles.Count} tiles @ {TileWidth}×{TileHeight}px]";
}
