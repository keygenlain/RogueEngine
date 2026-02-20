using RogueEngine.Core.Models;

namespace RogueEngine.Core.Engine;

/// <summary>
/// Procedural-generation algorithms that can be applied to an <see cref="AsciiMap"/>.
/// Each method is deterministic when a seed is supplied so that games can be
/// reproduced from a saved seed value.
/// </summary>
public static class MapGenerator
{
    // ── Shared cell templates ──────────────────────────────────────────────────

    private static AsciiCell FloorCell(int fg = 0xAAAAAA) =>
        new() { Character = '.', ForegroundColor = fg, BackgroundColor = 0x000000 };

    private static AsciiCell WallCell(int fg = 0x888888) =>
        new() { Character = '#', ForegroundColor = fg, BackgroundColor = 0x111111 };

    // ── Cellular-automata cave ─────────────────────────────────────────────────

    /// <summary>
    /// Generates a cave map using the classic cellular-automata algorithm:
    /// <list type="number">
    ///   <item>Randomly fill the grid according to <paramref name="fillRatio"/>.</item>
    ///   <item>Run <paramref name="iterations"/> smoothing passes.</item>
    /// </list>
    /// Cells with ≥ 5 wall neighbours become walls; others become floor.
    /// The border is always solid wall.
    /// </summary>
    /// <param name="map">The map to fill. Existing content is overwritten.</param>
    /// <param name="fillRatio">
    /// Initial proportion of cells that are walls (0.0 – 1.0).
    /// A value around 0.45 produces typical caves.
    /// </param>
    /// <param name="iterations">Number of smoothing passes (3 – 7 recommended).</param>
    /// <param name="seed">
    /// Optional RNG seed for reproducibility. Pass <see langword="null"/> for random.
    /// </param>
    public static void GenerateCave(
        AsciiMap map,
        double fillRatio = 0.45,
        int iterations = 5,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        fillRatio = Math.Clamp(fillRatio, 0.0, 1.0);
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        // Step 1: random fill
        var grid = new bool[map.Width, map.Height]; // true = wall
        for (var x = 0; x < map.Width; x++)
        for (var y = 0; y < map.Height; y++)
            grid[x, y] = IsBorder(x, y, map) || rng.NextDouble() < fillRatio;

        // Step 2: smoothing passes
        for (var i = 0; i < iterations; i++)
        {
            var next = new bool[map.Width, map.Height];
            for (var x = 0; x < map.Width; x++)
            for (var y = 0; y < map.Height; y++)
            {
                if (IsBorder(x, y, map)) { next[x, y] = true; continue; }
                next[x, y] = CountWallNeighbours(grid, x, y, map) >= 5;
            }
            grid = next;
        }

        // Step 3: write cells to map
        for (var x = 0; x < map.Width; x++)
        for (var y = 0; y < map.Height; y++)
            map[x, y] = grid[x, y] ? WallCell() : FloorCell();
    }

    // ── BSP room placement ─────────────────────────────────────────────────────

    /// <summary>
    /// Generates a dungeon using Binary Space Partitioning (BSP).
    /// The map is recursively split into leaves; each leaf receives a room;
    /// sibling rooms are connected by L-shaped corridors.
    /// </summary>
    /// <param name="map">The map to fill.</param>
    /// <param name="minRoomSize">Minimum room dimension (width or height).</param>
    /// <param name="maxRoomSize">Maximum room dimension.</param>
    /// <param name="seed">Optional RNG seed.</param>
    public static void GenerateRoomsBSP(
        AsciiMap map,
        int minRoomSize = 4,
        int maxRoomSize = 12,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        minRoomSize = Math.Max(2, minRoomSize);
        maxRoomSize = Math.Max(minRoomSize + 1, maxRoomSize);
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        // Fill everything with wall first.
        map.Fill(WallCell());

        var rooms = new List<(int X, int Y, int W, int H)>();
        Partition(0, 0, map.Width, map.Height, rng, minRoomSize, maxRoomSize, rooms, map);

        // Carve corridors between consecutive room centres.
        for (var i = 1; i < rooms.Count; i++)
            CarveCorridorL(map,
                rooms[i - 1].X + rooms[i - 1].W / 2,
                rooms[i - 1].Y + rooms[i - 1].H / 2,
                rooms[i].X + rooms[i].W / 2,
                rooms[i].Y + rooms[i].H / 2);
    }

