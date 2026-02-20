namespace RogueEngine.Core.Models;

/// <summary>
/// Represents a single cell in an ASCII display grid.
/// Each cell stores a character, a foreground colour, and a background colour.
/// Colours are encoded as 24-bit RGB integers (0xRRGGBB).
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
    /// Creates a deep copy of this cell.
    /// </summary>
    public AsciiCell Clone() => new()
    {
        Character = Character,
        ForegroundColor = ForegroundColor,
        BackgroundColor = BackgroundColor,
    };

    /// <inheritdoc/>
    public override string ToString() =>
        $"'{Character}' fg=#{ForegroundColor:X6} bg=#{BackgroundColor:X6}";
}
