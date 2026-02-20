namespace RogueEngine.Core.Scene;

/// <summary>
/// Central registry for <see cref="SpriteDefinition"/> objects and
/// <see cref="SpriteSheet"/> atlases used by a game project.
///
/// <para>
/// The library provides a single lookup point so that
/// <see cref="SpriteNode"/> instances and the visual-script executor can
/// resolve sprite names at runtime without hard-coding paths.
/// </para>
///
/// <para>
/// A default set of built-in ASCII-only sprites (wall, floor, player, monster,
/// item) is always present and can be overridden by registering a sprite with
/// the same name.
/// </para>
/// </summary>
public sealed class SpriteLibrary
{
    private readonly Dictionary<string, SpriteDefinition> _sprites  = [];
    private readonly Dictionary<string, SpriteSheet>      _sheets   = [];

    /// <summary>Creates a new library pre-populated with the built-in sprites.</summary>
    public SpriteLibrary() => RegisterBuiltins();

    // ── Read-only views ────────────────────────────────────────────────────────

    /// <summary>All registered sprite definitions, keyed by name.</summary>
    public IReadOnlyDictionary<string, SpriteDefinition> Sprites => _sprites;

    /// <summary>All registered sprite sheets, keyed by name.</summary>
    public IReadOnlyDictionary<string, SpriteSheet> Sheets => _sheets;

    // ── Registration ──────────────────────────────────────────────────────────

    /// <summary>
    /// Registers (or replaces) a <see cref="SpriteDefinition"/>.
    /// The sprite is keyed by <see cref="SpriteDefinition.Name"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sprite"/> is null.</exception>
    public void Register(SpriteDefinition sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        _sprites[sprite.Name] = sprite;
    }

    /// <summary>
    /// Registers a <see cref="SpriteSheet"/> and automatically creates a
    /// <see cref="SpriteDefinition"/> for each tile using the supplied
    /// <paramref name="glyphMap"/> (tile-name → glyph character).
    /// Tiles not present in <paramref name="glyphMap"/> get glyph <c>'?'</c>.
    /// </summary>
    public void RegisterSheet(SpriteSheet sheet, Dictionary<string, char>? glyphMap = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        _sheets[sheet.Name] = sheet;

        foreach (var tileName in sheet.Tiles.Keys)
        {
            var glyph = glyphMap?.GetValueOrDefault(tileName, '?') ?? '?';
            var sprite = sheet.CreateSprite(tileName, glyph);
            _sprites[sprite.Name] = sprite;
        }
    }

    // ── Lookup ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="SpriteDefinition"/> with the given name,
    /// or <see langword="null"/> if it is not registered.
    /// </summary>
    public SpriteDefinition? Get(string name) =>
        _sprites.GetValueOrDefault(name);

    /// <summary>
    /// Returns the <see cref="SpriteDefinition"/> for the given tile in the
    /// named sheet, or <see langword="null"/> if either the sheet or tile is
    /// unknown.
    /// </summary>
    public SpriteDefinition? GetFromSheet(string sheetName, string tileName) =>
        Get($"{sheetName}/{tileName}");

    /// <summary>
    /// Returns the <see cref="SpriteSheet"/> with the given name,
    /// or <see langword="null"/> if it is not registered.
    /// </summary>
    public SpriteSheet? GetSheet(string sheetName) =>
        _sheets.GetValueOrDefault(sheetName);

    /// <summary>
    /// Resolves the best <see cref="SpriteDefinition"/> to render for a given
    /// name.  Returns the built-in "unknown" sprite if the name is not found.
    /// </summary>
    public SpriteDefinition Resolve(string name) =>
        Get(name) ?? _sprites["unknown"];

    // ── Built-in sprite definitions ────────────────────────────────────────────

    private void RegisterBuiltins()
    {
        Add("unknown",          '?', 0xFF00FF, 0x000000);
        Add("wall",             '#', 0x888888, 0x000000);
        Add("wall.lit",         '#', 0xAAAAAA, 0x111111);
        Add("floor",            '.', 0x666666, 0x000000);
        Add("floor.lit",        '.', 0x999999, 0x111111);
        Add("floor.grass",      ',', 0x44AA44, 0x000000);
        Add("floor.water",      '~', 0x2244FF, 0x000011);
        Add("door.closed",      '+', 0xAA8833, 0x000000);
        Add("door.open",        '/', 0xAA8833, 0x000000);
        Add("stairs.down",      '>', 0xCCCCCC, 0x000000);
        Add("stairs.up",        '<', 0xCCCCCC, 0x000000);
        Add("player",           '@', 0xFFFFFF, 0x000000);
        Add("npc",              '@', 0xFFFF00, 0x000000);
        Add("monster.rat",      'r', 0xAA6633, 0x000000);
        Add("monster.goblin",   'g', 0x00AA00, 0x000000);
        Add("monster.orc",      'o', 0x338800, 0x000000);
        Add("monster.troll",    'T', 0x226600, 0x000000);
        Add("monster.skeleton", 's', 0xCCCCCC, 0x000000);
        Add("monster.dragon",   'D', 0xFF4400, 0x000000);
        Add("monster.boss",     'B', 0xFF0000, 0x000000);
        Add("item.potion",      '!', 0xFF44FF, 0x000000);
        Add("item.scroll",      '?', 0xFFFF88, 0x000000);
        Add("item.weapon",      '/', 0xCCCCCC, 0x000000);
        Add("item.armor",       '[', 0x6688AA, 0x000000);
        Add("item.gold",        '$', 0xFFDD00, 0x000000);
        Add("item.key",         'k', 0xFFDD00, 0x000000);
        Add("item.chest",       'C', 0xAA8833, 0x000000);
        Add("item.torch",       'i', 0xFF9900, 0x000000);
        Add("trap",             '^', 0xFF4400, 0x000000);
        Add("projectile.arrow", '-', 0xAAAAAA, 0x000000);
        Add("effect.explosion", '*', 0xFF8800, 0x000000);
        Add("effect.magic",     '*', 0x8844FF, 0x000000);
        Add("ui.cursor",        'X', 0xFFFFFF, 0x444444);
        Add("ui.selection",     '[', 0xFFFF00, 0x003300);
        Add("void",             ' ', 0x000000, 0x000000);
    }

    private void Add(string name, char glyph, int fg, int bg) =>
        _sprites[name] = new SpriteDefinition
        {
            Name            = name,
            Glyph           = glyph,
            ForegroundColor = fg,
            BackgroundColor = bg,
            RenderMode      = SpriteRenderMode.Auto,
        };
}
