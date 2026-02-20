namespace RogueEngine.Core.Models;

/// <summary>
/// A game entity – any interactive object (player, monster, item, NPC, etc.)
/// placed on an <see cref="AsciiMap"/>.
/// </summary>
public sealed class Entity
{
    /// <summary>Unique identifier for this entity.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Display name (e.g. "Player", "Goblin", "Gold Coin").</summary>
    public string Name { get; set; } = "Entity";

    /// <summary>ASCII character used to represent this entity on the map.</summary>
    public char Glyph { get; set; } = '?';

    /// <summary>Foreground colour encoded as 0xRRGGBB.</summary>
    public int ForegroundColor { get; set; } = 0xFFFFFF;

    /// <summary>Current column position on the map.</summary>
    public int X { get; set; }

    /// <summary>Current row position on the map.</summary>
    public int Y { get; set; }

    /// <summary>Whether this entity blocks movement through its tile.</summary>
    public bool BlocksMovement { get; set; }

    /// <summary>Arbitrary key-value properties (health, damage, etc.).</summary>
    public Dictionary<string, string> Properties { get; } = [];

    /// <summary>
    /// Moves the entity by (<paramref name="dx"/>, <paramref name="dy"/>) tiles,
    /// optionally clamping to the supplied map bounds.
    /// </summary>
    public void Move(int dx, int dy, int? maxX = null, int? maxY = null)
    {
        X = maxX.HasValue ? Math.Clamp(X + dx, 0, maxX.Value - 1) : X + dx;
        Y = maxY.HasValue ? Math.Clamp(Y + dy, 0, maxY.Value - 1) : Y + dy;
    }

    /// <inheritdoc/>
    public override string ToString() => $"[{Glyph}] {Name} @ ({X},{Y})";
}
