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
///
/// <para>
/// The executor integrates three optional sub-systems:
/// <list type="bullet">
///   <item><see cref="Overworld"/> – overworld travel and location management</item>
///   <item><see cref="NetworkSessionManager"/> – multiplayer sessions</item>
///   <item><see cref="PersistenceManager"/> – save / load game state</item>
/// </list>
/// Pass instances of these to the constructor to enable their associated node
/// types.  All three default to new independent instances.
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
    private int _waitTicksRemaining;
    private bool _inCutscene;

    // Tracks nodes currently in the execution call stack to detect recursion.
    private readonly HashSet<Guid> _inProgress = [];

    // ── Sub-systems ────────────────────────────────────────────────────────────

    /// <summary>
    /// <see langword="true"/> while a cutscene started by
    /// <see cref="NodeType.StartCutscene"/> is running.
    /// Normal player input should be suppressed in this state.
    /// </summary>
    public bool IsInCutscene => _inCutscene;

    /// <summary>Overworld and faction/time management.</summary>
    public OverworldManager Overworld { get; }

    /// <summary>Save / load persistence.</summary>
    public PersistenceManager Persistence { get; }

    /// <summary>
    /// Active network session manager.
    /// Assign a running <see cref="NetworkSessionManager"/> before executing
    /// multiplayer nodes.
    /// </summary>
    public NetworkSessionManager? Network { get; set; }

    /// <summary>
    /// Scene tree managed by this executor.
    /// Scene-tree node types (SceneCreate, SceneAddChild, etc.) operate on this tree.
    /// A fresh tree with the default <see cref="Scene.SpriteLibrary"/> is created
    /// automatically; pass a pre-configured tree to share state across executors.
    /// </summary>
    public Scene.SceneTree SceneTree { get; }

    /// <summary>
    /// Creates a new executor for the given <paramref name="graph"/>.
    /// </summary>
    /// <param name="graph">The script graph to execute.</param>
    /// <param name="seed">Optional RNG seed for reproducible runs.</param>
    /// <param name="overworld">Optional shared overworld manager.</param>
    /// <param name="persistence">Optional shared persistence manager.</param>
    /// <param name="sceneTree">Optional pre-configured scene tree.</param>
    public ScriptExecutor(
        ScriptGraph graph,
        int? seed = null,
        OverworldManager? overworld = null,
        PersistenceManager? persistence = null,
        Scene.SceneTree? sceneTree = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        Overworld   = overworld   ?? new OverworldManager();
        Persistence = persistence ?? new PersistenceManager();
        SceneTree   = sceneTree   ?? new Scene.SceneTree();
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
            case NodeType.OnEnterLocation:
            case NodeType.OnLeaveLocation:
            case NodeType.OnMessageReceived:
            case NodeType.OnEntityStateReceived:
            case NodeType.OnRelationChanged:
            case NodeType.OnTimeOfDay:
                // Event nodes fire their chain when triggered externally.
                break;

            // ── Overworld ──────────────────────────────────────────────────────
            case NodeType.CreateOverworld:
            {
                var name = node.Properties.GetValueOrDefault("Name", "World");
                var world = Overworld.CreateOverworld(name);
                SetPortValue(node, "Overworld", world);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.AddLocation:
            {
                var world = ResolveAny(node, "Overworld") as Models.Overworld
                    ?? Overworld.ActiveOverworld;
                if (world is null) { _log.Add("[ERROR] AddLocation: no overworld."); break; }
                var locName = node.Properties.GetValueOrDefault("Name", "Location");
                var wx = int.TryParse(node.Properties.GetValueOrDefault("WorldX", "0"), out var wx2) ? wx2 : 0;
                var wy = int.TryParse(node.Properties.GetValueOrDefault("WorldY", "0"), out var wy2) ? wy2 : 0;
                var desc = node.Properties.GetValueOrDefault("Description", "");
                var loc = Overworld.AddLocation(world, locName, wx, wy, desc);
                SetPortValue(node, "Location", loc);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.ConnectLocations:
            {
                var from = ResolveAny(node, "From") as OverworldLocation;
                var to   = ResolveAny(node, "To") as OverworldLocation;
                if (from is null || to is null) { _log.Add("[ERROR] ConnectLocations: missing location."); break; }
                var exit = node.Properties.GetValueOrDefault("ExitName", "North");
                var rev  = node.Properties.GetValueOrDefault("ReverseExitName", "South");
                Overworld.ConnectLocations(from, to, exit, rev);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.TravelToLocation:
            {
                var world = ResolveAny(node, "Overworld") as Models.Overworld
                    ?? Overworld.ActiveOverworld;
                var loc = ResolveAny(node, "Location") as OverworldLocation;
                if (world is not null && loc is not null)
                {
                    world.TravelTo(loc.Id);
                    _log.Add($"[TRAVEL] Arrived at '{loc.Name}'");
                }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.TravelViaExit:
            {
                var world = ResolveAny(node, "Overworld") as Models.Overworld
                    ?? Overworld.ActiveOverworld;
                var exit = node.Properties.GetValueOrDefault("ExitName", "North");
                if (world is not null)
                {
                    var dest = world.TravelViaExit(exit);
                    SetPortValue(node, "Arrived", dest);
                    _log.Add($"[TRAVEL] via '{exit}' → '{dest?.Name ?? "nowhere"}'");
                }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GetCurrentLocation:
            {
                var world = ResolveAny(node, "Overworld") as Models.Overworld
                    ?? Overworld.ActiveOverworld;
                var cur = world?.CurrentLocation;
                SetPortValue(node, "Location", cur);
                SetPortValue(node, "Name", cur?.Name ?? string.Empty);
                break;
            }
            case NodeType.GetLocationData:
            {
                var loc = ResolveAny(node, "Location") as OverworldLocation
                    ?? Overworld.ActiveOverworld?.CurrentLocation;
                var key = node.Properties.GetValueOrDefault("Key", "key");
                var val = loc?.PersistentData.GetValueOrDefault(key, string.Empty) ?? string.Empty;
                SetPortValue(node, "Value", val);
                break;
            }
            case NodeType.SetLocationData:
            {
                var loc = ResolveAny(node, "Location") as OverworldLocation
                    ?? Overworld.ActiveOverworld?.CurrentLocation;
                var key = node.Properties.GetValueOrDefault("Key", "key");
                var val = ResolveString(node, "Value");
                if (loc is not null) loc.PersistentData[key] = val;
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GenerateLocation:
            {
                var loc = ResolveAny(node, "Location") as OverworldLocation;
                if (loc is null) { _log.Add("[ERROR] GenerateLocation: no location."); break; }
                var algo  = node.Properties.GetValueOrDefault("Algorithm", "Cave");
                var gw    = int.TryParse(node.Properties.GetValueOrDefault("Width", "60"),  out var gwv) ? gwv : 60;
                var gh    = int.TryParse(node.Properties.GetValueOrDefault("Height", "20"), out var ghv) ? ghv : 20;
                var map   = Overworld.GenerateLocationMap(loc, algo, gw, gh);
                _activeMap = map;
                SetPortValue(node, "Map", map);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.RenderOverworld:
            {
                var world = ResolveAny(node, "Overworld") as Models.Overworld
                    ?? Overworld.ActiveOverworld;
                if (world is not null && _activeMap is not null)
                    Overworld.RenderOverworld(world, _activeMap, _activeMap.Width, _activeMap.Height);
                ExecuteExecChain(node, "Exec");
                break;
            }

            // ── Multiplayer ────────────────────────────────────────────────────
            case NodeType.HostSession:
            {
                var mgr = Network ?? new NetworkSessionManager();
                Network = mgr;
                var sName  = node.Properties.GetValueOrDefault("SessionName", "My Game");
                var pName  = node.Properties.GetValueOrDefault("PlayerName", "Host");
                var port   = int.TryParse(node.Properties.GetValueOrDefault("Port", "7777"), out var pv) ? pv : 7777;
                var maxP   = int.TryParse(node.Properties.GetValueOrDefault("MaxPlayers", "4"), out var mpv) ? mpv : 4;
                // Fire-and-forget; script continues immediately.
                _ = mgr.HostAsync(sName, pName, port, maxP);
                SetPortValue(node, "Session", mgr.Session);
                _log.Add($"[NET] Hosting '{sName}' on :{port}");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.JoinSession:
            {
                var mgr = Network ?? new NetworkSessionManager();
                Network = mgr;
                var host  = node.Properties.GetValueOrDefault("Host", "127.0.0.1");
                var pName = node.Properties.GetValueOrDefault("PlayerName", "Player");
                var port  = int.TryParse(node.Properties.GetValueOrDefault("Port", "7777"), out var pv) ? pv : 7777;
                _ = mgr.JoinAsync(host, pName, port);
                SetPortValue(node, "Session", mgr.Session);
                _log.Add($"[NET] Joining {host}:{port}");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.LeaveSession:
            {
                if (Network is not null) { _ = Network.DisconnectAsync(); }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.BroadcastMessage:
            {
                var payload = ResolveString(node, "Payload");
                var msgType = node.Properties.GetValueOrDefault("MessageType", "chat");
                if (Network is not null)
                    _ = Network.BroadcastAsync(new NetworkMessage { MessageType = msgType, Payload = payload });
                _log.Add($"[NET] Broadcast [{msgType}]: {payload}");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SendMessageToPlayer:
            {
                var payload = ResolveString(node, "Payload");
                var msgType = node.Properties.GetValueOrDefault("MessageType", "direct");
                var target  = ResolveString(node, "TargetPlayer");
                _log.Add($"[NET] → {target} [{msgType}]: {payload}");
                if (Network is not null)
                    _ = Network.BroadcastAsync(new NetworkMessage
                    {
                        MessageType = msgType,
                        Payload = payload,
                        // Routing by name: host filters on its end.
                    });
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GetConnectedPlayers:
            {
                var session = ResolveAny(node, "Session") as NetworkSession
                    ?? Network?.Session;
                var count = session?.PlayerCount ?? 0;
                var names = session is not null
                    ? string.Join("\n", session.Players.Select(p => p.Name))
                    : string.Empty;
                SetPortValue(node, "PlayerCount", count);
                SetPortValue(node, "PlayerNames", names);
                break;
            }
            case NodeType.GetLocalPlayerName:
            {
                var session = ResolveAny(node, "Session") as NetworkSession
                    ?? Network?.Session;
                SetPortValue(node, "Name", session?.LocalPlayer?.Name ?? string.Empty);
                break;
            }
            case NodeType.IsHost:
            {
                var session = ResolveAny(node, "Session") as NetworkSession
                    ?? Network?.Session;
                SetPortValue(node, "Result", session?.IsHost ?? false);
                break;
            }
            case NodeType.SyncEntityState:
            {
                var ent = ResolveAny(node, "Entity") as Entity;
                if (ent is not null && Network is not null)
                    _ = Network.BroadcastAsync(new NetworkMessage
                    {
                        MessageType = "entity-state",
                        Payload = $"{ent.Id},{ent.X},{ent.Y},{ent.Glyph}",
                    });
                ExecuteExecChain(node, "Exec");
                break;
            }

            // ── Persistence ────────────────────────────────────────────────────
            case NodeType.SaveGame:
            {
                var slot    = node.Properties.GetValueOrDefault("Slot", "slot1");
                var success = Persistence.Save(slot,
                    Overworld, _entities,
                    Overworld.GameHour);
                SetPortValue(node, "Success", success);
                _log.Add($"[SAVE] Slot '{slot}': {(success ? "OK" : "FAILED")}");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.LoadGame:
            {
                var slot   = node.Properties.GetValueOrDefault("Slot", "slot1");
                var result = Persistence.Load(slot, Overworld);
                var ok     = result is not null;
                if (ok)
                {
                    _entities.Clear();
                    _entities.AddRange(result!.Entities);
                }
                SetPortValue(node, "Success", ok);
                _log.Add($"[LOAD] Slot '{slot}': {(ok ? "OK" : "NOT FOUND")}");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.DeleteSave:
            {
                var slot = node.Properties.GetValueOrDefault("Slot", "slot1");
                Persistence.DeleteSlot(slot);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SaveSlotExists:
            {
                var slot = node.Properties.GetValueOrDefault("Slot", "slot1");
                SetPortValue(node, "Exists", Persistence.SlotExists(slot));
                break;
            }
            case NodeType.SetPersistentValue:
            {
                var key = node.Properties.GetValueOrDefault("Key", "key");
                var val = ResolveString(node, "Value");
                Persistence.SetValue(key, val);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GetPersistentValue:
            {
                var key = node.Properties.GetValueOrDefault("Key", "key");
                var def = node.Properties.GetValueOrDefault("Default", "");
                SetPortValue(node, "Value", Persistence.GetValue(key, def));
                break;
            }

            // ── Dialogue & Cutscenes ───────────────────────────────────────────
            case NodeType.ShowDialogueLine:
            {
                var speaker = node.Properties.GetValueOrDefault("Speaker", "NPC");
                var text    = node.Properties.GetValueOrDefault("Text", "");
                var dx      = int.TryParse(node.Properties.GetValueOrDefault("X", "0"),  out var dxv) ? dxv : 0;
                var dy      = int.TryParse(node.Properties.GetValueOrDefault("Y", "20"), out var dyv) ? dyv : 20;
                _log.Add($"[DIALOGUE] {speaker}: {text}");
                if (_activeMap is not null)
                {
                    var line = $"{speaker}: {text}";
                    for (var i = 0; i < line.Length && _activeMap.IsInBounds(dx + i, dy); i++)
                        _activeMap[dx + i, dy] = new AsciiCell
                        {
                            Character = line[i],
                            ForegroundColor = 0xFFFF88,
                            BackgroundColor = 0x000033,
                        };
                }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.ShowDialogueChoice:
            {
                var prompt  = node.Properties.GetValueOrDefault("Prompt", "Choose:");
                var choices = node.Properties.GetValueOrDefault("Choices", "").Split('\n');
                _log.Add($"[CHOICE] {prompt}: {string.Join(", ", choices)}");
                SetPortValue(node, "SelectedIndex", _lastMenuSelection);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.OnDialogueChoice:
            {
                var target = int.TryParse(
                    node.Properties.GetValueOrDefault("ChoiceIndex", "0"), out var cv) ? cv : 0;
                if (ResolveInt(node, "SelectedIndex") == target)
                    ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.StartCutscene:
                _inCutscene = true;
                _log.Add($"[CUTSCENE] Start: {node.Properties.GetValueOrDefault("Name", "")}");
                ExecuteExecChain(node, "Exec");
                break;
            case NodeType.EndCutscene:
                _inCutscene = false;
                _log.Add("[CUTSCENE] End");
                ExecuteExecChain(node, "Exec");
                break;
            case NodeType.Wait:
            {
                var ticks = ResolveInt(node, "Ticks");
                _waitTicksRemaining = ticks;
                _log.Add($"[WAIT] {ticks} ticks");
                // In sync execution we skip immediately; async tick-based wait is
                // handled in Tick() via _waitTicksRemaining.
                ExecuteExecChain(node, "Exec");
                break;
            }

            // ── Factions ──────────────────────────────────────────────────────
            case NodeType.CreateFaction:
                _log.Add($"[FACTION] Created: {node.Properties.GetValueOrDefault("Name", "")}");
                ExecuteExecChain(node, "Exec");
                break;
            case NodeType.AssignEntityFaction:
            {
                var ent     = ResolveAny(node, "Entity") as Entity;
                var faction = node.Properties.GetValueOrDefault("Faction", "");
                if (ent is not null) Overworld.AssignEntityFaction(ent.Id.ToString(), faction);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SetFactionRelation:
            {
                var fa  = node.Properties.GetValueOrDefault("FactionA", "A");
                var fb  = node.Properties.GetValueOrDefault("FactionB", "B");
                var val = ResolveInt(node, "Value");
                Overworld.SetFactionRelation(fa, fb, val);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GetFactionRelation:
            {
                var fa = node.Properties.GetValueOrDefault("FactionA", "A");
                var fb = node.Properties.GetValueOrDefault("FactionB", "B");
                SetPortValue(node, "Value", Overworld.GetFactionRelation(fa, fb));
                break;
            }
            case NodeType.GetEntityFaction:
            {
                var ent = ResolveAny(node, "Entity") as Entity;
                var faction = ent is not null
                    ? Overworld.GetEntityFaction(ent.Id.ToString())
                    : string.Empty;
                SetPortValue(node, "Faction", faction);
                break;
            }

            // ── Time ──────────────────────────────────────────────────────────
            case NodeType.AdvanceTime:
            {
                var amount  = ResolveInt(node, "Amount");
                if (amount == 0)
                    int.TryParse(node.Properties.GetValueOrDefault("Amount", "1"), out amount);
                var newHour = Overworld.AdvanceTime(amount);
                SetPortValue(node, "NewHour", newHour);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.GetTimeOfDay:
                SetPortValue(node, "Hour", Overworld.GameHour);
                break;
            case NodeType.IsNight:
            {
                var ns = int.TryParse(node.Properties.GetValueOrDefault("NightStart", "20"), out var nsv) ? nsv : 20;
                var ne = int.TryParse(node.Properties.GetValueOrDefault("NightEnd",   "6"),  out var nev) ? nev : 6;
                SetPortValue(node, "Result", Overworld.IsNight(ns, ne));
                break;
            }

            // ── Scene Tree ─────────────────────────────────────────────────────
            case NodeType.SceneCreate:
            {
                var nodeTypeName = node.Properties.GetValueOrDefault("NodeType", "SpriteNode");
                var nodeName     = node.Properties.GetValueOrDefault("Name", "NewNode");
                var sceneNode    = CreateSceneNodeByType(nodeTypeName, nodeName);
                var parent       = ResolveAny(node, "Parent") as Scene.SceneNode;
                if (parent is not null) parent.AddChild(sceneNode);
                else SceneTree.Root.AddChild(sceneNode);
                SetPortValue(node, "Node", sceneNode);
                _log.Add($"[SCENE] Created {nodeTypeName} '{nodeName}'");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SceneAddChild:
            {
                var parent = ResolveAny(node, "Parent") as Scene.SceneNode;
                var child  = ResolveAny(node, "Child")  as Scene.SceneNode;
                if (parent is not null && child is not null)
                    parent.AddChild(child);
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SceneRemoveChild:
            {
                var sceneNode = ResolveAny(node, "Node") as Scene.SceneNode;
                sceneNode?.QueueFree();
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SceneFindNode:
            {
                var name = node.Properties.GetValueOrDefault("Name", "");
                var found = SceneTree.FindNode<Scene.SceneNode>(name);
                SetPortValue(node, "Node", found);
                break;
            }
            case NodeType.SceneInstantiate:
            {
                var sceneName = node.Properties.GetValueOrDefault("SceneName", "");
                // Instantiation via ChangeScene is the primary path; here we just log.
                _log.Add($"[SCENE] Instantiate '{sceneName}'");
                try { SceneTree.ChangeScene(sceneName); }
                catch (KeyNotFoundException) { _log.Add($"[WARN] Scene '{sceneName}' not registered."); }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SceneChange:
            {
                var sceneName = node.Properties.GetValueOrDefault("SceneName", "");
                try { SceneTree.ChangeScene(sceneName); _log.Add($"[SCENE] → '{sceneName}'"); }
                catch (KeyNotFoundException) { _log.Add($"[WARN] Scene '{sceneName}' not found."); }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SceneGetCurrent:
                SetPortValue(node, "Name", SceneTree.ActiveSceneName);
                break;
            case NodeType.SceneSetPosition:
            {
                var sceneNode = ResolveAny(node, "Node") as Scene.GridNode;
                if (sceneNode is not null)
                {
                    sceneNode.GridX = ResolveInt(node, "X");
                    sceneNode.GridY = ResolveInt(node, "Y");
                }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SceneGetPosition:
            {
                var sceneNode = ResolveAny(node, "Node") as Scene.GridNode;
                SetPortValue(node, "X", sceneNode?.GridX ?? 0);
                SetPortValue(node, "Y", sceneNode?.GridY ?? 0);
                break;
            }
            case NodeType.SceneSetActive:
            {
                var sceneNode = ResolveAny(node, "Node") as Scene.SceneNode;
                if (sceneNode is not null)
                    sceneNode.Active = ResolveBool(node, "Active");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SceneSetVisible:
            {
                var sceneNode = ResolveAny(node, "Node") as Scene.SceneNode;
                if (sceneNode is not null)
                    sceneNode.Visible = ResolveBool(node, "Visible");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.OnTimerTimeout:
            case NodeType.OnAreaBodyEntered:
            case NodeType.OnAreaBodyExited:
                // Event nodes — fired externally by the SceneTree.
                break;

            // ── Sprite System ──────────────────────────────────────────────────
            case NodeType.RegisterSprite:
            {
                var spriteName = node.Properties.GetValueOrDefault("Name", "sprite");
                var glyphStr   = node.Properties.GetValueOrDefault("Glyph", "?");
                int.TryParse(node.Properties.GetValueOrDefault("FgColor", "FFFFFF"),
                    System.Globalization.NumberStyles.HexNumber, null, out var fg);
                int.TryParse(node.Properties.GetValueOrDefault("BgColor", "000000"),
                    System.Globalization.NumberStyles.HexNumber, null, out var bg);
                var imagePath   = node.Properties.GetValueOrDefault("ImagePath", "");
                int.TryParse(node.Properties.GetValueOrDefault("TileX",      "0"), out var tx);
                int.TryParse(node.Properties.GetValueOrDefault("TileY",      "0"), out var ty);
                int.TryParse(node.Properties.GetValueOrDefault("TileWidth",  "0"), out var tw);
                int.TryParse(node.Properties.GetValueOrDefault("TileHeight", "0"), out var th);
                Enum.TryParse<Scene.SpriteRenderMode>(
                    node.Properties.GetValueOrDefault("RenderMode", "Auto"), out var mode);

                var sprite = new Scene.SpriteDefinition
                {
                    Name            = spriteName,
                    Glyph           = glyphStr.Length > 0 ? glyphStr[0] : '?',
                    ForegroundColor = fg,
                    BackgroundColor = bg,
                    ImagePath       = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath,
                    TileX           = tx, TileY = ty, TileWidth = tw, TileHeight = th,
                    RenderMode      = mode,
                };
                SceneTree.Library.Register(sprite);
                _log.Add($"[SPRITE] Registered '{spriteName}'");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.LoadSpriteSheet:
            {
                var sheetName = node.Properties.GetValueOrDefault("SheetName", "tiles");
                var imagePath = node.Properties.GetValueOrDefault("ImagePath", "");
                int.TryParse(node.Properties.GetValueOrDefault("TileWidth",  "16"), out var tw);
                int.TryParse(node.Properties.GetValueOrDefault("TileHeight", "16"), out var th);
                int.TryParse(node.Properties.GetValueOrDefault("SpacingX",   "0"),  out var sx);
                int.TryParse(node.Properties.GetValueOrDefault("SpacingY",   "0"),  out var sy);
                int.TryParse(node.Properties.GetValueOrDefault("MarginX",    "0"),  out var mx);
                int.TryParse(node.Properties.GetValueOrDefault("MarginY",    "0"),  out var my);
                var sheet = new Scene.SpriteSheet
                {
                    Name = sheetName, ImagePath = imagePath,
                    TileWidth = tw, TileHeight = th,
                    SpacingX = sx, SpacingY = sy,
                    MarginX = mx, MarginY = my,
                };
                SceneTree.Library.RegisterSheet(sheet);
                _log.Add($"[SPRITE] Loaded sheet '{sheetName}'");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SpriteSetSprite:
            {
                var sceneNode = ResolveAny(node, "Node") as Scene.SpriteNode;
                var name      = node.Properties.GetValueOrDefault("SpriteName", "unknown");
                if (sceneNode is not null)
                {
                    sceneNode.SpriteName = name;
                    sceneNode.Sprite     = SceneTree.Library.Get(name);
                }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SpriteGetSprite:
            {
                var sceneNode = ResolveAny(node, "Node") as Scene.SpriteNode;
                SetPortValue(node, "SpriteName", sceneNode?.SpriteName ?? string.Empty);
                break;
            }
            case NodeType.SpriteSetRenderMode:
            {
                var sceneNode = ResolveAny(node, "Node") as Scene.SpriteNode;
                if (sceneNode?.Sprite is not null)
                {
                    Enum.TryParse<Scene.SpriteRenderMode>(
                        node.Properties.GetValueOrDefault("Mode", "Auto"), out var rm);
                    sceneNode.Sprite.RenderMode = rm;
                }
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SpriteSetPlaying:
            {
                var sceneNode = ResolveAny(node, "Node") as Scene.SpriteNode;
                if (sceneNode is not null)
                    sceneNode.Playing = ResolveBool(node, "Playing");
                ExecuteExecChain(node, "Exec");
                break;
            }

            // ── Client-Server Multiplayer ──────────────────────────────────────
            case NodeType.HostServer:
            {
                var mgr    = Network ?? new NetworkSessionManager();
                Network    = mgr;
                var sName  = node.Properties.GetValueOrDefault("SessionName", "Dedicated Server");
                var port   = int.TryParse(node.Properties.GetValueOrDefault("Port", "7777"), out var pv) ? pv : 7777;
                var maxP   = int.TryParse(node.Properties.GetValueOrDefault("MaxPlayers", "16"), out var mpv) ? mpv : 16;
                _ = mgr.HostAsServerAsync(sName, port, maxP);
                SetPortValue(node, "Session", mgr.Session);
                _log.Add($"[NET] Dedicated server '{sName}' on :{port}");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.ConnectToServer:
            {
                var mgr   = Network ?? new NetworkSessionManager();
                Network   = mgr;
                var host  = node.Properties.GetValueOrDefault("Host", "127.0.0.1");
                var pName = node.Properties.GetValueOrDefault("PlayerName", "Player");
                var port  = int.TryParse(node.Properties.GetValueOrDefault("Port", "7777"), out var pv) ? pv : 7777;
                _ = mgr.JoinAsync(host, pName, port, NetworkRole.AuthoritativeClient);
                SetPortValue(node, "Session", mgr.Session);
                _log.Add($"[NET] Connecting to server {host}:{port} as AuthoritativeClient");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.SendToClient:
            {
                var payload      = ResolveString(node, "Payload");
                var msgType      = node.Properties.GetValueOrDefault("MessageType", "server-direct");
                var playerIdStr  = ResolveString(node, "PlayerId");
                _log.Add($"[NET] → client {playerIdStr} [{msgType}]: {payload}");
                if (Network is not null && Guid.TryParse(playerIdStr, out var pid))
                    _ = Network.SendToClientAsync(pid, new NetworkMessage
                        { MessageType = msgType, Payload = payload });
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.OnClientConnected:
            case NodeType.OnClientDisconnected:
                // Event nodes — wired up via NetworkSessionManager.PlayerJoined / PlayerLeft.
                break;
            case NodeType.GetNetworkRole:
            {
                var session = ResolveAny(node, "Session") as NetworkSession
                    ?? Network?.Session;
                SetPortValue(node, "Role",
                    (session?.Role ?? NetworkRole.Peer).ToString());
                break;
            }

            // ── Morgue File ────────────────────────────────────────────────────
            case NodeType.GenerateMorgueFile:
            {
                var ent    = ResolveAny(node, "Entity") as Entity;
                var cause  = node.Properties.GetValueOrDefault("Cause", "Unknown cause");
                var dir    = node.Properties.GetValueOrDefault("Directory", "morgues");
                var morgue = MorgueFileWriter.BuildFromRunState(
                    player: ent,
                    cause: cause,
                    turnsPlayed: _tickCount,
                    visitedLocations: Overworld.ActiveOverworld?.Locations
                        .Where(l => l.HasBeenVisited).Select(l => l.Name),
                    notes: _log);
                var writer = new MorgueFileWriter();
                var path   = writer.WriteToFile(morgue, dir) ?? "(write failed)";
                SetPortValue(node, "FilePath", path);
                _log.Add($"[MORGUE] Written: {path}");
                ExecuteExecChain(node, "Exec");
                break;
            }
            case NodeType.OnPlayerDeath:
                // Event node — fired externally when player HP drops to 0.
                break;

            default:
                _log.Add($"[WARN] Unhandled node type: {node.Type}");
                break;
        }
    }

    private static Scene.SceneNode CreateSceneNodeByType(string typeName, string name) =>
        typeName switch
        {
            nameof(Scene.SpriteNode)   => new Scene.SpriteNode   { Name = name },
            nameof(Scene.EntityNode)   => new Scene.EntityNode    { Name = name },
            nameof(Scene.MapNode)      => new Scene.MapNode       { Name = name },
            nameof(Scene.LabelNode)    => new Scene.LabelNode     { Name = name },
            nameof(Scene.TimerNode)    => new Scene.TimerNode     { Name = name },
            nameof(Scene.AreaNode)     => new Scene.AreaNode      { Name = name },
            nameof(Scene.CameraNode)   => new Scene.CameraNode    { Name = name },
            _                          => new Scene.GridNode      { Name = name },
        };

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
        new(_log.AsReadOnly(), _activeMap, new List<Entity>(_entities), _inCutscene);
}

/// <summary>
/// Captures the results of a single <see cref="ScriptExecutor"/> run.
/// </summary>
/// <param name="Log">Execution log messages.</param>
/// <param name="Map">The active <see cref="AsciiMap"/> at the end of execution, if any.</param>
/// <param name="Entities">All live entities at the end of execution.</param>
/// <param name="IsInCutscene">
/// <see langword="true"/> if a cutscene is still active at the end of the run.
/// </param>
public sealed record ExecutionResult(
    IReadOnlyList<string> Log,
    AsciiMap? Map,
    IReadOnlyList<Entity> Entities,
    bool IsInCutscene = false);
