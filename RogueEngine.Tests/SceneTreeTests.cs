using RogueEngine.Core.Models;
using RogueEngine.Core.Scene;

namespace RogueEngine.Tests;

/// <summary>
/// Unit tests for the scene tree (<see cref="SceneTree"/>),
/// <see cref="SceneNode"/> hierarchy, and built-in node types.
/// </summary>
public sealed class SceneTreeTests
{
    // ── Tree construction ──────────────────────────────────────────────────────

    [Fact]
    public void NewTree_HasRootNode()
    {
        var tree = new SceneTree();
        Assert.NotNull(tree.Root);
        Assert.Equal("root", tree.Root.Name);
    }

    [Fact]
    public void AddChild_AttachesNode()
    {
        var parent = new GridNode { Name = "Parent" };
        var child  = new GridNode { Name = "Child"  };
        parent.AddChild(child);

        Assert.Single(parent.Children);
        Assert.Same(parent, child.Parent);
    }

    [Fact]
    public void AddChild_ThrowsIfAlreadyHasParent()
    {
        var p1 = new GridNode { Name = "P1" };
        var p2 = new GridNode { Name = "P2" };
        var ch = new GridNode { Name = "Ch" };
        p1.AddChild(ch);

        Assert.Throws<InvalidOperationException>(() => p2.AddChild(ch));
    }

    [Fact]
    public void RemoveChild_DetachesNode()
    {
        var parent = new GridNode { Name = "Parent" };
        var child  = new GridNode { Name = "Child"  };
        parent.AddChild(child);
        parent.RemoveChild(child);

        Assert.Empty(parent.Children);
        Assert.Null(child.Parent);
    }

    [Fact]
    public void QueueFree_RemovesFromParent()
    {
        var parent = new GridNode { Name = "P" };
        var child  = new GridNode { Name = "C" };
        parent.AddChild(child);
        child.QueueFree();

        Assert.Empty(parent.Children);
    }

    // ── Path navigation ────────────────────────────────────────────────────────

    [Fact]
    public void GetChild_ByName_ReturnsCorrectChild()
    {
        var parent = new GridNode { Name = "P" };
        var a      = new GridNode { Name = "A" };
        var b      = new GridNode { Name = "B" };
        parent.AddChild(a);
        parent.AddChild(b);

        Assert.Same(a, parent.GetChild("A"));
        Assert.Same(b, parent.GetChild("B"));
        Assert.Null(parent.GetChild("C"));
    }

    [Fact]
    public void GetNode_SlashPath_Navigates()
    {
        var root   = new GridNode { Name = "root" };
        var player = new GridNode { Name = "Player" };
        var sprite = new SpriteNode { Name = "Sprite" };
        root.AddChild(player);
        player.AddChild(sprite);

        Assert.Same(sprite, root.GetNode("Player/Sprite"));
    }

    [Fact]
    public void GetNode_DotDot_GoesToParent()
    {
        var root   = new GridNode { Name = "root" };
        var player = new GridNode { Name = "Player" };
        root.AddChild(player);

        Assert.Same(root, player.GetNode(".."));
    }

    [Fact]
    public void GetPath_ReturnsFullPath()
    {
        var root   = new GridNode { Name = "root" };
        var level  = new GridNode { Name = "Level" };
        var player = new GridNode { Name = "Player" };
        root.AddChild(level);
        level.AddChild(player);

        Assert.Equal("root/Level/Player", player.GetPath());
    }

    // ── FindChild / FindChildren ───────────────────────────────────────────────

    [Fact]
    public void FindChild_ReturnsFirstOfType()
    {
        var root  = new GridNode { Name = "root" };
        var timer = new TimerNode { Name = "T" };
        root.AddChild(new GridNode { Name = "A" });
        root.AddChild(timer);

        Assert.Same(timer, root.FindChild<TimerNode>());
    }

    [Fact]
    public void FindChildren_ReturnsAllOfType()
    {
        var root = new GridNode { Name = "root" };
        root.AddChild(new SpriteNode { Name = "S1" });
        root.AddChild(new GridNode   { Name = "G"  });
        root.AddChild(new SpriteNode { Name = "S2" });

        var sprites = root.FindChildren<SpriteNode>().ToList();
        Assert.Equal(2, sprites.Count);
    }

    // ── Tags ───────────────────────────────────────────────────────────────────

