namespace RogueEngine.Core.Scene;

/// <summary>
/// Base class for every node in the RogueEngine scene tree.
///
/// <para>
/// The scene tree works like Godot's node system: every running game is a
/// tree of <see cref="SceneNode"/> objects.  A root node owns zero or more
/// children; each child can itself have children, forming a hierarchy of
/// arbitrary depth.
/// </para>
///
/// <para><b>Lifecycle</b></para>
/// <list type="number">
///   <item><see cref="OnReady"/> — called once when the node enters the tree.</item>
///   <item><see cref="OnUpdate"/> — called every game tick with the elapsed seconds.</item>
///   <item><see cref="OnInput"/> — called with the key string on each input event.</item>
///   <item><see cref="OnExit"/> — called when the node is removed from the tree.</item>
/// </list>
///
/// <para>
/// Override the protected <c>On*</c> methods in sub-classes to add behaviour.
/// Alternatively wire a <see cref="ScriptGraph"/> via
/// <see cref="AttachedScriptId"/> so the visual-script executor drives the node.
/// </para>
/// </summary>
public abstract class SceneNode
{
    private readonly List<SceneNode> _children = [];
    private bool _readyCalled;

    // ── Identity ───────────────────────────────────────────────────────────────

    /// <summary>Unique id assigned at creation.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Display name of this node.
    /// Must be unique among the siblings in the same parent to allow
    /// <see cref="GetNode"/> path lookups (e.g. <c>"Player/Sprite"</c>).
    /// </summary>
    public string Name { get; set; } = "Node";

    /// <summary>
    /// Arbitrary string tags that can be queried with
    /// <see cref="SceneTree.FindNodesWithTag{T}"/>.
    /// </summary>
    public HashSet<string> Tags { get; } = [];

    // ── State ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="false"/> this node (and all its children) skips
    /// <see cref="OnUpdate"/> and <see cref="OnInput"/> calls.
    /// </summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// When <see langword="false"/> this node does not emit render data.
    /// The node is still updated unless <see cref="Active"/> is also false.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary><see langword="true"/> once this node is attached to a tree.</summary>
    public bool IsInsideTree { get; private set; }

    // ── Script attachment ──────────────────────────────────────────────────────

    /// <summary>
    /// Optional id of a <see cref="Models.ScriptGraph"/> that drives this node.
    /// When set, the <see cref="SceneTree"/> executor calls the graph's Start,
    /// OnUpdate and OnKeyPress event nodes for this node's lifecycle hooks.
    /// </summary>
    public Guid? AttachedScriptId { get; set; }

    // ── Tree structure ─────────────────────────────────────────────────────────

    /// <summary>Parent node, or <see langword="null"/> for root nodes.</summary>
    public SceneNode? Parent { get; private set; }

    /// <summary>Ordered child list (read-only view).</summary>
    public IReadOnlyList<SceneNode> Children => _children;

    // ── Tree manipulation ──────────────────────────────────────────────────────

    /// <summary>
    /// Appends <paramref name="child"/> as the last child of this node and
    /// triggers <see cref="OnReady"/> if the tree is already running.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="child"/> already has a parent.
    /// </exception>
    public void AddChild(SceneNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.Parent is not null)
            throw new InvalidOperationException(
                $"Node '{child.Name}' already has a parent '{child.Parent.Name}'.");

        child.Parent = this;
        _children.Add(child);

