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

        new ScriptExecutor(graph).Run();
        Assert.Equal(42, varNode.Outputs.First().RuntimeValue);
    }

    [Fact]
    public void VariableFloat_OutputsConfiguredValue()
    {
        var varNode = NodeFactory.Create(NodeType.VariableFloat);
        varNode.Properties["Value"] = "3.14";
        var graph = BuildGraphWith(varNode);
        new ScriptExecutor(graph).Run();
        Assert.Equal(3.14f, (float)(varNode.Outputs.First().RuntimeValue ?? 0f), 2);
    }

    [Fact]
    public void VariableBool_TrueString_OutputsTrue()
    {
        var varNode = NodeFactory.Create(NodeType.VariableBool);
        varNode.Properties["Value"] = "true";
        var graph = BuildGraphWith(varNode);
        new ScriptExecutor(graph).Run();
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
        var start = NodeFactory.Create(NodeType.Start);

        var graph = BuildGraphWith(start, a, b, add);

        // Connect a→A, b→B
        graph.Connect(a.Id, a.Outputs.First().Id, add.Id, add.Inputs.First(p => p.Name == "A").Id);
        graph.Connect(b.Id, b.Outputs.First().Id, add.Id, add.Inputs.First(p => p.Name == "B").Id);

        new ScriptExecutor(graph).Run();
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
        new ScriptExecutor(graph).Run();
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

        new ScriptExecutor(graph, seed: 123).Run();
        var v1 = (int?)rndNode.Outputs.First(p => p.Name == "Value").RuntimeValue ?? -1;

        // Re-run with same seed
        rndNode.Outputs.First().RuntimeValue = null;
        new ScriptExecutor(graph, seed: 123).Run();
        var v2 = (int?)rndNode.Outputs.First(p => p.Name == "Value").RuntimeValue ?? -2;

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
}