    private static void Partition(
        int x, int y, int w, int h,
        Random rng, int minRoom, int maxRoom,
        List<(int, int, int, int)> rooms,
        AsciiMap map)
    {
        const int minLeaf = 6;
        bool canSplitH = h >= minLeaf * 2;
        bool canSplitV = w >= minLeaf * 2;

        if (!canSplitH && !canSplitV)
        {
            // Leaf: carve a room.
            var rw = Math.Clamp(rng.Next(minRoom, maxRoom + 1), 2, w - 2);
            var rh = Math.Clamp(rng.Next(minRoom, maxRoom + 1), 2, h - 2);
            var rx = x + 1 + rng.Next(w - rw - 1);
            var ry = y + 1 + rng.Next(h - rh - 1);
            map.FillRegion(rx, ry, rw, rh, FloorCell());
            rooms.Add((rx, ry, rw, rh));
            return;
        }

        bool splitH = canSplitH && (!canSplitV || rng.Next(2) == 0);
        if (splitH)
        {
            var split = y + minLeaf + rng.Next(h - minLeaf * 2);
            Partition(x, y, w, split - y, rng, minRoom, maxRoom, rooms, map);
            Partition(x, split, w, h - (split - y), rng, minRoom, maxRoom, rooms, map);
        }
        else
        {
            var split = x + minLeaf + rng.Next(w - minLeaf * 2);
            Partition(x, y, split - x, h, rng, minRoom, maxRoom, rooms, map);
            Partition(split, y, w - (split - x), h, rng, minRoom, maxRoom, rooms, map);
        }
    }

    private static void CarveCorridorL(AsciiMap map, int x1, int y1, int x2, int y2)
    {
        for (var x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
            if (map.IsInBounds(x, y1)) map[x, y1] = FloorCell(0x886644);

        for (var y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
            if (map.IsInBounds(x2, y)) map[x2, y] = FloorCell(0x886644);
    }

    // ── Drunkard Walk ──────────────────────────────────────────────────────────

    /// <summary>
    /// Generates open space by simulating a "drunk" agent that randomly walks
    /// through the map, carving floor tiles as it goes.
    /// </summary>
    /// <param name="map">The map to fill (starts entirely as walls).</param>
    /// <param name="steps">Number of steps the drunkard takes.</param>
    /// <param name="seed">Optional RNG seed.</param>
    public static void GenerateDrunkardWalk(
        AsciiMap map,
        int steps = 500,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        map.Fill(WallCell());
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        int x = map.Width / 2;
        int y = map.Height / 2;
        int[] dx = [0, 0, -1, 1];
        int[] dy = [-1, 1, 0, 0];

        for (var step = 0; step < steps; step++)
        {
            map[x, y] = FloorCell();
            var dir = rng.Next(4);
            var nx = x + dx[dir];
            var ny = y + dy[dir];
            if (map.IsInBounds(nx, ny) && !IsBorder(nx, ny, map))
            {
                x = nx;
                y = ny;
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static bool IsBorder(int x, int y, AsciiMap map) =>
        x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1;

    private static int CountWallNeighbours(bool[,] grid, int x, int y, AsciiMap map)
    {
        var count = 0;
        for (var nx = x - 1; nx <= x + 1; nx++)
        for (var ny = y - 1; ny <= y + 1; ny++)
        {
            if (nx == x && ny == y) continue;
            if (!map.IsInBounds(nx, ny)) { count++; continue; } // out-of-bounds = wall
            if (grid[nx, ny]) count++;
        }
        return count;
    }
}
