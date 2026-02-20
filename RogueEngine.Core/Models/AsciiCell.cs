namespace RogueEngine.Core.Models;

/// <summary>
/// Represents a single cell in an ASCII display grid.
/// Each cell stores a character, foreground/background colours, and an optional
/// sprite name for graphical rendering.  Colours are 24-bit RGB (0xRRGGBB).
/// </summary>
public sealed class AsciiCell
{
    /// <summary>The character displayed in this cell.</summary>
    public char Character { get; set; } = ' ';

    /// <summary>
    /// Foreground (glyph) colour encoded as 0xRRGGBB.
    /// Default is white (0xFFFFFF).
    /// </summary>
    public int ForegroundColor { get; set; } = 0xFFFFFF;

    /// <summary>
    /// Background colour encoded as 0xRRGGBB.
    /// Default is black (0x000000).
    /// </summary>
    public int BackgroundColor { get; set; } = 0x000000;

    /// <summary>
    /// Optional name of a <see cref="Scene.SpriteDefinition"/> registered in the
    /// project's <see cref="Scene.SpriteLibrary"/>.
    /// When non-null and the WPF renderer is in <em>graphic</em> mode, the
    /// renderer draws the sprite's tile image instead of (or beneath) the
    /// ASCII glyph.  In pure ASCII mode this field is ignored.
    /// </summary>
    public string? SpriteName { get; set; }

    /// <summary>Creates a deep copy of this cell.</summary>
    public AsciiCell Clone() => new()
    {
        Character       = Character,
        ForegroundColor = ForegroundColor,
        BackgroundColor = BackgroundColor,
        SpriteName      = SpriteName,
    };

    /// <inheritdoc/>
    public override string ToString() =>
        $"'{Character}' fg=#{ForegroundColor:X6} bg=#{BackgroundColor:X6}" +
        (SpriteName is null ? "" : $" sprite='{SpriteName}'");
}
