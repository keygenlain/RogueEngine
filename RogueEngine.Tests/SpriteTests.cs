using RogueEngine.Core.Scene;

namespace RogueEngine.Tests;

/// <summary>
/// Unit tests for <see cref="SpriteDefinition"/>, <see cref="SpriteSheet"/>,
/// and <see cref="SpriteLibrary"/>.
/// </summary>
public sealed class SpriteTests
{
    // ── SpriteDefinition ──────────────────────────────────────────────────────

    [Fact]
    public void SpriteDefinition_HasGraphic_FalseWhenNoImagePath()
    {
        var s = new SpriteDefinition { Name = "wall", Glyph = '#' };
        Assert.False(s.HasGraphic);
    }

    [Fact]
    public void SpriteDefinition_HasGraphic_TrueWhenImagePathSet()
    {
        var s = new SpriteDefinition { Name = "wall", ImagePath = "tiles.png" };
        Assert.True(s.HasGraphic);
    }

    [Fact]
    public void SpriteDefinition_EffectiveMode_FallsBackToAscii_WhenNoGraphicAndAuto()
    {
        var s = new SpriteDefinition { RenderMode = SpriteRenderMode.Auto };
        Assert.Equal(SpriteRenderMode.AsciiOnly, s.EffectiveMode);
    }

    [Fact]
    public void SpriteDefinition_EffectiveMode_RemainsAuto_WhenGraphicPresent()
    {
        var s = new SpriteDefinition
        {
            RenderMode = SpriteRenderMode.Auto,
            ImagePath  = "tiles.png",
        };
        Assert.Equal(SpriteRenderMode.Auto, s.EffectiveMode);
    }

    [Fact]
    public void SpriteDefinition_EffectiveMode_AsciiOnly_EvenWithImage()
    {
        var s = new SpriteDefinition
        {
            RenderMode = SpriteRenderMode.AsciiOnly,
            ImagePath  = "tiles.png",
        };
        Assert.Equal(SpriteRenderMode.AsciiOnly, s.EffectiveMode);
    }

    // ── SpriteSheet ───────────────────────────────────────────────────────────

    [Fact]
    public void SpriteSheet_RegisterTile_StoredCorrectly()
    {
        var sheet = new SpriteSheet { Name = "test", TileWidth = 16, TileHeight = 16 };
        sheet.RegisterTile("wall", 2, 3);

        Assert.True(sheet.Tiles.ContainsKey("wall"));
        Assert.Equal((2, 3), sheet.Tiles["wall"]);
    }

    [Fact]
    public void SpriteSheet_GetTileRegion_ComputesPixelRect()
    {
        var sheet = new SpriteSheet
        {
            Name = "test",
            TileWidth = 16, TileHeight = 16,
            SpacingX = 2, SpacingY = 2,
            MarginX  = 4, MarginY  = 4,
        };
        sheet.RegisterTile("door", 1, 2);
        // X = 4 + 1*(16+2) = 22; Y = 4 + 2*(16+2) = 40; W=16; H=16
        var region = sheet.GetTileRegion("door");
        Assert.NotNull(region);
        Assert.Equal(22, region!.Value.X);
        Assert.Equal(40, region!.Value.Y);
        Assert.Equal(16, region!.Value.Width);
        Assert.Equal(16, region!.Value.Height);
    }

    [Fact]
    public void SpriteSheet_GetTileRegion_NullForUnknownTile()
    {
        var sheet = new SpriteSheet { Name = "test", TileWidth = 16, TileHeight = 16 };
        Assert.Null(sheet.GetTileRegion("unknown"));
    }

    [Fact]
    public void SpriteSheet_RegisterRange_NamesSequentially()
    {
        var sheet = new SpriteSheet { Name = "test", TileWidth = 16, TileHeight = 16 };
        sheet.RegisterRange("floor_", 4, startCol: 0, startRow: 0, columns: 16);

        Assert.True(sheet.Tiles.ContainsKey("floor_0"));
        Assert.True(sheet.Tiles.ContainsKey("floor_1"));
        Assert.True(sheet.Tiles.ContainsKey("floor_2"));
        Assert.True(sheet.Tiles.ContainsKey("floor_3"));
        Assert.Equal((0, 0), sheet.Tiles["floor_0"]);
        Assert.Equal((1, 0), sheet.Tiles["floor_1"]);
    }

    [Fact]
    public void SpriteSheet_CreateSprite_SetsImageAndAsciiFields()
    {
        var sheet = new SpriteSheet
        {
            Name = "dungeon", ImagePath = "dungeon.png",
            TileWidth = 16, TileHeight = 16,
        };
        sheet.RegisterTile("wall", 0, 0);
        var sprite = sheet.CreateSprite("wall", '#', 0x888888, 0x000000);

        Assert.Equal("dungeon/wall", sprite.Name);
        Assert.Equal('#',            sprite.Glyph);
        Assert.Equal(0x888888,       sprite.ForegroundColor);
        Assert.Equal("dungeon.png",  sprite.ImagePath);
        Assert.Equal("dungeon",      sprite.SheetName);
        Assert.Equal(16,             sprite.TileWidth);
        Assert.True(sprite.HasGraphic);
    }

    // ── SpriteLibrary ─────────────────────────────────────────────────────────

