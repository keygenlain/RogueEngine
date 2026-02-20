namespace RogueEngine.Core.Scene;

/// <summary>
/// Serialisable template for a scene.  A <see cref="SceneDefinition"/> stores
/// the node hierarchy as data (type, name, properties, children) so it can be
/// saved to JSON and reinstantiated at runtime via
/// <see cref="SceneTree.Instantiate"/>.
/// </summary>
public sealed class SceneDefinition
{
    /// <summary>Unique identifier for this scene template.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Human-readable name shown in the editor.</summary>
    public string Name { get; set; } = "New Scene";

    /// <summary>Optional description / notes.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The root node template.</summary>
    public SceneNodeTemplate? Root { get; set; }

    /// <summary>
    /// Sprites used exclusively within this scene.
    /// Merged into the global <see cref="SpriteLibrary"/> when instantiated.
    /// </summary>
    public List<SpriteDefinition> Sprites { get; } = [];

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a sprite that belongs to this scene template.
    /// </summary>
    public void AddSprite(SpriteDefinition sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        Sprites.Add(sprite);
    }

    /// <inheritdoc/>
    public override string ToString() => $"[SceneDefinition '{Name}']";
}

/// <summary>
/// Data-only representation of a single node inside a
/// <see cref="SceneDefinition"/>.
/// </summary>
public sealed class SceneNodeTemplate
{
    /// <summary>
    /// Fully-qualified type name used to create the node at instantiation time
    /// (e.g. <c>"SpriteNode"</c>, <c>"EntityNode"</c>, <c>"MapNode"</c>).
    /// </summary>
    public string NodeType { get; set; } = nameof(SceneNode);

    /// <summary>Node name (must be unique among siblings).</summary>
    public string Name { get; set; } = "Node";

    /// <summary>Whether the node is active on creation.</summary>
    public bool Active { get; set; } = true;

    /// <summary>Whether the node is visible on creation.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Arbitrary property bag serialised as key-value strings.
    /// The instantiator maps these to node properties via reflection.
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = [];

    /// <summary>Tags applied to the node.</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>Optional id of an attached <see cref="Models.ScriptGraph"/>.</summary>
    public Guid? AttachedScriptId { get; set; }

    /// <summary>Child node templates.</summary>
    public List<SceneNodeTemplate> Children { get; set; } = [];
}
