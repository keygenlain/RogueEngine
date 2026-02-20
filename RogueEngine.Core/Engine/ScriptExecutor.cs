using RogueEngine.Core.Models;

namespace RogueEngine.Core.Engine;

/// <summary>
/// Executes a <see cref="ScriptGraph"/> by walking from the
/// <see cref="NodeType.Start"/> node along execution-flow connections.
///
/// <para>
/// Data-flow values are resolved lazily: when a node needs an input value,
/// the executor walks backwards through data connections to compute it from
/// the connected output node.
/// </para>
///
/// <para>
/// Execution is synchronous and depth-first.  Loop guards prevent runaway
/// infinite loops by capping iterations at <see cref="MaxLoopIterations"/>.
/// </para>
/// </summary>
public sealed class ScriptExecutor
{
    /// <summary>Maximum iterations any single loop node may perform.</summary>
    public const int MaxLoopIterations = 10_000;

    private readonly ScriptGraph _graph;
    private readonly Random _rng;

    // Runtime state shared across nodes during a single Run() call.
    private readonly Dictionary<Guid, object?> _portValues = [];
    private readonly List<string> _log = [];
    private AsciiMap? _activeMap;
    private readonly List<Entity> _entities = [];
    private int _tickCount;
    private int _lastMenuSelection = 0;

    // Tracks nodes currently in the execution call stack to detect recursion.
    private readonly HashSet<Guid> _inProgress = [];