        if (IsInsideTree)
            child.EnterTree();
    }

    /// <summary>
    /// Removes <paramref name="child"/> from this node's child list
    /// and triggers <see cref="OnExit"/>.
    /// </summary>
    public bool RemoveChild(SceneNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (!_children.Remove(child)) return false;
        child.ExitTree();
        child.Parent = null;
        return true;
    }

    /// <summary>
    /// Removes this node from its parent.
    /// No-op if this node has no parent.
    /// </summary>
    public void QueueFree() => Parent?.RemoveChild(this);

    /// <summary>
    /// Finds a direct child by name.  Returns <see langword="null"/> if not found.
    /// </summary>
    public SceneNode? GetChild(string name) =>
        _children.FirstOrDefault(c => c.Name == name);

    /// <summary>
    /// Finds a direct child of type <typeparamref name="T"/> by name.
    /// </summary>
    public T? GetChild<T>(string name) where T : SceneNode =>
        GetChild(name) as T;

    /// <summary>
    /// Traverses a slash-separated path from this node
    /// (e.g. <c>"Player/Sprite"</c> or <c>"../HUD"</c>).
    /// <c>".."</c> steps to the parent.
    /// Returns <see langword="null"/> if any segment is not found.
    /// </summary>
    public SceneNode? GetNode(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return this;
        var parts  = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        SceneNode? cur = this;
        foreach (var part in parts)
        {
            if (cur is null) return null;
            cur = part == ".." ? cur.Parent : cur.GetChild(part);
        }
        return cur;
    }

    /// <summary>
    /// Traverses a path and casts the result to <typeparamref name="T"/>.
    /// </summary>
    public T? GetNode<T>(string path) where T : SceneNode =>
        GetNode(path) as T;

    /// <summary>
    /// Returns the first descendant of type <typeparamref name="T"/> found by
    /// depth-first search, or <see langword="null"/>.
    /// </summary>
    public T? FindChild<T>() where T : SceneNode
    {
        foreach (var c in _children)
        {
            if (c is T match) return match;
            var deep = c.FindChild<T>();
            if (deep is not null) return deep;
        }
        return null;
    }

    /// <summary>
    /// Returns all descendants of type <typeparamref name="T"/>.
    /// </summary>
    public IEnumerable<T> FindChildren<T>() where T : SceneNode
    {
        foreach (var c in _children)
        {
            if (c is T match) yield return match;
            foreach (var deep in c.FindChildren<T>())
                yield return deep;
        }
    }

    /// <summary>
    /// Absolute path from the root to this node (e.g. <c>"root/Level/Player"</c>).
    /// </summary>
    public string GetPath()
    {
        var parts = new List<string>();
        SceneNode? cur = this;
        while (cur is not null) { parts.Add(cur.Name); cur = cur.Parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }

    // ── Lifecycle hooks (override in sub-classes) ──────────────────────────────

    /// <summary>Called once when this node (and all its children) enter the tree.</summary>
    protected virtual void OnReady() { }

    /// <summary>
    /// Called every game tick.
    /// <paramref name="deltaSeconds"/> is the wall-clock time since the last tick.
    /// </summary>
    protected virtual void OnUpdate(double deltaSeconds) { }

    /// <summary>
    /// Called on each input event before propagating to children.
    /// Return <see langword="true"/> to consume the event (stop propagation).
    /// </summary>
    protected virtual bool OnInput(string inputKey) => false;

    /// <summary>Called when this node is removed from the tree.</summary>
    protected virtual void OnExit() { }

    // ── Internal tree propagation (called by SceneTree) ───────────────────────

    internal void EnterTree()
    {
        IsInsideTree = true;
        if (!_readyCalled) { _readyCalled = true; OnReady(); }
        foreach (var c in _children.ToList())
            c.EnterTree();
    }

    internal void ExitTree()
    {
        foreach (var c in _children.ToList())
            c.ExitTree();
        OnExit();
        IsInsideTree = false;
    }

    internal void PropagateUpdate(double delta)
    {
        if (!Active) return;
        OnUpdate(delta);
        foreach (var c in _children.ToList())
            c.PropagateUpdate(delta);
    }

    internal bool PropagateInput(string key)
    {
        if (!Active) return false;
        if (OnInput(key)) return true;
        foreach (var c in _children.ToList())
            if (c.PropagateInput(key)) return true;
        return false;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"[{GetType().Name}] {Name} ({_children.Count} children)";
}
