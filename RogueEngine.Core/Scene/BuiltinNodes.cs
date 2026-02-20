using RogueEngine.Core.Models;

namespace RogueEngine.Core.Scene;

// ─────────────────────────────────────────────────────────────────────────────
// Built-in scene node types
//
// Each class lives here to keep the Scene/ folder self-contained.
// All nodes inherit from SceneNode and add specialised properties / behaviour.
// ─────────────────────────────────────────────────────────────────────────────

// ── GridNode ──────────────────────────────────────────────────────────────────

/// <summary>
/// A <see cref="SceneNode"/> that has a position on the ASCII grid.
/// All visible game objects (sprites, entities, labels) inherit from this.
/// </summary>
public class GridNode : SceneNode
{
    /// <summary>Column position on the ASCII grid (0-based).</summary>
    public int GridX { get; set; }

    /// <summary>Row position on the ASCII grid (0-based).</summary>
    public int GridY { get; set; }

    /// <summary>
    /// Z-order for rendering.  Higher values are drawn on top.
    /// ASCII characters are always drawn in Z order; graphical sprites respect
    /// this when the WPF renderer layers images.
    /// </summary>
    public int ZIndex { get; set; }

    /// <summary>Moves this node by (dx, dy) grid cells.</summary>
    public void Move(int dx, int dy) { GridX += dx; GridY += dy; }

    /// <summary>Teleports this node to an absolute grid position.</summary>
    public void SetPosition(int x, int y) { GridX = x; GridY = y; }
}

// ── SpriteNode ────────────────────────────────────────────────────────────────

/// <summary>
/// Renders a sprite at its grid position.
///
/// <para>
/// The sprite can be a pure ASCII glyph or a graphical tile from a
/// <see cref="SpriteSheet"/>, depending on the sprite's
/// <see cref="SpriteDefinition.RenderMode"/> and whether the WPF renderer has
/// a graphical back-end enabled.
/// </para>
/// </summary>
public sealed class SpriteNode : GridNode
{
    /// <summary>
    /// Name of the sprite to render, looked up in the scene's
    /// <see cref="SpriteLibrary"/> at render time.
    /// </summary>
    public string SpriteName { get; set; } = "unknown";

    /// <summary>
    /// Resolved sprite definition.  The <see cref="SceneTree"/> fills this
    /// in from the <see cref="SpriteLibrary"/> when the node enters the tree.
    /// May be <see langword="null"/> until then.
    /// </summary>
    public SpriteDefinition? Sprite { get; set; }

    // ── Animation state ───────────────────────────────────────────────────────

    /// <summary>Whether animation is currently playing.</summary>
    public bool Playing { get; set; } = true;

    private double _animTimer;
    private int    _frameIndex;

    /// <inheritdoc/>
    protected override void OnUpdate(double deltaSeconds)
    {
        if (Sprite is null || !Playing || Sprite.AnimationFrames.Count < 2) return;
        _animTimer += deltaSeconds;
        var frameDur = Sprite.FramesPerSecond > 0 ? 1.0 / Sprite.FramesPerSecond : 0.25;
        if (_animTimer >= frameDur)
        {
            _animTimer -= frameDur;
            _frameIndex = (_frameIndex + 1) % Sprite.AnimationFrames.Count;
        }
    }

    /// <summary>
    /// Returns the name of the current animation frame sprite, or
    /// <see cref="SpriteName"/> when not animating.
    /// </summary>
    public string CurrentFrameName =>
        Sprite?.AnimationFrames.Count > 0
            ? Sprite.AnimationFrames[_frameIndex % Sprite.AnimationFrames.Count]
            : SpriteName;
}

// ── EntityNode ────────────────────────────────────────────────────────────────

/// <summary>
/// Wraps an <see cref="Models.Entity"/> as a scene node, synchronising its grid
/// position with <see cref="GridNode.GridX"/> / <see cref="GridNode.GridY"/>.
/// </summary>
public sealed class EntityNode : GridNode
{
    private Entity? _entity;

