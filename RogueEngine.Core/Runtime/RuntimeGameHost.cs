using RogueEngine.Core.Runtime.Ecs;
using RogueEngine.Core.Runtime.Input;
using RogueEngine.Core.Runtime.Platform;
using RogueEngine.Core.Runtime.Rendering;
using RogueEngine.Core.Engine;
using RogueEngine.Core.Models;

namespace RogueEngine.Core.Runtime;

/// <summary>
/// Reference runtime host that wires world systems, input map, and renderer.
/// </summary>
public sealed class RuntimeGameHost : IGameRuntime, IInputActionConsumer
{
    private readonly List<ISimulationSystem> _systems = [];
    private readonly ScriptGraph? _graph;
    private readonly int? _scriptSeed;
    private readonly Dictionary<string, string> _actionToKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MoveUp"] = "ArrowUp",
        ["MoveDown"] = "ArrowDown",
        ["MoveLeft"] = "ArrowLeft",
        ["MoveRight"] = "ArrowRight",
        ["Confirm"] = "Enter",
        ["Cancel"] = "Escape",
        ["Interact"] = "Space",
    };

    private ScriptExecutor? _executor;
    private InputActionMap? _inputActions;
    private AsciiMap? _activeMap;
    private IReadOnlyList<Entity> _activeEntities = [];

    public RuntimeGameHost(IRenderer renderer, ScriptGraph? graph = null, int? scriptSeed = null)
    {
        Renderer = renderer;
        World = new World();
        _graph = graph;
        _scriptSeed = scriptSeed;
    }

    public World World { get; }

    public IRenderer Renderer { get; }

    public IEnginePlatform? Platform { get; private set; }

    public void AddSystem(ISimulationSystem system)
    {
        _systems.Add(system);
    }

    public void AttachInputActionMap(InputActionMap inputActions)
    {
        _inputActions = inputActions;
    }

    public void BindActionKey(string actionName, string keyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);
        _actionToKey[actionName] = keyName;
    }

    public void Initialize(IEnginePlatform platform)
    {
        Platform = platform;

        if (_graph is not null)
        {
            _executor = new ScriptExecutor(_graph, seed: _scriptSeed);
            CaptureScriptResult(_executor.Run());
        }

        Platform.Logger.Info("RuntimeGameHost initialized.");
    }

    public void FixedUpdate(FrameTime time)
    {
        if (_executor is not null)
        {
            if (_inputActions is not null)
            {
                foreach (var (action, key) in _actionToKey)
                {
                    if (_inputActions.IsJustPressed(action))
                        CaptureScriptResult(_executor.TriggerKeyPress(key));
                }
            }

            CaptureScriptResult(_executor.Tick());
        }

        foreach (var system in _systems)
            system.Update(World, time);
    }

    public void Render(FrameTime time)
    {
        Renderer.BeginFrame(time);

        if (_activeMap is not null)
            Renderer.DrawMap(_activeMap);
        if (_activeEntities.Count > 0)
            Renderer.DrawEntities(_activeEntities);

        Renderer.EndFrame();
    }

    public void Shutdown()
    {
        Platform?.Logger.Info("RuntimeGameHost shutdown.");
    }

    private void CaptureScriptResult(ExecutionResult result)
    {
        if (result.Map is not null)
            _activeMap = result.Map;

        _activeEntities = result.Entities;

        if (Platform is null || result.Log.Count == 0)
            return;

        foreach (var line in result.Log)
            Platform.Logger.Info($"[Script] {line}");
    }
}
