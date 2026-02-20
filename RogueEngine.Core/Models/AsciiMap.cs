namespace RogueEngine.Core.Models;

/// <summary>
/// A two-dimensional grid of <see cref="AsciiCell"/> objects that represents
/// the game world (or any region of it).
/// Coordinates use (column, row) ordering, i.e. X = column, Y = row.
/// </summary>
public sealed class AsciiMap
{
    private readonly AsciiCell[,] _cells;

    /// <summary>Number of columns in the map.</summary>
    public int Width { get; }

    /// <summary>Number of rows in the map.</summary>
    public int Height { get; }

    /// <summary>
    /// Initialises a new map filled with space characters on a black background.
    /// </summary>
    /// <param name="width">Number of columns.</param>
    /// <param name="height">Number of rows.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> or <paramref name="height"/> is less than 1.
    /// </exception>
    public AsciiMap(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        Width = width;
        Height = height;
        _cells = new AsciiCell[width, height];

        for (var x = 0; x < width; x++)
        for (var y = 0; y < height; y++)
            _cells[x, y] = new AsciiCell();
    }

    /// <summary>
    /// Gets or sets the cell at position (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when coordinates are outside the map bounds.
    /// </exception>
    public AsciiCell this[int x, int y]
    {
        get
        {
            ValidateBounds(x, y);
            return _cells[x, y];
        }
        set
        {
            ValidateBounds(x, y);
            _cells[x, y] = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if (<paramref name="x"/>, <paramref name="y"/>)
    /// is within the map boundaries.
    /// </summary>
    public bool IsInBounds(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// Fills every cell in the map with <paramref name="fill"/>.
    /// </summary>
    public void Fill(AsciiCell fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        for (var x = 0; x < Width; x++)
        for (var y = 0; y < Height; y++)
            _cells[x, y] = fill.Clone();
    }

    /// <summary>
    /// Fills a rectangular region with <paramref name="fill"/>.
    /// Coordinates are clamped to map boundaries.
    /// </summary>
    public void FillRegion(int x, int y, int regionWidth, int regionHeight, AsciiCell fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        var x1 = Math.Max(0, x);
        var y1 = Math.Max(0, y);
        var x2 = Math.Min(Width - 1, x + regionWidth - 1);
        var y2 = Math.Min(Height - 1, y + regionHeight - 1);

        for (var cx = x1; cx <= x2; cx++)
        for (var cy = y1; cy <= y2; cy++)
            _cells[cx, cy] = fill.Clone();
    }

    /// <summary>Creates a deep copy of this map.</summary>
    public AsciiMap Clone()
    {
        var copy = new AsciiMap(Width, Height);
        for (var x = 0; x < Width; x++)
        for (var y = 0; y < Height; y++)
            copy._cells[x, y] = _cells[x, y].Clone();
        return copy;
    }

    private void ValidateBounds(int x, int y)
    {
        if (!IsInBounds(x, y))
            throw new ArgumentOutOfRangeException(
                $"({x},{y})",
                $"Coordinates must be within [0,{Width - 1}] × [0,{Height - 1}].");
    }
}