    /// <summary>
    /// Creates a new executor for the given <paramref name="graph"/>.
    /// </summary>
    /// <param name="graph">The script graph to execute.</param>
    /// <param name="seed">Optional RNG seed for reproducible runs.</param>
    public ScriptExecutor(ScriptGraph graph, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the script from its <see cref="NodeType.Start"/> node.
    /// </summary>
    /// <returns>
    /// An <see cref="ExecutionResult"/> containing the log, any active map,
    /// and the final list of entities.
    /// </returns>
    public ExecutionResult Run()
    {
        _portValues.Clear();
        _log.Clear();
        _entities.Clear();
        _activeMap = null;
        _inProgress.Clear();

        var start = _graph.FindStartNode();
        if (start is null)
        {
            _log.Add("[WARN] Graph has no Start node.");
            return BuildResult();
        }

        ExecuteExecChain(start, "Exec");
        return BuildResult();
    }

    /// <summary>
    /// Evaluates a single node and all its transitive data-input dependencies,
    /// without requiring a Start node or exec chain.
    /// Useful for evaluating pure data nodes (variables, math) in isolation.
    /// </summary>
    /// <param name="node">The node to evaluate.</param>
    public void EvaluateNode(ScriptNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _inProgress.Clear();
        _inProgress.Add(node.Id);
        try { ExecuteNode(node); }
        finally { _inProgress.Remove(node.Id); }
    }

    /// <summary>
    /// Simulates a single game tick.
    /// Finds all <see cref="NodeType.OnTick"/> event nodes and fires their chains.
    /// </summary>
    public ExecutionResult Tick()
    {
        _portValues.Clear();
        _log.Clear();
        _inProgress.Clear();
        _tickCount++;

        foreach (var node in _graph.Nodes.Where(n => n.Type == NodeType.OnTick))
        {
            SetPortValue(node, "TickCount", _tickCount);
            ExecuteExecChain(node, "Exec");
        }
        return BuildResult();
    }

    /// <summary>
    /// Sets the menu selection index (used by the WPF layer when a menu returns).
    /// </summary>
    public void SetMenuSelection(int index) => _lastMenuSelection = index;

    // ── Execution helpers ──────────────────────────────────────────────────────

    private void ExecuteExecChain(ScriptNode node, string outPortName)
    {
        var execConn = _graph.GetOutgoingConnections(node.Id)
            .FirstOrDefault(c =>
            {
                var srcPort = node.Outputs.FirstOrDefault(p => p.Id == c.SourcePortId);
                return srcPort?.Name == outPortName;
            });
        if (execConn is null) return;

        var nextNode = _graph.FindNode(execConn.TargetNodeId);
        if (nextNode is null) return;

        ExecuteNode(nextNode);
    }

    private void ExecuteNode(ScriptNode node)
    {
        switch (node.Type)
        {
            // ── Variables ──────────────────────────────────────────────────────
            case NodeType.VariableInt:
                SetPortValue(node, "Value", ParseInt(node.Properties.GetValueOrDefault("Value", "0")));
                break;
            case NodeType.VariableFloat:
                SetPortValue(node, "Value", ParseFloat(node.Properties.GetValueOrDefault("Value", "0")));
                break;
            case NodeType.VariableString:
                SetPortValue(node, "Value", node.Properties.GetValueOrDefault("Value", ""));
                break;
            case NodeType.VariableBool:
                SetPortValue(node, "Value", ParseBool(node.Properties.GetValueOrDefault("Value", "false")));
                break;

            // ── Math & Logic ───────────────────────────────────────────────────
            case NodeType.MathAdd:
            {
                var a = ResolveFloat(node, "A");
                var b = ResolveFloat(node, "B");
                SetPortValue(node, "Result", a + b);
                break;
            }
            case NodeType.MathSubtract:
            {
                var a = ResolveFloat(node, "A");
                var b = ResolveFloat(node, "B");
                SetPortValue(node, "Result", a - b);
                break;
            }
            case NodeType.MathMultiply:
            {
                var a = ResolveFloat(node, "A");
                var b = ResolveFloat(node, "B");
                SetPortValue(node, "Result", a * b);
                break;
            }
            case NodeType.MathDivide:
            {
                var a = ResolveFloat(node, "A");
                var b = ResolveFloat(node, "B");
                SetPortValue(node, "Result", b == 0f ? 0f : a / b);
                break;
            }
            case NodeType.RandomInt:
            {
                var min = ResolveInt(node, "Min");
                var max = ResolveInt(node, "Max");
                if (max <= min) max = min + 1;
                SetPortValue(node, "Value", _rng.Next(min, max));
                break;
            }
            case NodeType.Compare:
            {
                var a = ResolveAny(node, "A");
                var b = ResolveAny(node, "B");
                var op = node.Properties.GetValueOrDefault("Operator", "==");
                SetPortValue(node, "Result", DoCompare(a, b, op));
                break;
            }
            case NodeType.LogicAnd:
            {
                var a = ResolveBool(node, "A");
                var b = ResolveBool(node, "B");
                SetPortValue(node, "Result", a && b);
                break;
            }
            case NodeType.LogicOr:
            {
                var a = ResolveBool(node, "A");
                var b = ResolveBool(node, "B");
                SetPortValue(node, "Result", a || b);
                break;
            }
            case NodeType.LogicNot:
            {
                var a = ResolveBool(node, "A");
                SetPortValue(node, "Result", !a);
                break;
            }

            // ── Control Flow ───────────────────────────────────────────────────
            case NodeType.Start:
                ExecuteExecChain(node, "Exec");
                break;

            case NodeType.Branch:
            {
                var cond = ResolveBool(node, "Condition");
                ExecuteExecChain(node, cond ? "True" : "False");
                break;
            }
            case NodeType.ForLoop:
            {
                var count = Math.Min(ResolveInt(node, "Count"), MaxLoopIterations);
                for (var i = 0; i < count; i++)
                {
                    SetPortValue(node, "Index", i);
                    ExecuteExecChain(node, "Loop Body");
                }
                ExecuteExecChain(node, "Completed");
                break;
            }
            case NodeType.WhileLoop:
            {
                var guard = 0;
                while (ResolveBool(node, "Condition") && ++guard <= MaxLoopIterations)
                    ExecuteExecChain(node, "Loop Body");
                if (guard > MaxLoopIterations)
                    _log.Add($"[WARN] WhileLoop in node {node.Id} hit iteration limit.");
                ExecuteExecChain(node, "Completed");
                break;
            }

            // ── Map & Procgen ──────────────────────────────────────────────────
            case NodeType.CreateMap:
            {
                var w = Math.Max(1, ResolveInt(node, "Width"));
                var h = Math.Max(1, ResolveInt(node, "Height"));
                var newMap = new AsciiMap(w, h);
                _activeMap = newMap;
                SetPortValue(node, "Map", newMap);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GenerateCaveCellular:
            {
                var m = ResolveMap(node, "Map") ?? _activeMap;
                if (m is null) { _log.Add("[ERROR] GenerateCave: no map."); break; }
                var fill = ResolveFloat(node, "FillRatio");
                if (fill == 0f) fill = ParseFloat(node.Properties.GetValueOrDefault("FillRatio", "0.45"));
                var iters = ResolveInt(node, "Iterations");
                if (iters == 0) iters = ParseInt(node.Properties.GetValueOrDefault("Iterations", "5"));
                MapGenerator.GenerateCave(m, fill, iters);
                _activeMap = m;
                SetPortValue(node, "Map", m);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GenerateRoomsBSP:
            {
                var m = ResolveMap(node, "Map") ?? _activeMap;
                if (m is null) { _log.Add("[ERROR] GenerateRoomsBSP: no map."); break; }
                var minR = ResolveInt(node, "MinRoomSize");
                if (minR == 0) minR = ParseInt(node.Properties.GetValueOrDefault("MinRoomSize", "4"));
                var maxR = ResolveInt(node, "MaxRoomSize");
                if (maxR == 0) maxR = ParseInt(node.Properties.GetValueOrDefault("MaxRoomSize", "12"));
                MapGenerator.GenerateRoomsBSP(m, minR, maxR);
                _activeMap = m;
                SetPortValue(node, "Map", m);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GenerateDrunkardWalk:
            {
                var m = ResolveMap(node, "Map") ?? _activeMap;
                if (m is null) { _log.Add("[ERROR] DrunkardWalk: no map."); break; }
                var steps = ResolveInt(node, "Steps");
                if (steps == 0) steps = ParseInt(node.Properties.GetValueOrDefault("Steps", "500"));
                MapGenerator.GenerateDrunkardWalk(m, steps);
                _activeMap = m;
                SetPortValue(node, "Map", m);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.FillRegion:
            {
                var m = ResolveMap(node, "Map") ?? _activeMap;
                if (m is null) { _log.Add("[ERROR] FillRegion: no map."); break; }
                var fillX = ResolveInt(node, "X");
                var fillY = ResolveInt(node, "Y");
                var fillW = ResolveInt(node, "Width");
                var fillH = ResolveInt(node, "Height");
                var ch = node.Properties.GetValueOrDefault("Char", "#");
                var fgHex = ParseColor(node.Properties.GetValueOrDefault("FgColor", "FFFFFF"));
                var bgHex = ParseColor(node.Properties.GetValueOrDefault("BgColor", "000000"));
                m.FillRegion(fillX, fillY, fillW, fillH, new AsciiCell
                {
                    Character = ch.Length > 0 ? ch[0] : '#',
                    ForegroundColor = fgHex,
                    BackgroundColor = bgHex,
                });
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GetCell:
            {
                var m = ResolveMap(node, "Map") ?? _activeMap;
                if (m is null) { _log.Add("[ERROR] GetCell: no map."); break; }
                var cx = ResolveInt(node, "X");
                var cy = ResolveInt(node, "Y");
                if (m.IsInBounds(cx, cy))
                    SetPortValue(node, "Cell", m[cx, cy]);
                break;
            }
            case NodeType.SetCell:
            {
                var m = ResolveMap(node, "Map") ?? _activeMap;
                if (m is null) { _log.Add("[ERROR] SetCell: no map."); break; }
                var cx = ResolveInt(node, "X");
                var cy = ResolveInt(node, "Y");
                var cell = ResolveAny(node, "Cell") as AsciiCell;
                if (cell is not null && m.IsInBounds(cx, cy))
                    m[cx, cy] = cell;
                ExecuteExecChain(node, "Exec");
                break;
            }

            // ── Entity ─────────────────────────────────────────────────────────
            case NodeType.SpawnEntity:
            {
                var ex = ResolveInt(node, "X");
                var ey = ResolveInt(node, "Y");
                var glyph = node.Properties.GetValueOrDefault("Glyph", "@");
                var ent = new Entity
                {
                    Name = node.Properties.GetValueOrDefault("Name", "Entity"),
                    Glyph = glyph.Length > 0 ? glyph[0] : '@',
                    ForegroundColor = ParseColor(node.Properties.GetValueOrDefault("FgColor", "FFFFFF")),
                    X = ex,
                    Y = ey,
                };
                _entities.Add(ent);
                SetPortValue(node, "Entity", ent);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.MoveEntity:
            {
                var ent = ResolveAny(node, "Entity") as Entity;
                if (ent is null) break;
                var dx = ResolveInt(node, "DX");
                var dy = ResolveInt(node, "DY");
                ent.Move(dx, dy,
                    _activeMap?.Width,
                    _activeMap?.Height);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.DestroyEntity:
            {
                var ent = ResolveAny(node, "Entity") as Entity;
                if (ent is not null) _entities.Remove(ent);
                ExecuteExecChain(node, "Exec");
                break;
            }

            // ── ASCII Display ──────────────────────────────────────────────────
            case NodeType.RenderMap:
            {
                var m = ResolveMap(node, "Map") ?? _activeMap;
                if (m is not null)
                {
                    _log.Add($"[RENDER] Map {m.Width}×{m.Height}");
                    _activeMap = m;
                }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.DrawChar:
            {
                var px = ResolveInt(node, "X");
                var py = ResolveInt(node, "Y");
                var ch = node.Properties.GetValueOrDefault("Char", "@");
                if (_activeMap is not null && _activeMap.IsInBounds(px, py))
                    _activeMap[px, py] = new AsciiCell
                    {
                        Character = ch.Length > 0 ? ch[0] : ' ',
                        ForegroundColor = ParseColor(node.Properties.GetValueOrDefault("FgColor", "FFFFFF")),
                        BackgroundColor = ParseColor(node.Properties.GetValueOrDefault("BgColor", "000000")),
                    };
                _log.Add($"[DRAW] '{ch}' @ ({px},{py})");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.PrintText:
            {
                var txt = ResolveString(node, "Text");
                var px = ResolveInt(node, "X");
                var py = ResolveInt(node, "Y");
                _log.Add($"[TEXT] \"{txt}\" @ ({px},{py})");
                if (_activeMap is not null)
                {
                    for (var i = 0; i < txt.Length; i++)
                    {
                        var tx = px + i;
                        if (_activeMap.IsInBounds(tx, py))
                            _activeMap[tx, py] = new AsciiCell
                            {
                                Character = txt[i],
                                ForegroundColor = 0xFFFFFF,
                                BackgroundColor = 0x000000,
                            };
                    }
                }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.ClearDisplay:
            {
                _activeMap?.Fill(new AsciiCell());
                _log.Add("[CLEAR]");
                ExecuteExecChain(node, "Exec");
                break;
            }

            // ── Menus ──────────────────────────────────────────────────────────
            case NodeType.ShowMenu:
            {
                var title = node.Properties.GetValueOrDefault("Title", "Menu");
                var items = node.Properties.GetValueOrDefault("Items", "").Split('\n');
                _log.Add($"[MENU] {title}: {string.Join(", ", items)}");
                // In the engine context menus are rendered by the WPF layer;
                // the executor just records the selection (default: 0).
                SetPortValue(node, "SelectedIndex", _lastMenuSelection);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GetMenuSelection:
                SetPortValue(node, "SelectedIndex", _lastMenuSelection);
                break;

            // ── Events ─────────────────────────────────────────────────────────
            case NodeType.OnKeyPress:
            case NodeType.OnTick:
            case NodeType.OnEntityEnterTile:
                // Event nodes fire their chain when triggered externally via Tick()
                // or dedicated trigger methods; nothing to do here.
                break;

            default:
                _log.Add($"[WARN] Unhandled node type: {node.Type}");
                break;
        }
    }

    // ── Value resolution ───────────────────────────────────────────────────────

    /// <summary>
    /// Sets the runtime value of an output port by port name.
    /// </summary>
    private void SetPortValue(ScriptNode node, string portName, object? value)
    {
        var port = node.Outputs.FirstOrDefault(p => p.Name == portName);
        if (port is null) return;
        _portValues[port.Id] = value;
        port.RuntimeValue = value;
    }

    /// <summary>
    /// Resolves the current value of an input port.
    /// If the port is connected, the cached output value is used when available;
    /// otherwise the source node is evaluated on-demand.
    /// Falls back to the port's <see cref="NodePort.DefaultValue"/> when unconnected.
    /// </summary>
    private object? ResolveAny(ScriptNode node, string portName)
    {
        var port = node.Inputs.FirstOrDefault(p => p.Name == portName);
        if (port is null) return null;

        var conn = _graph.GetIncomingConnections(node.Id)
            .FirstOrDefault(c => c.TargetPortId == port.Id);

        if (conn is not null)
        {
            // Use cached value if the source port has already produced a result.
            if (_portValues.TryGetValue(conn.SourcePortId, out var cached))
                return cached;

            var srcNode = _graph.FindNode(conn.SourceNodeId);
            if (srcNode is not null && !_inProgress.Contains(srcNode.Id))
            {
                _inProgress.Add(srcNode.Id);
                try { ExecuteNode(srcNode); }
                finally { _inProgress.Remove(srcNode.Id); }

                if (_portValues.TryGetValue(conn.SourcePortId, out var computed))
                    return computed;
            }
        }

        // No connection: fall back to node Properties dict first, then port DefaultValue.
        if (node.Properties.TryGetValue(portName, out var propVal) &&
            !string.IsNullOrEmpty(propVal))
            return propVal;

        return port.DefaultValue;
    }

    private int ResolveInt(ScriptNode node, string portName)
    {
        var v = ResolveAny(node, portName);
        return v switch
        {
            int i => i,
            float f => (int)f,
            double d => (int)d,
            string s => ParseInt(s),
            _ => 0,
        };
    }

    private float ResolveFloat(ScriptNode node, string portName)
    {
        var v = ResolveAny(node, portName);
        return v switch
        {
            float f => f,
            int i => (float)i,
            double d => (float)d,
            string s => ParseFloat(s),
            _ => 0f,
        };
    }

    private bool ResolveBool(ScriptNode node, string portName)
    {
        var v = ResolveAny(node, portName);
        return v switch
        {
            bool b => b,
            int i => i != 0,
            string s => ParseBool(s),
            _ => false,
        };
    }

    private string ResolveString(ScriptNode node, string portName)
    {
        var v = ResolveAny(node, portName);
        return v?.ToString() ?? string.Empty;
    }

    private AsciiMap? ResolveMap(ScriptNode node, string portName)
    {
        return ResolveAny(node, portName) as AsciiMap;
    }

    // ── Comparison ─────────────────────────────────────────────────────────────

    private static bool DoCompare(object? a, object? b, string op)
    {
        if (a is null || b is null) return false;
        try
        {
            var fa = Convert.ToDouble(a);
            var fb = Convert.ToDouble(b);
            return op switch
            {
                "==" => Math.Abs(fa - fb) < 1e-9,
                "!=" => Math.Abs(fa - fb) >= 1e-9,
                ">"  => fa > fb,
                "<"  => fa < fb,
                ">=" => fa >= fb,
                "<=" => fa <= fb,
                _ => false,
            };
        }
        catch
        {
            return op == "==" ? a.ToString() == b.ToString() : a.ToString() != b.ToString();
        }
    }

    // ── Parsing helpers ────────────────────────────────────────────────────────

    private static int ParseInt(string s) =>
        int.TryParse(s, out var v) ? v : 0;

    private static float ParseFloat(string s) =>
        float.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static bool ParseBool(string s) =>
        s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1";

    private static int ParseColor(string hex)
    {
        if (int.TryParse(hex.TrimStart('#'), System.Globalization.NumberStyles.HexNumber,
            null, out var v)) return v;
        return 0xFFFFFF;
    }

    // ── Result building ────────────────────────────────────────────────────────

    private ExecutionResult BuildResult() =>
        new(_log.AsReadOnly(), _activeMap, new List<Entity>(_entities));
}

/// <summary>
/// Captures the results of a single <see cref="ScriptExecutor"/> run.
/// </summary>
/// <param name="Log">Execution log messages.</param>
/// <param name="Map">The active <see cref="AsciiMap"/> at the end of execution, if any.</param>
/// <param name="Entities">All live entities at the end of execution.</param>
public sealed record ExecutionResult(
    IReadOnlyList<string> Log,
    AsciiMap? Map,
    IReadOnlyList<Entity> Entities);
