using RogueEngine.Core.Runtime.Input;
using RogueEngine.Core.Runtime.Platform;

namespace RogueEngine.Core.Runtime;

/// <summary>
/// Deterministic fixed-step engine loop with decoupled render calls.
/// </summary>
public sealed class EngineLoop
{
    private readonly IGameRuntime _runtime;
    private readonly IEnginePlatform _platform;
    private readonly EngineLoopOptions _options;
    private readonly InputActionMap? _inputActions;

    private long _frame;
    private double _totalSeconds;

    public EngineLoop(
        IGameRuntime runtime,
        IEnginePlatform? platform = null,
        EngineLoopOptions? options = null,
        InputActionMap? inputActions = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _platform = platform ?? new DefaultEnginePlatform();
        _options = options ?? new EngineLoopOptions();
        _inputActions = inputActions;

        if (_inputActions is not null && _runtime is IInputActionConsumer consumer)
            consumer.AttachInputActionMap(_inputActions);
    }

    public void RunFrames(int frameCount, CancellationToken cancellationToken = default)
    {
        if (frameCount < 1) return;

        var dt = 1.0 / Math.Max(1, _options.FixedUpdateHz);

        _runtime.Initialize(_platform);
        try
        {
            for (var i = 0; i < frameCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                StepFrame(dt);
            }
        }
        finally
        {
            _runtime.Shutdown();
        }
    }

    public void RunFor(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero) return;

        var dt = 1.0 / Math.Max(1, _options.FixedUpdateHz);
        var remaining = duration.TotalSeconds;

        _runtime.Initialize(_platform);
        try
        {
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                StepFrame(dt);
                remaining -= dt;
            }
        }
        finally
        {
            _runtime.Shutdown();
        }
    }

    private void StepFrame(double fixedDeltaSeconds)
    {
        for (var i = 0; i < 1; i++)
        {
            var snapshot = _platform.Input.Poll();
            _inputActions?.Update(snapshot);

            _frame++;
            _totalSeconds += fixedDeltaSeconds;
            var updateTime = new FrameTime(fixedDeltaSeconds, _totalSeconds, _frame);
            _runtime.FixedUpdate(updateTime);
        }

        var renderTime = new FrameTime(fixedDeltaSeconds, _totalSeconds, _frame);
        _runtime.Render(renderTime);
    }
}
