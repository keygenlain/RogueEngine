using RogueEngine.Core.Engine;
using RogueEngine.Core.Models;

namespace RogueEngine.Tests;

/// <summary>
/// Tests for <see cref="ScriptExecutor"/> and the visual scripting system.
/// </summary>
public sealed class ScriptExecutorTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ScriptGraph BuildGraphWith(params ScriptNode[] nodes)
    {
        var graph = new ScriptGraph();
        foreach (var n in nodes)
            graph.AddNode(n);
        return graph;
    }

    private static (ScriptGraph graph, ScriptNode start, ScriptNode target)
        BuildExecChain(NodeType targetType)
    {
        var start = NodeFactory.Create(NodeType.Start, 0, 0);
        var target = NodeFactory.Create(targetType, 200, 0);
        var graph = BuildGraphWith(start, target);

        // Wire start Exec → target Exec
        var srcPort = start.Outputs.First(p => p.Name == "Exec");
        var dstPort = target.Inputs.FirstOrDefault(p => p.Name == "Exec");
        if (dstPort is not null)
            graph.Connect(start.Id, srcPort.Id, target.Id, dstPort.Id);

        return (graph, start, target);
    }

    /// <summary>
    /// Creates a VariableInt node with the given value, adds it to the graph,
    /// and returns it. Shared helper used across Roguelike Core tests.
    /// </summary>
    private static ScriptNode AddIntVar(ScriptGraph graph, string value)
    {
        var v = NodeFactory.Create(NodeType.VariableInt);
        v.Properties["Value"] = value;
        graph.AddNode(v);
        return v;
    }

    // ── Graph structure tests ──────────────────────────────────────────────────

    [Fact]
    public void ScriptGraph_AddAndRemoveNode_Works()
    {
        var graph = new ScriptGraph();
        var node = NodeFactory.Create(NodeType.VariableInt);
        graph.AddNode(node);
        Assert.Single(graph.Nodes);

        graph.RemoveNode(node.Id);
        Assert.Empty(graph.Nodes);
    }

    [Fact]
    public void ScriptGraph_Connect_PreventsDuplicates()
    {
        var graph = new ScriptGraph();
        var a = NodeFactory.Create(NodeType.VariableInt);
        var b = NodeFactory.Create(NodeType.MathAdd);
        graph.AddNode(a);
        graph.AddNode(b);

        var src = a.Outputs.First();
        var dst = b.Inputs.First();

        var c1 = graph.Connect(a.Id, src.Id, b.Id, dst.Id);
        var c2 = graph.Connect(a.Id, src.Id, b.Id, dst.Id);

        Assert.NotNull(c1);
        Assert.Null(c2); // duplicate → null
        Assert.Single(graph.Connections);
    }

    [Fact]
    public void ScriptGraph_RemoveNode_AlsoRemovesConnections()
    {
        var graph = new ScriptGraph();
        var a = NodeFactory.Create(NodeType.VariableInt);
        var b = NodeFactory.Create(NodeType.MathAdd);
        graph.AddNode(a);
        graph.AddNode(b);
        graph.Connect(a.Id, a.Outputs.First().Id, b.Id, b.Inputs.First().Id);
        Assert.Single(graph.Connections);

        graph.RemoveNode(a.Id);
        Assert.Empty(graph.Connections);
    }

    // ── Executor: no start node ────────────────────────────────────────────────

    [Fact]
    public void Executor_NoStartNode_LogsWarning()
    {
        var graph = new ScriptGraph();
        var result = new ScriptExecutor(graph).Run();
        Assert.Contains(result.Log, l => l.Contains("no Start node"));
    }

    // ── Variables ─────────────────────────────────────────────────────────────

    [Fact]
    public void VariableInt_OutputsConfiguredValue()
    {
        var varNode = NodeFactory.Create(NodeType.VariableInt);
        varNode.Properties["Value"] = "42";
        var graph = BuildGraphWith(varNode);

        new ScriptExecutor(graph).EvaluateNode(varNode);
        Assert.Equal(42, varNode.Outputs.First().RuntimeValue);
    }

    [Fact]
    public void VariableFloat_OutputsConfiguredValue()
    {
        var varNode = NodeFactory.Create(NodeType.VariableFloat);
        varNode.Properties["Value"] = "3.14";
        var graph = BuildGraphWith(varNode);
        new ScriptExecutor(graph).EvaluateNode(varNode);
        Assert.Equal(3.14f, (float)(varNode.Outputs.First().RuntimeValue ?? 0f), 2);
    }

    [Fact]
    public void VariableBool_TrueString_OutputsTrue()
    {
        var varNode = NodeFactory.Create(NodeType.VariableBool);
        varNode.Properties["Value"] = "true";
        var graph = BuildGraphWith(varNode);
        new ScriptExecutor(graph).EvaluateNode(varNode);
        Assert.Equal(true, varNode.Outputs.First().RuntimeValue);
    }

    // ── Math ───────────────────────────────────────────────────────────────────

    [Fact]
    public void MathAdd_WithConnectedInputs_ReturnsSum()
    {
        var a = NodeFactory.Create(NodeType.VariableFloat);
        a.Properties["Value"] = "10";
        var b = NodeFactory.Create(NodeType.VariableFloat);
        b.Properties["Value"] = "5";
        var add = NodeFactory.Create(NodeType.MathAdd);

        var graph = BuildGraphWith(a, b, add);

        // Connect a→A, b→B
        graph.Connect(a.Id, a.Outputs.First().Id, add.Id, add.Inputs.First(p => p.Name == "A").Id);
        graph.Connect(b.Id, b.Outputs.First().Id, add.Id, add.Inputs.First(p => p.Name == "B").Id);

        new ScriptExecutor(graph).EvaluateNode(add);
        Assert.Equal(15f, add.Outputs.First(p => p.Name == "Result").RuntimeValue);
    }

    [Fact]
    public void MathDivide_ByZero_ReturnsZero()
    {
        var a = NodeFactory.Create(NodeType.VariableFloat);
        a.Properties["Value"] = "10";
        var b = NodeFactory.Create(NodeType.VariableFloat);
        b.Properties["Value"] = "0";
        var div = NodeFactory.Create(NodeType.MathDivide);
        var graph = BuildGraphWith(a, b, div);
        graph.Connect(a.Id, a.Outputs.First().Id, div.Id, div.Inputs.First(p => p.Name == "A").Id);
        graph.Connect(b.Id, b.Outputs.First().Id, div.Id, div.Inputs.First(p => p.Name == "B").Id);
        new ScriptExecutor(graph).EvaluateNode(div);
        Assert.Equal(0f, div.Outputs.First(p => p.Name == "Result").RuntimeValue);
    }

    // ── Random ────────────────────────────────────────────────────────────────

    [Fact]
    public void RandomInt_WithSeed_IsConsistent()
    {
        var rndNode = NodeFactory.Create(NodeType.RandomInt);
        rndNode.Properties["Min"] = "1";
        rndNode.Properties["Max"] = "100";
        var graph = BuildGraphWith(rndNode);

        var exec1 = new ScriptExecutor(graph, seed: 123);
        exec1.EvaluateNode(rndNode);
        var v1 = (int)(rndNode.Outputs.First(p => p.Name == "Value").RuntimeValue ?? -1);

        // Re-run with same seed
        rndNode.Outputs.First().RuntimeValue = null;
        var exec2 = new ScriptExecutor(graph, seed: 123);
        exec2.EvaluateNode(rndNode);
        var v2 = (int)(rndNode.Outputs.First(p => p.Name == "Value").RuntimeValue ?? -2);

        Assert.Equal(v1, v2);
    }

    // ── Map generation via script ──────────────────────────────────────────────

    [Fact]
    public void Script_CreateMap_SetsActiveMap()
    {
        var (graph, start, createMap) = BuildExecChain(NodeType.CreateMap);
        createMap.Properties["Width"] = "20";
        createMap.Properties["Height"] = "10";
        // Wire width/height inputs from defaults (no connected nodes; uses props)

        var result = new ScriptExecutor(graph, seed: 0).Run();
        Assert.NotNull(result.Map);
        Assert.Equal(20, result.Map!.Width);
        Assert.Equal(10, result.Map.Height);
    }

    [Fact]
    public void Script_CreateMap_ThenCaveProcgen_ProducesMap()
    {
        var start = NodeFactory.Create(NodeType.Start);
        var createMap = NodeFactory.Create(NodeType.CreateMap);
        createMap.Properties["Width"] = "30";
        createMap.Properties["Height"] = "15";
        var cave = NodeFactory.Create(NodeType.GenerateCaveCellular);
        cave.Properties["FillRatio"] = "0.45";
        cave.Properties["Iterations"] = "3";

        var graph = BuildGraphWith(start, createMap, cave);

        // Exec chain: Start → CreateMap → Cave
        graph.Connect(start.Id,
            start.Outputs.First(p => p.Name == "Exec").Id,
            createMap.Id,
            createMap.Inputs.First(p => p.Name == "Exec").Id);

        graph.Connect(createMap.Id,
            createMap.Outputs.First(p => p.Name == "Exec").Id,
            cave.Id,
            cave.Inputs.First(p => p.Name == "Exec").Id);

        // Map data connection: CreateMap.Map → Cave.Map
        graph.Connect(createMap.Id,
            createMap.Outputs.First(p => p.Name == "Map").Id,
            cave.Id,
            cave.Inputs.First(p => p.Name == "Map").Id);

        var result = new ScriptExecutor(graph, seed: 77).Run();
        Assert.NotNull(result.Map);
        Assert.Equal(30, result.Map!.Width);
    }

    // ── Entity ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Script_SpawnEntity_CreatesEntity()
    {
        var (graph, start, spawn) = BuildExecChain(NodeType.SpawnEntity);
        spawn.Properties["Name"] = "Hero";
        spawn.Properties["Glyph"] = "@";

        var result = new ScriptExecutor(graph).Run();
        Assert.Single(result.Entities);
        Assert.Equal("Hero", result.Entities[0].Name);
        Assert.Equal('@', result.Entities[0].Glyph);
    }

    [Fact]
    public void Script_DestroyEntity_RemovesEntity()
    {
        var start = NodeFactory.Create(NodeType.Start);
        var spawn = NodeFactory.Create(NodeType.SpawnEntity);
        spawn.Properties["Name"] = "Goblin";
        var destroy = NodeFactory.Create(NodeType.DestroyEntity);

        var graph = BuildGraphWith(start, spawn, destroy);

        // Start → Spawn
        graph.Connect(start.Id, start.Outputs.First(p => p.Name == "Exec").Id,
            spawn.Id, spawn.Inputs.First(p => p.Name == "Exec").Id);

        // Spawn → Destroy (exec)
        graph.Connect(spawn.Id, spawn.Outputs.First(p => p.Name == "Exec").Id,
            destroy.Id, destroy.Inputs.First(p => p.Name == "Exec").Id);

        // Spawn.Entity → Destroy.Entity (data)
        graph.Connect(spawn.Id, spawn.Outputs.First(p => p.Name == "Entity").Id,
            destroy.Id, destroy.Inputs.First(p => p.Name == "Entity").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.Empty(result.Entities);
    }

    // ── Control flow ──────────────────────────────────────────────────────────

    [Fact]
    public void ForLoop_ExecutesCorrectNumberOfTimes()
    {
        var start = NodeFactory.Create(NodeType.Start);
        var loop = NodeFactory.Create(NodeType.ForLoop);
        loop.Properties["Count"] = "5";

        // Track how many times the loop body fires by counting spawn calls
        var spawn = NodeFactory.Create(NodeType.SpawnEntity);
        var graph = BuildGraphWith(start, loop, spawn);

        graph.Connect(start.Id, start.Outputs.First(p => p.Name == "Exec").Id,
            loop.Id, loop.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(loop.Id, loop.Outputs.First(p => p.Name == "Loop Body").Id,
            spawn.Id, spawn.Inputs.First(p => p.Name == "Exec").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.Equal(5, result.Entities.Count);
    }

    // ── NodeFactory ────────────────────────────────────────────────────────────

    [Fact]
    public void NodeFactory_AllDefinitions_HaveValidTitles()
    {
        foreach (var def in NodeFactory.AllDefinitions)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Title),
                $"Node {def.Type} has an empty title");
        }
    }

    [Fact]
    public void NodeFactory_Create_SetsCorrectType()
    {
        foreach (var def in NodeFactory.AllDefinitions)
        {
            var node = NodeFactory.Create(def.Type);
            Assert.Equal(def.Type, node.Type);
        }
    }

    // ── Roguelike Core ────────────────────────────────────────────────────────

    [Fact]
    public void ComputeFOV_ReturnsOriginInVisibleTiles()
    {
        var (graph, start, fov) = BuildExecChain(NodeType.ComputeFOV);
        fov.Properties["Radius"] = "3";

        // Route start → createMap → fov so _activeMap is populated
        var createMap = NodeFactory.Create(NodeType.CreateMap);
        createMap.Properties["Width"] = "10";
        createMap.Properties["Height"] = "10";
        graph.AddNode(createMap);

        var startExec = start.Outputs.First(p => p.Name == "Exec");
        var createExec = createMap.Inputs.First(p => p.Name == "Exec");
        var createOut  = createMap.Outputs.First(p => p.Name == "Exec");
        var fovExec    = fov.Inputs.First(p => p.Name == "Exec");
        graph.Connect(start.Id, startExec.Id, createMap.Id, createExec.Id);
        graph.Connect(createMap.Id, createOut.Id, fov.Id, fovExec.Id);

        // Wire OriginX=5, OriginY=5
        var varX = NodeFactory.Create(NodeType.VariableInt);
        varX.Properties["Value"] = "5";
        var varY = NodeFactory.Create(NodeType.VariableInt);
        varY.Properties["Value"] = "5";
        graph.AddNode(varX);
        graph.AddNode(varY);
        graph.Connect(varX.Id, varX.Outputs.First(p => p.Name == "Value").Id,
                      fov.Id,  fov.Inputs.First(p => p.Name == "OriginX").Id);
        graph.Connect(varY.Id, varY.Outputs.First(p => p.Name == "Value").Id,
                      fov.Id,  fov.Inputs.First(p => p.Name == "OriginY").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: ComputeFOV"));
    }

    [Fact]
    public void ComputeFOV_NullMap_ReturnsOnlyOrigin()
    {
        var (graph, start, fov) = BuildExecChain(NodeType.ComputeFOV);
        fov.Properties["Radius"] = "5";

        var varX = NodeFactory.Create(NodeType.VariableInt);
        varX.Properties["Value"] = "3";
        var varY = NodeFactory.Create(NodeType.VariableInt);
        varY.Properties["Value"] = "3";
        graph.AddNode(varX);
        graph.AddNode(varY);
        graph.Connect(varX.Id, varX.Outputs.First(p => p.Name == "Value").Id,
                      fov.Id, fov.Inputs.First(p => p.Name == "OriginX").Id);
        graph.Connect(varY.Id, varY.Outputs.First(p => p.Name == "Value").Id,
                      fov.Id, fov.Inputs.First(p => p.Name == "OriginY").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: ComputeFOV"));
    }

    [Fact]
    public void FindPathAStar_OpenMap_FindsPath()
    {
        // 5×5 open map, path from (0,0) to (4,4) should succeed
        var (graph, start, pathNode) = BuildExecChain(NodeType.FindPathAStar);

        var createMap = NodeFactory.Create(NodeType.CreateMap);
        createMap.Properties["Width"] = "5";
        createMap.Properties["Height"] = "5";
        graph.AddNode(createMap);

        var startExec = start.Outputs.First(p => p.Name == "Exec");
        var createExec = createMap.Inputs.First(p => p.Name == "Exec");
        var createOut = createMap.Outputs.First(p => p.Name == "Exec");
        var pathExec = pathNode.Inputs.First(p => p.Name == "Exec");
        graph.Connect(start.Id, startExec.Id, createMap.Id, createExec.Id);
        graph.Connect(createMap.Id, createOut.Id, pathNode.Id, pathExec.Id);

        // EndX=4, EndY=4
        var varEx = NodeFactory.Create(NodeType.VariableInt);
        varEx.Properties["Value"] = "4";
        var varEy = NodeFactory.Create(NodeType.VariableInt);
        varEy.Properties["Value"] = "4";
        graph.AddNode(varEx);
        graph.AddNode(varEy);
        graph.Connect(varEx.Id, varEx.Outputs.First(p => p.Name == "Value").Id,
                      pathNode.Id, pathNode.Inputs.First(p => p.Name == "EndX").Id);
        graph.Connect(varEy.Id, varEy.Outputs.First(p => p.Name == "Value").Id,
                      pathNode.Id, pathNode.Inputs.First(p => p.Name == "EndY").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: FindPathAStar"));
    }

    [Fact]
    public void FindPathAStar_NullMap_ReturnsFalse()
    {
        // Without a map the node should set Success=false and not crash
        var (graph, start, pathNode) = BuildExecChain(NodeType.FindPathAStar);
        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: FindPathAStar"));
    }

    [Fact]
    public void GetEntityStat_KnownStat_ReturnsValue()
    {
        var spawnNode = NodeFactory.Create(NodeType.SpawnEntity);
        spawnNode.Properties["Name"] = "Hero";
        var statNode = NodeFactory.Create(NodeType.GetEntityStat);
        var start = NodeFactory.Create(NodeType.Start);
        var graph = BuildGraphWith(start, spawnNode, statNode);

        // Wire: start → spawnNode (exec)
        graph.Connect(start.Id, start.Outputs.First(p => p.Name == "Exec").Id,
                      spawnNode.Id, spawnNode.Inputs.First(p => p.Name == "Exec").Id);
        // Wire entity output → GetEntityStat entity input
        graph.Connect(spawnNode.Id, spawnNode.Outputs.First(p => p.Name == "Entity").Id,
                      statNode.Id, statNode.Inputs.First(p => p.Name == "Entity").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: GetEntityStat"));
    }

    [Fact]
    public void WaitForInput_WithInjectedKey_OutputsKey()
    {
        var (graph, start, waitNode) = BuildExecChain(NodeType.WaitForInput);
        var executor = new ScriptExecutor(graph);
        executor.InjectKeyPress("ArrowUp");
        var result = executor.Run();

        Assert.Contains(result.Log, l => l.Contains("[WAIT_FOR_INPUT] Key: ArrowUp"));
    }

    [Fact]
    public void WaitForInput_NoInjectedKey_LogsNone()
    {
        var (graph, start, waitNode) = BuildExecChain(NodeType.WaitForInput);
        var result = new ScriptExecutor(graph).Run();

        Assert.Contains(result.Log, l => l.Contains("[WAIT_FOR_INPUT] Key: (none)"));
    }

    [Fact]
    public void NodeFactory_RoguelikeCoreNodes_HaveCorrectCategory()
    {
        var roguelikeCoreTypes = new[]
        {
            NodeType.ComputeFOV,
            NodeType.FindPathAStar,
            NodeType.GetEntityStat,
            NodeType.WaitForInput,
            NodeType.CheckEntityBump,
            NodeType.GetEntityType,
            NodeType.SetEntityStat,
            NodeType.ModifyEntityStat,
            NodeType.GetEntitiesAtTile,
        };

        foreach (var type in roguelikeCoreTypes)
        {
            var def = NodeFactory.AllDefinitions.FirstOrDefault(d => d.Type == type);
            Assert.NotNull(def);
            Assert.Equal("Roguelike Core", def.Category);
        }
    }

    // ── CheckEntityBump ───────────────────────────────────────────────────────

    [Fact]
    public void CheckEntityBump_BlockedByOtherEntity_ReturnsTrue()
    {
        // Spawn two entities: player at (1,1), enemy at (2,1).
        // Moving player right (DX=1, DY=0) should detect the enemy.
        var start   = NodeFactory.Create(NodeType.Start);
        var spawnP  = NodeFactory.Create(NodeType.SpawnEntity);
        spawnP.Properties["Name"] = "Player";
        var spawnE  = NodeFactory.Create(NodeType.SpawnEntity);
        spawnE.Properties["Name"] = "Enemy";
        var bumpNode = NodeFactory.Create(NodeType.CheckEntityBump);

        var graph = BuildGraphWith(start, spawnP, spawnE, bumpNode);

        // Exec chain: start → spawnPlayer → spawnEnemy
        graph.Connect(start.Id, start.Outputs.First(p => p.Name == "Exec").Id,
                      spawnP.Id, spawnP.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(spawnP.Id, spawnP.Outputs.First(p => p.Name == "Exec").Id,
                      spawnE.Id, spawnE.Inputs.First(p => p.Name == "Exec").Id);

        // Player at (1,1)
        var pxNode = AddIntVar(graph, "1"); var pyNode = AddIntVar(graph, "1");
        graph.Connect(pxNode.Id, pxNode.Outputs.First(p => p.Name == "Value").Id,
                      spawnP.Id, spawnP.Inputs.First(p => p.Name == "X").Id);
        graph.Connect(pyNode.Id, pyNode.Outputs.First(p => p.Name == "Value").Id,
                      spawnP.Id, spawnP.Inputs.First(p => p.Name == "Y").Id);

        // Enemy at (2,1)
        var exNode = AddIntVar(graph, "2"); var eyNode = AddIntVar(graph, "1");
        graph.Connect(exNode.Id, exNode.Outputs.First(p => p.Name == "Value").Id,
                      spawnE.Id, spawnE.Inputs.First(p => p.Name == "X").Id);
        graph.Connect(eyNode.Id, eyNode.Outputs.First(p => p.Name == "Value").Id,
                      spawnE.Id, spawnE.Inputs.First(p => p.Name == "Y").Id);

        // Wire player entity → bump node
        graph.Connect(spawnP.Id, spawnP.Outputs.First(p => p.Name == "Entity").Id,
                      bumpNode.Id, bumpNode.Inputs.First(p => p.Name == "Entity").Id);

        // DX=1, DY=0
        var dxNode = AddIntVar(graph, "1"); var dyNode = AddIntVar(graph, "0");
        graph.Connect(dxNode.Id, dxNode.Outputs.First(p => p.Name == "Value").Id,
                      bumpNode.Id, bumpNode.Inputs.First(p => p.Name == "DX").Id);
        graph.Connect(dyNode.Id, dyNode.Outputs.First(p => p.Name == "Value").Id,
                      bumpNode.Id, bumpNode.Inputs.First(p => p.Name == "DY").Id);

        var executor = new ScriptExecutor(graph);
        executor.Run();
        executor.EvaluateNode(bumpNode);

        // The node should have set Blocked = true
        Assert.DoesNotContain(
            executor.Run().Log, l => l.Contains("Unhandled node type: CheckEntityBump"));
    }

    [Fact]
    public void CheckEntityBump_NoEntityAtTarget_ReturnsFalse()
    {
        // Spawn a single entity. Moving in any direction should not be blocked.
        var (graph, start, bumpNode) = BuildExecChain(NodeType.CheckEntityBump);
        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: CheckEntityBump"));
    }

    // ── GetEntityType ─────────────────────────────────────────────────────────

    [Fact]
    public void GetEntityType_EntityWithTypeProperty_ReturnsType()
    {
        var start     = NodeFactory.Create(NodeType.Start);
        var spawnNode = NodeFactory.Create(NodeType.SpawnEntity);
        var typeNode  = NodeFactory.Create(NodeType.GetEntityType);
        var graph = BuildGraphWith(start, spawnNode, typeNode);

        graph.Connect(start.Id, start.Outputs.First(p => p.Name == "Exec").Id,
                      spawnNode.Id, spawnNode.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(spawnNode.Id, spawnNode.Outputs.First(p => p.Name == "Entity").Id,
                      typeNode.Id, typeNode.Inputs.First(p => p.Name == "Entity").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: GetEntityType"));
    }

    // ── SetEntityStat ─────────────────────────────────────────────────────────

    [Fact]
    public void SetEntityStat_WritesPropertyOnEntity()
    {
        var start     = NodeFactory.Create(NodeType.Start);
        var spawnNode = NodeFactory.Create(NodeType.SpawnEntity);
        var setNode   = NodeFactory.Create(NodeType.SetEntityStat);
        var graph = BuildGraphWith(start, spawnNode, setNode);

        graph.Connect(start.Id, start.Outputs.First(p => p.Name == "Exec").Id,
                      spawnNode.Id, spawnNode.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(spawnNode.Id, spawnNode.Outputs.First(p => p.Name == "Exec").Id,
                      setNode.Id, setNode.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(spawnNode.Id, spawnNode.Outputs.First(p => p.Name == "Entity").Id,
                      setNode.Id, setNode.Inputs.First(p => p.Name == "Entity").Id);

        // StatName = "HP", Value supplied via VariableInt = 10
        var statNameVar = NodeFactory.Create(NodeType.VariableString);
        statNameVar.Properties["Value"] = "HP";
        graph.AddNode(statNameVar);
        graph.Connect(statNameVar.Id, statNameVar.Outputs.First(p => p.Name == "Value").Id,
                      setNode.Id, setNode.Inputs.First(p => p.Name == "StatName").Id);

        var valueVar = NodeFactory.Create(NodeType.VariableInt);
        valueVar.Properties["Value"] = "10";
        graph.AddNode(valueVar);
        graph.Connect(valueVar.Id, valueVar.Outputs.First(p => p.Name == "Value").Id,
                      setNode.Id, setNode.Inputs.First(p => p.Name == "Value").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: SetEntityStat"));
        // The spawned entity should have HP set to "10"
        Assert.Single(result.Entities);
        Assert.Equal("10", result.Entities[0].Properties["HP"]);
    }

    // ── ModifyEntityStat ──────────────────────────────────────────────────────

    [Fact]
    public void ModifyEntityStat_AddAmount_UpdatesStatCorrectly()
    {
        var start     = NodeFactory.Create(NodeType.Start);
        var spawnNode = NodeFactory.Create(NodeType.SpawnEntity);
        var setNode   = NodeFactory.Create(NodeType.SetEntityStat);   // set HP=10 first
        var modNode   = NodeFactory.Create(NodeType.ModifyEntityStat); // then HP += 5
        modNode.Properties["Operator"] = "+";
        var graph = BuildGraphWith(start, spawnNode, setNode, modNode);

        // Exec chain
        graph.Connect(start.Id,    start.Outputs.First(p => p.Name == "Exec").Id,
                      spawnNode.Id, spawnNode.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(spawnNode.Id, spawnNode.Outputs.First(p => p.Name == "Exec").Id,
                      setNode.Id,  setNode.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(setNode.Id,  setNode.Outputs.First(p => p.Name == "Exec").Id,
                      modNode.Id,  modNode.Inputs.First(p => p.Name == "Exec").Id);

        // Entity wire through all three nodes
        graph.Connect(spawnNode.Id, spawnNode.Outputs.First(p => p.Name == "Entity").Id,
                      setNode.Id, setNode.Inputs.First(p => p.Name == "Entity").Id);
        graph.Connect(spawnNode.Id, spawnNode.Outputs.First(p => p.Name == "Entity").Id,
                      modNode.Id, modNode.Inputs.First(p => p.Name == "Entity").Id);

        // setNode: StatName="HP", Value=10
        var snVar1 = NodeFactory.Create(NodeType.VariableString); snVar1.Properties["Value"] = "HP"; graph.AddNode(snVar1);
        graph.Connect(snVar1.Id, snVar1.Outputs.First(p => p.Name == "Value").Id,
                      setNode.Id, setNode.Inputs.First(p => p.Name == "StatName").Id);
        var valVar = NodeFactory.Create(NodeType.VariableInt); valVar.Properties["Value"] = "10"; graph.AddNode(valVar);
        graph.Connect(valVar.Id, valVar.Outputs.First(p => p.Name == "Value").Id,
                      setNode.Id, setNode.Inputs.First(p => p.Name == "Value").Id);

        // modNode: StatName="HP", Amount=5
        var snVar2 = NodeFactory.Create(NodeType.VariableString); snVar2.Properties["Value"] = "HP"; graph.AddNode(snVar2);
        graph.Connect(snVar2.Id, snVar2.Outputs.First(p => p.Name == "Value").Id,
                      modNode.Id, modNode.Inputs.First(p => p.Name == "StatName").Id);
        var amtVar = NodeFactory.Create(NodeType.VariableFloat); amtVar.Properties["Value"] = "5"; graph.AddNode(amtVar);
        graph.Connect(amtVar.Id, amtVar.Outputs.First(p => p.Name == "Value").Id,
                      modNode.Id, modNode.Inputs.First(p => p.Name == "Amount").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: ModifyEntityStat"));
        Assert.Single(result.Entities);
        Assert.Equal("15", result.Entities[0].Properties["HP"]);
    }

    [Fact]
    public void ModifyEntityStat_MultiplyAmount_UpdatesStatCorrectly()
    {
        var start     = NodeFactory.Create(NodeType.Start);
        var spawnNode = NodeFactory.Create(NodeType.SpawnEntity);
        var setNode   = NodeFactory.Create(NodeType.SetEntityStat);
        var modNode   = NodeFactory.Create(NodeType.ModifyEntityStat);
        modNode.Properties["Operator"] = "*";
        var graph = BuildGraphWith(start, spawnNode, setNode, modNode);

        graph.Connect(start.Id,    start.Outputs.First(p => p.Name == "Exec").Id,
                      spawnNode.Id, spawnNode.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(spawnNode.Id, spawnNode.Outputs.First(p => p.Name == "Exec").Id,
                      setNode.Id,  setNode.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(setNode.Id,  setNode.Outputs.First(p => p.Name == "Exec").Id,
                      modNode.Id,  modNode.Inputs.First(p => p.Name == "Exec").Id);

        graph.Connect(spawnNode.Id, spawnNode.Outputs.First(p => p.Name == "Entity").Id,
                      setNode.Id, setNode.Inputs.First(p => p.Name == "Entity").Id);
        graph.Connect(spawnNode.Id, spawnNode.Outputs.First(p => p.Name == "Entity").Id,
                      modNode.Id, modNode.Inputs.First(p => p.Name == "Entity").Id);

        var snVar = NodeFactory.Create(NodeType.VariableString); snVar.Properties["Value"] = "ATK"; graph.AddNode(snVar);
        graph.Connect(snVar.Id, snVar.Outputs.First(p => p.Name == "Value").Id,
                      setNode.Id, setNode.Inputs.First(p => p.Name == "StatName").Id);
        var valVar = NodeFactory.Create(NodeType.VariableInt); valVar.Properties["Value"] = "4"; graph.AddNode(valVar);
        graph.Connect(valVar.Id, valVar.Outputs.First(p => p.Name == "Value").Id,
                      setNode.Id, setNode.Inputs.First(p => p.Name == "Value").Id);

        var snVar2 = NodeFactory.Create(NodeType.VariableString); snVar2.Properties["Value"] = "ATK"; graph.AddNode(snVar2);
        graph.Connect(snVar2.Id, snVar2.Outputs.First(p => p.Name == "Value").Id,
                      modNode.Id, modNode.Inputs.First(p => p.Name == "StatName").Id);
        var amtVar = NodeFactory.Create(NodeType.VariableFloat); amtVar.Properties["Value"] = "3"; graph.AddNode(amtVar);
        graph.Connect(amtVar.Id, amtVar.Outputs.First(p => p.Name == "Value").Id,
                      modNode.Id, modNode.Inputs.First(p => p.Name == "Amount").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: ModifyEntityStat"));
        Assert.Single(result.Entities);
        Assert.Equal("12", result.Entities[0].Properties["ATK"]);
    }

    // ── GetEntitiesAtTile ─────────────────────────────────────────────────────

    [Fact]
    public void GetEntitiesAtTile_TwoEntitiesOnSameTile_ReturnsCount2()
    {
        var start  = NodeFactory.Create(NodeType.Start);
        var spawn1 = NodeFactory.Create(NodeType.SpawnEntity);
        var spawn2 = NodeFactory.Create(NodeType.SpawnEntity);
        var getTile = NodeFactory.Create(NodeType.GetEntitiesAtTile);
        var graph = BuildGraphWith(start, spawn1, spawn2, getTile);

        graph.Connect(start.Id,  start.Outputs.First(p => p.Name == "Exec").Id,
                      spawn1.Id, spawn1.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(spawn1.Id, spawn1.Outputs.First(p => p.Name == "Exec").Id,
                      spawn2.Id, spawn2.Inputs.First(p => p.Name == "Exec").Id);

        // Both entities spawn at default (0,0); GetEntitiesAtTile(0,0)
        var result = new ScriptExecutor(graph).Run();
        Assert.DoesNotContain(result.Log, l => l.Contains("Unhandled node type: GetEntitiesAtTile"));
    }
}
