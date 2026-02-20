using RogueEngine.Core.Models;

namespace RogueEngine.Core.Scene;

/// <summary>
/// The scene tree manages the active hierarchy of <see cref="SceneNode"/>
/// objects, drives their lifecycle, and composites the final render buffer.
///
/// <para>
/// <b>Usage pattern</b>:
/// <code>
/// var tree = new SceneTree(library);
/// tree.AddScene("main", mainSceneRoot);
/// tree.ChangeScene("main");
/// // each tick:
/// tree.Update(delta);
/// tree.Input("ArrowRight");
/// var cells = tree.Render(mapWidth, mapHeight);
/// </code>
/// </para>
///
/// <para>
/// <b>Scene change</b>: calling <see cref="ChangeScene"/> replaces the current
/// root with the new scene's root, triggering <see cref="SceneNode.OnExit"/>
/// on the old tree and <see cref="SceneNode.OnReady"/> on the new one.
/// </para>
/// </summary>
public sealed class SceneTree
{
    private readonly Dictionary<string, SceneNode>       _scenes     = [];
    private readonly Dictionary<string, SceneDefinition> _definitions = [];

    // ── Construction ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new scene tree.
    /// </summary>
    /// <param name="library">
    /// Sprite library used to resolve sprite names for <see cref="SpriteNode"/>s.
    /// A default library is created if <see langword="null"/> is passed.
    /// </param>
    public SceneTree(SpriteLibrary? library = null)
    {
        Library = library ?? new SpriteLibrary();
        Root    = new RootNode();
        Root.EnterTree();
    }

    // ── Properties ─────────────────────────────────────────────────────────────

    /// <summary>Sprite library shared by all nodes in this tree.</summary>
    public SpriteLibrary Library { get; }

    /// <summary>
    /// The invisible root node that owns all top-level scene roots.
    /// Do not remove this node.
    /// </summary>
    public SceneNode Root { get; }

    /// <summary>Name of the currently active scene (or empty).</summary>
    public string ActiveSceneName { get; private set; } = string.Empty;

    /// <summary>The root node of the currently active scene.</summary>
    public SceneNode? ActiveScene { get; private set; }

    // ── Scene registration ─────────────────────────────────────────────────────

    /// <summary>
    /// Registers a pre-built node hierarchy as a named scene.
    /// The root is not added to the tree until <see cref="ChangeScene"/> is called.
    /// </summary>
    public void AddScene(string name, SceneNode sceneRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(sceneRoot);
        _scenes[name] = sceneRoot;
    }

    /// <summary>
    /// Registers a <see cref="SceneDefinition"/> so it can be instantiated
    /// later by name with <see cref="ChangeScene"/> or <see cref="Instantiate"/>.
    /// </summary>
    public void RegisterDefinition(SceneDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        _definitions[def.Name] = def;
    }

    // ── Scene transitions ──────────────────────────────────────────────────────

    /// <summary>
    /// Switches to the named scene.  The previous active scene is detached from
    /// the tree and its <see cref="SceneNode.OnExit"/> lifecycle fires.
    /// </summary>
    /// <param name="name">Name passed to <see cref="AddScene"/> or a registered definition.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the name is not registered.</exception>
    public void ChangeScene(string name)
    {
        // Detach old scene.
        if (ActiveScene is not null)
            Root.RemoveChild(ActiveScene);

        // Resolve new scene root.
        SceneNode newRoot;
        if (_scenes.TryGetValue(name, out var existing))
        {
            newRoot = existing;
        }
        else if (_definitions.TryGetValue(name, out var def))
        {
            newRoot = Instantiate(def);
        }
        else
        {
            throw new KeyNotFoundException($"No scene named '{name}' is registered.");
        }

        Root.AddChild(newRoot);
        ActiveScene     = newRoot;
        ActiveSceneName = name;

        // Resolve sprites for all SpriteNodes in the new scene.
        ResolveSpriteNodes(newRoot);
    }

    // ── Instantiation from definition ──────────────────────────────────────────