    [Fact]
    public void FindNodesWithTag_FiltersCorrectly()
    {
        var tree = new SceneTree();
        var root = new GridNode { Name = "scene" };
        var a = new GridNode { Name = "A" };
        var b = new GridNode { Name = "B" };
        a.Tags.Add("enemy");
        b.Tags.Add("player");
        root.AddChild(a);
        root.AddChild(b);
        tree.AddScene("test", root);
        tree.ChangeScene("test");

        var enemies = tree.FindNodesWithTag<GridNode>("enemy").ToList();
        Assert.Single(enemies);
        Assert.Equal("A", enemies[0].Name);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [Fact]
    public void OnReady_CalledWhenNodeEntersTree()
    {
        var tree = new SceneTree();
        var counter = new CounterNode { Name = "c" };
        tree.Root.AddChild(counter);

        Assert.Equal(1, counter.ReadyCount);
    }

    [Fact]
    public void OnUpdate_CalledEachTick()
    {
        var tree    = new SceneTree();
        var counter = new CounterNode { Name = "c" };
        tree.Root.AddChild(counter);

        tree.Update(0.016);
        tree.Update(0.016);

        Assert.Equal(2, counter.UpdateCount);
    }

    [Fact]
    public void OnExit_CalledWhenRemovedFromTree()
    {
        var tree    = new SceneTree();
        var counter = new CounterNode { Name = "c" };
        tree.Root.AddChild(counter);
        tree.Root.RemoveChild(counter);

        Assert.Equal(1, counter.ExitCount);
    }

    [Fact]
    public void InactiveNode_SkipsUpdate()
    {
        var tree    = new SceneTree();
        var counter = new CounterNode { Name = "c", Active = false };
        tree.Root.AddChild(counter);

        tree.Update(0.016);
        Assert.Equal(0, counter.UpdateCount);
    }

    // ── Input propagation ──────────────────────────────────────────────────────

    [Fact]
    public void Input_PropagatesDepthFirst()
    {
        var tree    = new SceneTree();
        var handler = new InputCaptureNode { Name = "ih" };
        tree.Root.AddChild(handler);

        tree.Input("ArrowUp");
        Assert.Equal("ArrowUp", handler.LastKey);
    }

    [Fact]
    public void Input_ConsumedByFirstHandler_StopsPropagation()
    {
        var tree = new SceneTree();
        var h1   = new ConsumingInputNode { Name = "h1" };
        var h2   = new InputCaptureNode   { Name = "h2" };
        tree.Root.AddChild(h1);
        tree.Root.AddChild(h2);

        tree.Input("x");
        // h2 should NOT receive the key because h1 consumed it.
        Assert.Null(h2.LastKey);
    }

    // ── Scene change ──────────────────────────────────────────────────────────

    [Fact]
    public void ChangeScene_SetsActiveSceneName()
    {
        var tree = new SceneTree();
        tree.AddScene("level1", new GridNode { Name = "level1" });
        tree.ChangeScene("level1");

        Assert.Equal("level1", tree.ActiveSceneName);
    }

    [Fact]
    public void ChangeScene_UnknownName_Throws()
    {
        var tree = new SceneTree();
        Assert.Throws<KeyNotFoundException>(() => tree.ChangeScene("nonexistent"));
    }

    [Fact]
    public void ChangeScene_OldSceneGetsExit()
    {
        var tree = new SceneTree();
        var counter1 = new CounterNode { Name = "scene1" };
        var counter2 = new GridNode    { Name = "scene2" };
        tree.AddScene("s1", counter1);
        tree.AddScene("s2", counter2);

        tree.ChangeScene("s1");
        tree.ChangeScene("s2");

        Assert.Equal(1, counter1.ExitCount);
    }

    // ── Scene instantiation from definition ───────────────────────────────────

    [Fact]
    public void Instantiate_CreatesNodeHierarchy()
    {
        var def = new SceneDefinition { Name = "test" };
        def.Root = new SceneNodeTemplate
        {
            NodeType = nameof(GridNode),
            Name = "Parent",
            Children =
            [
                new SceneNodeTemplate { NodeType = nameof(SpriteNode), Name = "Sprite" },
                new SceneNodeTemplate { NodeType = nameof(LabelNode),  Name = "Label"  },
            ],
        };

        var tree = new SceneTree();
        var root = tree.Instantiate(def);

        Assert.Equal("Parent", root.Name);
        Assert.Equal(2, root.Children.Count);
        Assert.IsType<SpriteNode>(root.Children[0]);
        Assert.IsType<LabelNode> (root.Children[1]);
    }

    [Fact]
    public void Instantiate_AppliesProperties()
    {
        var def = new SceneDefinition { Name = "test" };
        def.Root = new SceneNodeTemplate
        {
            NodeType = nameof(LabelNode),
            Name     = "Lbl",
            Properties = { ["Text"] = "Hello", ["GridX"] = "5", ["GridY"] = "10" },
        };

        var tree = new SceneTree();
        var root = (LabelNode)tree.Instantiate(def);

        Assert.Equal("Hello", root.Text);
        Assert.Equal(5,       root.GridX);
        Assert.Equal(10,      root.GridY);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Fact]
    public void Render_EmptyScene_ReturnsBlankMap()
    {
        var tree = new SceneTree();
        var map  = tree.Render(40, 10);

        Assert.Equal(40, map.Width);
        Assert.Equal(10, map.Height);
        Assert.Equal(' ', map[0, 0].Character);
    }

    [Fact]
    public void Render_LabelNode_WritesTextToCells()
    {
        var tree  = new SceneTree();
        var scene = new GridNode { Name = "scene" };
        var label = new LabelNode
        {
            Name  = "lbl",
            Text  = "Hi",
            GridX = 2,
            GridY = 1,
        };
        scene.AddChild(label);
        tree.AddScene("s", scene);
        tree.ChangeScene("s");

        var map = tree.Render(20, 5);

        Assert.Equal('H', map[2, 1].Character);
        Assert.Equal('i', map[3, 1].Character);
    }

    [Fact]
    public void Render_SpriteNode_UsesLibraryGlyph()
    {
        var tree   = new SceneTree();
        var scene  = new GridNode  { Name = "scene" };
        var sprite = new SpriteNode
        {
            Name       = "sp",
            SpriteName = "player",
            GridX      = 5,
            GridY      = 3,
        };
        scene.AddChild(sprite);
        tree.AddScene("s", scene);
        tree.ChangeScene("s");

        var map = tree.Render(20, 10);
        Assert.Equal('@', map[5, 3].Character);
    }

    [Fact]
    public void Render_MapNode_BlitsMapLayer()
    {
        var tree   = new SceneTree();
        var scene  = new GridNode { Name = "scene" };
        var ascii  = new AsciiMap(5, 5);
        ascii[2, 2] = new AsciiCell { Character = '#' };

        var mapNode = new MapNode { Name = "map", Map = ascii };
        scene.AddChild(mapNode);
        tree.AddScene("s", scene);
        tree.ChangeScene("s");

        var rendered = tree.Render(10, 10);
        Assert.Equal('#', rendered[2, 2].Character);
    }

    [Fact]
    public void Render_CameraOffset_ShiftsCoordinates()
    {
        var tree   = new SceneTree();
        var scene  = new GridNode  { Name = "scene" };
        var sprite = new SpriteNode
        {
            Name = "sp", SpriteName = "player",
            GridX = 10, GridY = 5,
        };
        scene.AddChild(sprite);
        tree.AddScene("s", scene);
        tree.ChangeScene("s");

        // Camera at (8, 3) → player should appear at (10-8, 5-3) = (2, 2)
        var map = tree.Render(20, 10, cameraX: 8, cameraY: 3);
        Assert.Equal('@', map[2, 2].Character);
    }

    // ── TimerNode ─────────────────────────────────────────────────────────────

    [Fact]
    public void TimerNode_Fires_AfterWaitSeconds()
    {
        var tree  = new SceneTree();
        var scene = new GridNode { Name = "scene" };
        var timer = new TimerNode { Name = "t", WaitSeconds = 1.0, Autostart = true };
        var fired = false;
        timer.Timeout += () => fired = true;
        scene.AddChild(timer);
        tree.AddScene("s", scene);
        tree.ChangeScene("s");

        tree.Update(0.5);
        Assert.False(fired);
        tree.Update(0.6);
        Assert.True(fired);
    }

    [Fact]
    public void TimerNode_OneShot_DoesNotRefire()
    {
        var tree  = new SceneTree();
        var scene = new GridNode { Name = "scene" };
        var timer = new TimerNode
            { Name = "t", WaitSeconds = 0.5, OneShot = true, Autostart = true };
        var count = 0;
        timer.Timeout += () => count++;
        scene.AddChild(timer);
        tree.AddScene("s", scene);
        tree.ChangeScene("s");

        tree.Update(0.6);
        tree.Update(0.6);
        Assert.Equal(1, count);
    }

    // ── AreaNode ──────────────────────────────────────────────────────────────

    [Fact]
    public void AreaNode_ContainsPoint_True()
    {
        var area = new AreaNode { X = 2, Y = 2, Width = 3, Height = 3 };
        Assert.True(area.ContainsPoint(3, 3));
        Assert.True(area.ContainsPoint(2, 2));
        Assert.True(area.ContainsPoint(4, 4));
    }

    [Fact]
    public void AreaNode_ContainsPoint_False()
    {
        var area = new AreaNode { X = 2, Y = 2, Width = 3, Height = 3 };
        Assert.False(area.ContainsPoint(1, 1));
        Assert.False(area.ContainsPoint(5, 5));
    }

    [Fact]
    public void AreaNode_BodyEntered_FiresWhenEntityEnters()
    {
        var tree  = new SceneTree();
        var scene = new GridNode { Name = "scene" };
        var area  = new AreaNode { Name = "a", X = 0, Y = 0, Width = 5, Height = 5 };
        Entity? entered = null;
        area.BodyEntered += e => entered = e;
        scene.AddChild(area);
        tree.AddScene("s", scene);
        tree.ChangeScene("s");

        var entity = new Entity { X = 2, Y = 2 };
        tree.Update(0.016, [entity]);

        Assert.NotNull(entered);
        Assert.Same(entity, entered);
    }

    [Fact]
    public void AreaNode_BodyExited_PassesActualEntityReference()
    {
        var tree  = new SceneTree();
        var scene = new GridNode { Name = "scene" };
        var area  = new AreaNode { Name = "a", X = 0, Y = 0, Width = 3, Height = 3 };
        Entity? exited = null;
        area.BodyExited += e => exited = e;
        scene.AddChild(area);
        tree.AddScene("s", scene);
        tree.ChangeScene("s");

        var entity = new Entity { X = 1, Y = 1 };

        // Tick 1: entity enters
        tree.Update(0.016, [entity]);
        Assert.Null(exited);

        // Tick 2: entity moves outside
        entity.X = 10;
        tree.Update(0.016, [entity]);

        Assert.NotNull(exited);
        Assert.Same(entity, exited);    // must receive the actual reference
    }

    // ── CameraNode ────────────────────────────────────────────────────────────

    [Fact]
    public void CameraNode_Current_OverridesRenderOffset()
    {
        var tree   = new SceneTree();
        var scene  = new GridNode  { Name = "scene" };
        var cam    = new CameraNode { Name = "cam", GridX = 5, GridY = 5, Current = true };
        var sprite = new SpriteNode { Name = "sp", SpriteName = "player", GridX = 7, GridY = 7 };
        scene.AddChild(cam);
        scene.AddChild(sprite);
        tree.AddScene("s", scene);
        tree.ChangeScene("s");

        // With camera at (5,5), sprite at (7,7) should appear at (2,2).
        var map = tree.Render(20, 20);
        Assert.Equal('@', map[2, 2].Character);
    }

    // ── EntityNode sync ───────────────────────────────────────────────────────

    [Fact]
    public void EntityNode_SyncsPositionToEntity()
    {
        var tree   = new SceneTree();
        var entity = new Entity { X = 3, Y = 4 };
        var eNode  = new EntityNode { Name = "player", Entity = entity };
        tree.Root.AddChild(eNode);

        eNode.GridX = 10;
        eNode.GridY = 12;
        tree.Update(0.016);

        Assert.Equal(10, entity.X);
        Assert.Equal(12, entity.Y);
    }

    // ── Helper test nodes ──────────────────────────────────────────────────────

    private sealed class CounterNode : SceneNode
    {
        public int ReadyCount  { get; private set; }
        public int UpdateCount { get; private set; }
        public int ExitCount   { get; private set; }
        protected override void OnReady()                  => ReadyCount++;
        protected override void OnUpdate(double _)         => UpdateCount++;
        protected override void OnExit()                   => ExitCount++;
    }

    private sealed class InputCaptureNode : SceneNode
    {
        public string? LastKey { get; private set; }
        protected override bool OnInput(string key) { LastKey = key; return false; }
    }

    private sealed class ConsumingInputNode : SceneNode
    {
        protected override bool OnInput(string _) => true; // consume
    }
}