    /// <summary>
    /// The underlying game entity.
    /// Setting this property synchronises the grid position from the entity.
    /// </summary>
    public Entity? Entity
    {
        get => _entity;
        set
        {
            _entity = value;
            if (value is not null) { GridX = value.X; GridY = value.Y; }
        }
    }

    /// <inheritdoc/>
    protected override void OnUpdate(double deltaSeconds)
    {
        // Keep entity and grid positions in sync.
        if (_entity is null) return;
        _entity.X = GridX;
        _entity.Y = GridY;
    }
}

// ── MapNode ───────────────────────────────────────────────────────────────────

/// <summary>
/// Holds an <see cref="AsciiMap"/> and exposes it to the scene tree.
/// The <see cref="SceneTree"/> renderer composites this node's map as the
/// bottom-most rendering layer before drawing sprites and entities.
/// </summary>
public sealed class MapNode : SceneNode
{
    /// <summary>The map managed by this node.</summary>
    public AsciiMap? Map { get; set; }

    /// <summary>Column offset when rendering this map on screen.</summary>
    public int OffsetX { get; set; }

    /// <summary>Row offset when rendering this map on screen.</summary>
    public int OffsetY { get; set; }
}

// ── LabelNode ─────────────────────────────────────────────────────────────────

/// <summary>
/// Displays a string of text at a grid position.
/// Each character in <see cref="Text"/> occupies one grid cell.
/// </summary>
public sealed class LabelNode : GridNode
{
    /// <summary>Text to display.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Foreground colour (0xRRGGBB).</summary>
    public int ForegroundColor { get; set; } = 0xFFFFFF;

    /// <summary>Background colour (0xRRGGBB). Use 0x000000 for transparent-black.</summary>
    public int BackgroundColor { get; set; } = 0x000000;

    /// <summary>Whether the label wraps at <see cref="MaxWidth"/> characters.</summary>
    public bool WordWrap { get; set; }

    /// <summary>Maximum width in characters before wrapping (0 = no limit).</summary>
    public int MaxWidth { get; set; }
}

// ── TimerNode ─────────────────────────────────────────────────────────────────

/// <summary>
/// Countdown timer node.  Fires <see cref="Timeout"/> after
/// <see cref="WaitSeconds"/> have elapsed.
/// </summary>
public sealed class TimerNode : SceneNode
{
    /// <summary>Duration of the timer in seconds.</summary>
    public double WaitSeconds { get; set; } = 1.0;

    /// <summary>When <see langword="true"/> the timer fires once and stops.</summary>
    public bool OneShot { get; set; } = true;

    /// <summary>When <see langword="true"/> the timer starts as soon as the node enters the tree.</summary>
    public bool Autostart { get; set; }

    /// <summary>Fires when the timer reaches zero.</summary>
    public event Action? Timeout;

    private double _elapsed;
    private bool   _running;

    /// <summary>Starts (or restarts) the timer.</summary>
    public void Start() { _elapsed = 0; _running = true; }

    /// <summary>Stops the timer without firing <see cref="Timeout"/>.</summary>
    public void Stop() => _running = false;

    /// <summary><see langword="true"/> while the timer is counting down.</summary>
    public bool IsRunning => _running;

    /// <summary>Time remaining in seconds, or 0 if stopped.</summary>
    public double TimeLeft => _running ? Math.Max(0, WaitSeconds - _elapsed) : 0;

    /// <inheritdoc/>
    protected override void OnReady()
    {
        if (Autostart) Start();
    }

    /// <inheritdoc/>
    protected override void OnUpdate(double deltaSeconds)
    {
        if (!_running) return;
        _elapsed += deltaSeconds;
        if (_elapsed < WaitSeconds) return;

        _running = !OneShot;
        if (!OneShot) _elapsed = 0;
        Timeout?.Invoke();
    }
}

// ── AreaNode ──────────────────────────────────────────────────────────────────

