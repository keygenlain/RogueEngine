using RogueEngine.Core.Engine;
using RogueEngine.Core.Models;

namespace RogueEngine.Tests;

/// <summary>
/// Tests for <see cref="MapGenerator"/>.
/// </summary>
public sealed class MapGeneratorTests
{
    [Fact]
    public void GenerateCave_FillsEntireMap()
    {
        var map = new AsciiMap(40, 20);
        MapGenerator.GenerateCave(map, fillRatio: 0.45, iterations: 5, seed: 42);

        // Every cell should be either '#' (wall) or '.' (floor)
        for (var x = 0; x < map.Width; x++)
        for (var y = 0; y < map.Height; y++)
        {
            var ch = map[x, y].Character;
            Assert.True(ch is '#' or '.', $"Unexpected char '{ch}' at ({x},{y})");
        }
    }

    [Fact]
    public void GenerateCave_BorderAlwaysWall()
    {
        var map = new AsciiMap(30, 15);
        MapGenerator.GenerateCave(map, seed: 1);

        for (var x = 0; x < map.Width; x++)
        {
            Assert.Equal('#', map[x, 0].Character);
            Assert.Equal('#', map[x, map.Height - 1].Character);
        }
        for (var y = 0; y < map.Height; y++)
        {
            Assert.Equal('#', map[0, y].Character);
            Assert.Equal('#', map[map.Width - 1, y].Character);
        }
    }

    [Fact]
    public void GenerateCave_IsDeterministic()
    {
        var map1 = new AsciiMap(20, 10);
        var map2 = new AsciiMap(20, 10);
        MapGenerator.GenerateCave(map1, seed: 99);
        MapGenerator.GenerateCave(map2, seed: 99);

        for (var x = 0; x < map1.Width; x++)
        for (var y = 0; y < map1.Height; y++)
            Assert.Equal(map1[x, y].Character, map2[x, y].Character);
    }

    [Fact]
    public void GenerateRoomsBSP_ProducesFloorTiles()
    {
        var map = new AsciiMap(60, 30);
        MapGenerator.GenerateRoomsBSP(map, minRoomSize: 4, maxRoomSize: 10, seed: 7);

        var floorCount = 0;
        for (var x = 0; x < map.Width; x++)
        for (var y = 0; y < map.Height; y++)
            if (map[x, y].Character == '.') floorCount++;

        Assert.True(floorCount > 0, "BSP map should contain at least one floor tile");
    }

    [Fact]
    public void GenerateDrunkardWalk_CarvesSomeFloor()
    {
        var map = new AsciiMap(40, 20);
        MapGenerator.GenerateDrunkardWalk(map, steps: 300, seed: 5);

        var floorCount = 0;
        for (var x = 0; x < map.Width; x++)
        for (var y = 0; y < map.Height; y++)
            if (map[x, y].Character == '.') floorCount++;

        Assert.True(floorCount > 0, "Drunkard walk should carve at least one floor tile");
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public void AsciiMap_ThrowsForInvalidDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AsciiMap(width, height));
    }

    [Fact]
    public void AsciiMap_FillRegion_ClampsToMapBounds()
    {
        var map = new AsciiMap(10, 10);
        // Fill a region partially outside the map
        map.FillRegion(-2, -2, 5, 5, new AsciiCell { Character = 'X' });

        // Cells in bounds should be 'X'
        Assert.Equal('X', map[0, 0].Character);
        Assert.Equal('X', map[2, 2].Character);
        // Cells outside region should still be default space
        Assert.Equal(' ', map[5, 5].Character);
    }

    [Fact]
    public void AsciiMap_CloneIsDeepCopy()
    {
        var original = new AsciiMap(5, 5);
        original[2, 2] = new AsciiCell { Character = '@' };
        var clone = original.Clone();

        // Modifying clone should not affect original
        clone[2, 2].Character = 'X';
        Assert.Equal('@', original[2, 2].Character);
    }

    // ── PlaceCustomRoom ────────────────────────────────────────────────────────

    [Fact]
    public void PlaceCustomRoom_StampsLayoutAtOrigin()
    {
        var map = new AsciiMap(20, 20);
        const string layout = "###\n#.#\n###";

        MapGenerator.PlaceCustomRoom(map, layout, 2, 3);

        Assert.Equal('#', map[2, 3].Character);
        Assert.Equal('.', map[3, 4].Character);
        Assert.Equal('#', map[4, 5].Character);
    }

    [Fact]
    public void PlaceCustomRoom_TransparentSpaceIsSkipped()
    {
        var map = new AsciiMap(10, 10);
        map[0, 0] = new AsciiCell { Character = '@' };

        // Space in layout means "leave existing cell alone"
        MapGenerator.PlaceCustomRoom(map, " ", 0, 0);

        Assert.Equal('@', map[0, 0].Character);
    }

    [Fact]
    public void PlaceCustomRoom_OutOfBoundsCellsAreIgnored()
    {
        var map = new AsciiMap(5, 5);
        // Origin at (-1, -1): cells at column/row -1 of the layout are out of bounds.
        // Layout row 1, col 1 ('.') maps to map[(-1+1), (-1+1)] = (0, 0).
        MapGenerator.PlaceCustomRoom(map, "###\n#.#\n###", -1, -1);

        // No exception, and the in-bounds '.' cell landed at (0, 0).
        Assert.Equal('.', map[0, 0].Character);
    }

    [Fact]
    public void PlaceCustomRoom_EmptyLayoutDoesNothing()
    {
        var map = new AsciiMap(5, 5);
        map[0, 0] = new AsciiCell { Character = 'X' };

        MapGenerator.PlaceCustomRoom(map, "", 0, 0);

        Assert.Equal('X', map[0, 0].Character);
    }

    [Fact]
    public void PlaceCustomRoom_CustomCharacter_PreservesGlyph()
    {
        var map = new AsciiMap(10, 10);
        MapGenerator.PlaceCustomRoom(map, "+", 3, 3);

        Assert.Equal('+', map[3, 3].Character);
    }
}
