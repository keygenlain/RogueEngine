using System.Diagnostics;

namespace RogueEngine.Core.Runtime.Diagnostics;

/// <summary>
/// Tracks per-phase timings for recent frames.
/// </summary>
public sealed class FrameProfiler
{
    private readonly Dictionary<string, Stopwatch> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _lastMs = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, double> LastFrameMs => _lastMs;

    public void Begin(string phase)
    {
        if (!_active.TryGetValue(phase, out var sw))
        {
            sw = new Stopwatch();
            _active[phase] = sw;
        }

        sw.Restart();
    }

    public void End(string phase)
    {
        if (!_active.TryGetValue(phase, out var sw))
            return;

        sw.Stop();
        _lastMs[phase] = sw.Elapsed.TotalMilliseconds;
    }

    public string BuildSummary()
    {
        if (_lastMs.Count == 0)
            return "No frame samples.";

        return string.Join(" | ", _lastMs.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value:0.00}ms"));
    }
}