/// <summary>
/// A rectangular trigger area on the grid.
/// The <see cref="SceneTree"/> checks for body overlap after every update tick
/// and fires <see cref="BodyEntered"/> / <see cref="BodyExited"/> as entities
/// enter or leave the area.
/// </summary>
public sealed class AreaNode : SceneNode
{
    /// <summary>Left column of the area (inclusive).</summary>
    public int X { get; set; }

    /// <summary>Top row of the area (inclusive).</summary>
    public int Y { get; set; }

    /// <summary>Width in grid cells.</summary>
    public int Width { get; set; } = 1;

    /// <summary>Height in grid cells.</summary>
    public int Height { get; set; } = 1;

    private readonly HashSet<Guid> _bodiesInside = [];
    private readonly Dictionary<Guid, Entity> _bodyMap = [];

    /// <summary>Fires when a new entity enters the area.</summary>
    public event Action<Entity>? BodyEntered;

    /// <summary>Fires when an entity leaves the area.</summary>
    public event Action<Entity>? BodyExited;

    /// <summary>
    /// Returns <see langword="true"/> if the grid point (<paramref name="x"/>,
    /// <paramref name="y"/>) lies within this area.
    /// </summary>
    public bool ContainsPoint(int x, int y) =>
        x >= X && x < X + Width && y >= Y && y < Y + Height;

    /// <summary>
    /// Should be called from the <see cref="SceneTree"/> after each update
    /// with the current entity list so overlap events are fired correctly.
    /// </summary>
    internal void CheckOverlap(IEnumerable<Entity> entities)
    {
        var nowInside = new HashSet<Guid>();
        foreach (var e in entities)
        {
            if (!ContainsPoint(e.X, e.Y)) continue;
            nowInside.Add(e.Id);
            if (_bodiesInside.Add(e.Id))
            {
                _bodyMap[e.Id] = e;
                BodyEntered?.Invoke(e);
            }
        }
        foreach (var id in _bodiesInside.ToList())
        {
            if (nowInside.Contains(id)) continue;
            _bodiesInside.Remove(id);
            if (_bodyMap.Remove(id, out var exited))
                BodyExited?.Invoke(exited);
        }
    }
}

// ── CameraNode ────────────────────────────────────────────────────────────────

/// <summary>
/// Defines a viewport into the game world.  When a <see cref="CameraNode"/>
/// is marked <see cref="Current"/>, the renderer offsets all grid coordinates
/// so that <see cref="GridNode.GridX"/> / <see cref="GridNode.GridY"/> of the
/// camera maps to the top-left corner of the display.
/// </summary>
public sealed class CameraNode : GridNode
{
    /// <summary>
    /// When <see langword="true"/> this camera is the one used by the renderer.
    /// Only one camera should be current at a time.
    /// </summary>
    public bool Current { get; set; }

    /// <summary>
    /// When <see langword="true"/> the camera smoothly interpolates towards its
    /// target position instead of snapping.
    /// </summary>
    public bool Smoothing { get; set; }

    /// <summary>Interpolation speed when <see cref="Smoothing"/> is enabled.</summary>
    public double SmoothingSpeed { get; set; } = 5.0;

    // Target position for smoothing.
    private double _targetX, _targetY;
    private bool   _hasTarget;

    /// <summary>Smoothly moves the camera towards (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public void LookAt(int x, int y)
    {
        _targetX   = x;
        _targetY   = y;
        _hasTarget = true;
        if (!Smoothing) { GridX = x; GridY = y; }
    }

    /// <inheritdoc/>
    protected override void OnUpdate(double deltaSeconds)
    {
        if (!Smoothing || !_hasTarget) return;
        GridX = (int)Math.Round(GridX + (_targetX - GridX) * SmoothingSpeed * deltaSeconds);
        GridY = (int)Math.Round(GridY + (_targetY - GridY) * SmoothingSpeed * deltaSeconds);
    }
}
