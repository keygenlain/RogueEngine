using RogueEngine.Core.Models;

namespace RogueEngine.Core.Runtime.Rendering;

/// <summary>
/// CPU-side ASCII frame buffer that can be rendered by any frontend.
/// </summary>
public sealed class AsciiFrameBuffer
{
    private readonly AsciiCell[,] _cells;

    public AsciiFrameBuffer(int width, int height)
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

    public int Width { get; }

    public int Height { get; }

    public AsciiCell this[int x, int y]
    {
        get => _cells[x, y];
        set => _cells[x, y] = value;
    }

    public void Clear(char glyph = ' ', int fg = 0xFFFFFF, int bg = 0x000000)
    {
        for (var x = 0; x < Width; x++)
        for (var y = 0; y < Height; y++)
            _cells[x, y] = new AsciiCell { Character = glyph, ForegroundColor = fg, BackgroundColor = bg };
    }

    public void BlitMap(AsciiMap map)
    {
        var copyW = Math.Min(map.Width, Width);
        var copyH = Math.Min(map.Height, Height);
        for (var x = 0; x < copyW; x++)
        for (var y = 0; y < copyH; y++)
            _cells[x, y] = map[x, y].Clone();
    }

    public void DrawEntity(Entity entity)
    {
        if (entity.X < 0 || entity.Y < 0 || entity.X >= Width || entity.Y >= Height)
            return;

        _cells[entity.X, entity.Y] = new AsciiCell
        {
            Character = entity.Glyph,
            ForegroundColor = entity.ForegroundColor,
            BackgroundColor = _cells[entity.X, entity.Y].BackgroundColor,
        };
    }
}
