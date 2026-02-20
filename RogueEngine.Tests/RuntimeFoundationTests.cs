using RogueEngine.Core.Runtime;
using RogueEngine.Core.Runtime.Content;
using RogueEngine.Core.Runtime.Ecs;
using RogueEngine.Core.Runtime.Input;
using RogueEngine.Core.Runtime.Platform;
using RogueEngine.Core.Runtime.Rendering;
using RogueEngine.Core.Engine;
using RogueEngine.Core.Models;

namespace RogueEngine.Tests;

public sealed class RuntimeFoundationTests
{
    [Fact]
    public void InputActionMap_BindsAndResolvesPressedStates()
    {
        var map = new InputActionMap();
        map.Bind("MoveUp", "W", "ArrowUp");

        map.Update(new InputSnapshot(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "W" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "W" },
            new HashSet<string>()));

        Assert.True(map.IsPressed("MoveUp"));
        Assert.True(map.IsJustPressed("MoveUp"));
        Assert.False(map.IsJustReleased("MoveUp"));
    }

    [Fact]
    public void World_MovementSystem_UpdatesPosition()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.Set(entity, new Position(2, 3));
        world.Set(entity, new Velocity(1, -1));

        var system = new MovementSystem();
        system.Update(world, new FrameTime(1d / 60d, 0.016, 1));

        Assert.True(world.TryGet<Position>(entity, out var pos));
        Assert.Equal(3, pos.X);
        Assert.Equal(2, pos.Y);
    }

    [Fact]
    public void AssetDatabase_SerializesAndDeserializes()
    {
        var db = new AssetDatabase();
        var asset = db.RegisterOrUpdate(
            logicalPath: "sprites/player",
            sourcePath: "Assets/Sprites/player.png",
            importer: "SpriteImporter",
            fingerprint: "abc123");

        var json = db.Serialize();
        var roundTrip = AssetDatabase.Deserialize(json);

        Assert.True(roundTrip.TryGetByPath("sprites/player", out var loaded));
        Assert.NotNull(loaded);
        Assert.Equal(asset.Id, loaded!.Id);
        Assert.Equal("SpriteImporter", loaded.Importer);
    }

    [Fact]
    public void EngineLoop_RunFrames_CallsRuntimeHooks()
    {
        var runtime = new TestRuntime();
        var loop = new EngineLoop(runtime, new DefaultEnginePlatform(), new EngineLoopOptions { FixedUpdateHz = 60 });

        loop.RunFrames(3);

        Assert.Equal(1, runtime.InitializeCount);
        Assert.Equal(3, runtime.FixedUpdateCount);
        Assert.Equal(3, runtime.RenderCount);
        Assert.Equal(1, runtime.ShutdownCount);
    }

    [Fact]
    public void RuntimeGameHost_RunsScriptGraph_OnFixedUpdate_AndRendersMap()
    {
        var tick = NodeFactory.Create(NodeType.OnTick);
        var createMap = NodeFactory.Create(NodeType.CreateMap);
        createMap.Properties["Width"] = "12";
        createMap.Properties["Height"] = "6";

        var graph = new ScriptGraph();
        graph.AddNode(tick);
        graph.AddNode(createMap);
        graph.Connect(
            tick.Id,
            tick.Outputs.First(p => p.Name == "Exec").Id,
            createMap.Id,
            createMap.Inputs.First(p => p.Name == "Exec").Id);

        var renderer = new TestRenderer();
        var host = new RuntimeGameHost(renderer, graph);
        var loop = new EngineLoop(host, new DefaultEnginePlatform(), new EngineLoopOptions { FixedUpdateHz = 30 });

        loop.RunFrames(1);

        Assert.True(renderer.MapDrawCount > 0);
        Assert.NotNull(renderer.LastMap);
        Assert.Equal(12, renderer.LastMap!.Width);
        Assert.Equal(6, renderer.LastMap.Height);
    }

    [Fact]
    public void RuntimeGameHost_UsesActionMap_ToTriggerOnKeyPressChains()
    {
        var onKey = NodeFactory.Create(NodeType.OnKeyPress);
        var createMap = NodeFactory.Create(NodeType.CreateMap);
        createMap.Properties["Width"] = "9";
        createMap.Properties["Height"] = "4";

        var graph = new ScriptGraph();
        graph.AddNode(onKey);
        graph.AddNode(createMap);
        graph.Connect(
            onKey.Id,
            onKey.Outputs.First(p => p.Name == "Exec").Id,
            createMap.Id,
            createMap.Inputs.First(p => p.Name == "Exec").Id);

        var actionMap = new InputActionMap();
        actionMap.Bind("Confirm", "Enter");

        var input = new SequenceInputSource(
            new InputSnapshot(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Enter" },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Enter" },
                new HashSet<string>()));

        var renderer = new TestRenderer();
        var host = new RuntimeGameHost(renderer, graph);
        var platform = new DefaultEnginePlatform(input: input);
        var loop = new EngineLoop(host, platform, new EngineLoopOptions { FixedUpdateHz = 60 }, actionMap);

        loop.RunFrames(1);

        Assert.NotNull(renderer.LastMap);
        Assert.Equal(9, renderer.LastMap!.Width);
        Assert.Equal(4, renderer.LastMap.Height);
    }

    private sealed class TestRuntime : IGameRuntime
    {
        public int InitializeCount { get; private set; }
        public int FixedUpdateCount { get; private set; }
        public int RenderCount { get; private set; }
        public int ShutdownCount { get; private set; }

        public void Initialize(IEnginePlatform platform) => InitializeCount++;

        public void FixedUpdate(FrameTime time) => FixedUpdateCount++;

        public void Render(FrameTime time) => RenderCount++;

        public void Shutdown() => ShutdownCount++;
    }

    private sealed class TestRenderer : IRenderer
    {
        public int MapDrawCount { get; private set; }
        public AsciiMap? LastMap { get; private set; }

        public void BeginFrame(FrameTime time)
        {
        }

        public void DrawMap(AsciiMap map)
        {
            MapDrawCount++;
            LastMap = map;
        }

        public void DrawEntities(IEnumerable<Entity> entities)
        {
        }

        public void EndFrame()
        {
        }
    }

    private sealed class SequenceInputSource : IInputSource
    {
        private readonly Queue<InputSnapshot> _snapshots;

        public SequenceInputSource(params InputSnapshot[] snapshots)
        {
            _snapshots = new Queue<InputSnapshot>(snapshots);
        }

        public InputSnapshot Poll()
        {
            if (_snapshots.Count == 0)
                return new InputSnapshot(new HashSet<string>(), new HashSet<string>(), new HashSet<string>());

            return _snapshots.Dequeue();
        }
    }
}