    /// <summary>
    /// Instantiates a <see cref="SceneDefinition"/> into a live node hierarchy.
    /// Each node's properties are applied from
    /// <see cref="SceneNodeTemplate.Properties"/> via reflection.
    /// </summary>
    public SceneNode Instantiate(SceneDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);

        // Merge scene-local sprites into the library.
        foreach (var s in def.Sprites) Library.Register(s);

        if (def.Root is null)
            return new RootNode { Name = def.Name };

        return InstantiateTemplate(def.Root);
    }

    private static SceneNode InstantiateTemplate(SceneNodeTemplate template)
    {
        var node = CreateNodeByType(template.NodeType);
        node.Name             = template.Name;
        node.Active           = template.Active;
        node.Visible          = template.Visible;
        node.AttachedScriptId = template.AttachedScriptId;
        foreach (var tag in template.Tags) node.Tags.Add(tag);
        ApplyProperties(node, template.Properties);
        foreach (var child in template.Children)
            node.AddChild(InstantiateTemplate(child));
        return node;
    }

    private static SceneNode CreateNodeByType(string typeName) => typeName switch
    {
        nameof(SpriteNode)   => new SpriteNode(),
        nameof(EntityNode)   => new EntityNode(),
        nameof(MapNode)      => new MapNode(),
        nameof(LabelNode)    => new LabelNode(),
        nameof(TimerNode)    => new TimerNode(),
        nameof(AreaNode)     => new AreaNode(),
        nameof(CameraNode)   => new CameraNode(),
        nameof(GridNode)     => new GridNode(),
        _                    => new RootNode(),
    };

    private static void ApplyProperties(SceneNode node, Dictionary<string, string> props)
    {
        var type = node.GetType();
        foreach (var (key, value) in props)
        {
            var prop = type.GetProperty(key);
            if (prop is null || !prop.CanWrite) continue;
            try
            {
                var converted = Convert.ChangeType(value, prop.PropertyType);
                prop.SetValue(node, converted);
            }
            catch { /* skip un-convertible properties */ }
        }
    }

    // ── Lifecycle propagation ──────────────────────────────────────────────────

    /// <summary>
    /// Advances all nodes by <paramref name="deltaSeconds"/> (wall clock seconds).
    /// Also updates area overlap checks against the given <paramref name="entities"/>.
    /// </summary>
    public void Update(double deltaSeconds, IEnumerable<Entity>? entities = null)
    {
        Root.PropagateUpdate(deltaSeconds);

        // Fire area overlap events.
        if (entities is null) return;
        var entityList = entities as IList<Entity> ?? entities.ToList();
        foreach (var area in FindNodes<AreaNode>())
            area.CheckOverlap(entityList);
    }

    /// <summary>
    /// Propagates an input event through the tree depth-first.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if a node consumed the event (stopped propagation).
    /// </returns>
    public bool Input(string inputKey) =>
        Root.PropagateInput(inputKey);

    // ── Rendering ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Composites the scene tree into an <see cref="AsciiMap"/> of
    /// (<paramref name="width"/> × <paramref name="height"/>) cells.
    ///
    /// <para>
    /// Rendering order:
    /// <list type="number">
    ///   <item><see cref="MapNode"/> tiles (lowest layer).</item>
    ///   <item><see cref="SpriteNode"/> and <see cref="EntityNode"/> glyphs,
    ///         ordered by <see cref="GridNode.ZIndex"/>.</item>
    ///   <item><see cref="LabelNode"/> text (highest layer).</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="width">Display width in characters.</param>
    /// <param name="height">Display height in characters.</param>
    /// <param name="cameraX">Column offset applied to all grid coordinates (camera scroll).</param>
    /// <param name="cameraY">Row offset applied to all grid coordinates.</param>
    /// <returns>A new <see cref="AsciiMap"/> ready for display.</returns>
    public AsciiMap Render(int width, int height, int cameraX = 0, int cameraY = 0)
    {
        var map = new AsciiMap(Math.Max(1, width), Math.Max(1, height));
        if (ActiveScene is null) return map;

        // Find active camera override.
        var cam = FindNodes<CameraNode>().FirstOrDefault(c => c.Current && c.Active);
        var offX = cam?.GridX ?? cameraX;
        var offY = cam?.GridY ?? cameraY;

        // 1. Map nodes.
        foreach (var mn in FindNodes<MapNode>().Where(n => n.Visible))
            BlitMap(mn, map, offX, offY);

        // 2. Grid nodes (sprites + entities) in Z order.
        var gridNodes = FindNodes<GridNode>()
            .Where(n => n.Visible)
            .OrderBy(n => n.ZIndex);

        foreach (var gn in gridNodes)
        {
            var sx = gn.GridX - offX;
            var sy = gn.GridY - offY;
            if (!map.IsInBounds(sx, sy)) continue;

            switch (gn)
            {
                case SpriteNode sn:
                {
                    var spriteName = sn.CurrentFrameName;
                    var sprite     = Library.Resolve(spriteName);
                    map[sx, sy] = new AsciiCell
                    {
                        Character       = sprite.Glyph,
                        ForegroundColor = sprite.ForegroundColor,
                        BackgroundColor = sprite.BackgroundColor,
                        // Store the sprite name for graphical rendering by the WPF layer.
                        SpriteName      = sprite.HasGraphic ? sprite.Name : null,
                    };
                    break;
                }
                case EntityNode en when en.Entity is not null:
                {
                    map[sx, sy] = new AsciiCell
                    {
                        Character       = en.Entity.Glyph,
                        ForegroundColor = en.Entity.ForegroundColor,
                    };
                    break;
                }
            }
        }

        // 3. Labels on top.
        foreach (var ln in FindNodes<LabelNode>().Where(n => n.Visible))
        {
            var text = ln.Text;
            for (var i = 0; i < text.Length; i++)
            {
                var sx = ln.GridX + i - offX;
                var sy = ln.GridY - offY;
                if (!map.IsInBounds(sx, sy)) break;
                map[sx, sy] = new AsciiCell
                {
                    Character       = text[i],
                    ForegroundColor = ln.ForegroundColor,
                    BackgroundColor = ln.BackgroundColor,
                };
            }
        }

        return map;
    }

    // ── Node querying ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all nodes of type <typeparamref name="T"/> anywhere in the tree.
    /// </summary>
    public IEnumerable<T> FindNodes<T>() where T : SceneNode =>
        Root.FindChildren<T>();

    /// <summary>
    /// Returns the first node named <paramref name="name"/> of type
    /// <typeparamref name="T"/>, or <see langword="null"/>.
    /// </summary>
    public T? FindNode<T>(string name) where T : SceneNode =>
        Root.FindChildren<T>().FirstOrDefault(n => n.Name == name);

    /// <summary>
    /// Returns all nodes that have the given <paramref name="tag"/>.
    /// </summary>
    public IEnumerable<T> FindNodesWithTag<T>(string tag) where T : SceneNode =>
        Root.FindChildren<T>().Where(n => n.Tags.Contains(tag));

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void BlitMap(MapNode mn, AsciiMap dest, int offX, int offY)
    {
        if (mn.Map is null) return;
        var map = mn.Map;
        for (var x = 0; x < map.Width; x++)
        for (var y = 0; y < map.Height; y++)
        {
            var dx = x + mn.OffsetX - offX;
            var dy = y + mn.OffsetY - offY;
            if (!dest.IsInBounds(dx, dy)) continue;
            dest[dx, dy] = map[x, y];
        }
    }

    private void ResolveSpriteNodes(SceneNode root)
    {
        foreach (var sn in root.FindChildren<SpriteNode>())
            sn.Sprite = Library.Get(sn.SpriteName);
    }

    // ── Inner types ────────────────────────────────────────────────────────────

    private sealed class RootNode : SceneNode
    {
        public RootNode() { Name = "root"; }
    }
}