    [Fact]
    public void SpriteLibrary_HasBuiltinSprites()
    {
        var lib = new SpriteLibrary();
        Assert.NotNull(lib.Get("player"));
        Assert.NotNull(lib.Get("wall"));
        Assert.NotNull(lib.Get("floor"));
        Assert.NotNull(lib.Get("unknown"));
    }

    [Fact]
    public void SpriteLibrary_BuiltinPlayer_IsAtSign()
    {
        var lib    = new SpriteLibrary();
        var player = lib.Get("player")!;
        Assert.Equal('@', player.Glyph);
    }

    [Fact]
    public void SpriteLibrary_Register_OverridesExisting()
    {
        var lib = new SpriteLibrary();
        lib.Register(new SpriteDefinition
        {
            Name  = "player",
            Glyph = '$',   // override default '@'
        });
        Assert.Equal('$', lib.Get("player")!.Glyph);
    }

    [Fact]
    public void SpriteLibrary_Register_AddsNew()
    {
        var lib = new SpriteLibrary();
        lib.Register(new SpriteDefinition { Name = "my_hero", Glyph = 'h' });
        Assert.NotNull(lib.Get("my_hero"));
        Assert.Equal('h', lib.Get("my_hero")!.Glyph);
    }

    [Fact]
    public void SpriteLibrary_Resolve_FallsBackToUnknown()
    {
        var lib = new SpriteLibrary();
        var s   = lib.Resolve("does_not_exist");
        Assert.NotNull(s);
        Assert.Equal('?', s.Glyph);
    }

    [Fact]
    public void SpriteLibrary_RegisterSheet_CreatesSpritesForAllTiles()
    {
        var sheet = new SpriteSheet
        {
            Name = "tiles", ImagePath = "tiles.png",
            TileWidth = 16, TileHeight = 16,
        };
        sheet.RegisterTile("wall",  0, 0);
        sheet.RegisterTile("floor", 1, 0);

        var lib = new SpriteLibrary();
        lib.RegisterSheet(sheet, new Dictionary<string, char>
        {
            ["wall"]  = '#',
            ["floor"] = '.',
        });

        var wall  = lib.Get("tiles/wall");
        var floor = lib.Get("tiles/floor");
        Assert.NotNull(wall);
        Assert.NotNull(floor);
        Assert.Equal('#', wall!.Glyph);
        Assert.Equal('.', floor!.Glyph);
        Assert.True(wall.HasGraphic);
    }

    [Fact]
    public void SpriteLibrary_GetFromSheet_FindsBySheetAndTile()
    {
        var sheet = new SpriteSheet
        {
            Name = "ui", ImagePath = "ui.png",
            TileWidth = 8, TileHeight = 8,
        };
        sheet.RegisterTile("cursor", 0, 0);

        var lib = new SpriteLibrary();
        lib.RegisterSheet(sheet);

        var sprite = lib.GetFromSheet("ui", "cursor");
        Assert.NotNull(sprite);
    }

    [Fact]
    public void SpriteLibrary_GetFromSheet_NullForUnknown()
    {
        var lib = new SpriteLibrary();
        Assert.Null(lib.GetFromSheet("unknown_sheet", "tile"));
    }

    // ── Animation frames ──────────────────────────────────────────────────────

    [Fact]
    public void SpriteDefinition_AnimationFrames_Empty_CurrentFrameIsDefault()
    {
        var node = new SpriteNode { SpriteName = "player" };
        Assert.Equal("player", node.CurrentFrameName);
    }

    [Fact]
    public void SpriteNode_Animation_CyclesThroughFrames()
    {
        var lib = new SpriteLibrary();
        lib.Register(new SpriteDefinition { Name = "walk0", Glyph = 'a' });
        lib.Register(new SpriteDefinition { Name = "walk1", Glyph = 'b' });
        lib.Register(new SpriteDefinition
        {
            Name             = "walk",
            Glyph            = 'a',
            AnimationFrames  = ["walk0", "walk1"],
            FramesPerSecond  = 2f,   // 0.5 s per frame
        });

        var tree   = new SceneTree(lib);
        var scene  = new GridNode  { Name = "scene" };
        var sprite = new SpriteNode { Name = "sp", SpriteName = "walk" };
        sprite.Sprite = lib.Get("walk");
        scene.AddChild(sprite);
        tree.AddScene("s", scene);
        tree.ChangeScene("s");

        // At t=0 → frame 0 ("walk0")
        Assert.Equal("walk0", sprite.CurrentFrameName);

        // Advance 0.6 s → past 0.5 s threshold → frame 1 ("walk1")
        tree.Update(0.6);
        Assert.Equal("walk1", sprite.CurrentFrameName);

        // Advance another 0.6 s → wraps back to frame 0
        tree.Update(0.6);
        Assert.Equal("walk0", sprite.CurrentFrameName);
    }

    // ── SpriteName on AsciiCell ────────────────────────────────────────────────

    [Fact]
    public void AsciiCell_SpriteName_RoundTrips()
    {
        var cell = new Core.Models.AsciiCell
        {
            Character  = '@',
            SpriteName = "player",
        };
        var clone = cell.Clone();
        Assert.Equal("player", clone.SpriteName);
    }

    [Fact]
    public void AsciiCell_SpriteName_NullByDefault()
    {
        var cell = new Core.Models.AsciiCell();
        Assert.Null(cell.SpriteName);
    }
}
