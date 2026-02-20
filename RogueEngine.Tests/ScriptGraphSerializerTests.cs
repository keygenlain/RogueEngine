using System.Text.Json;
using RogueEngine.Core.Engine;
using RogueEngine.Core.Models;

namespace RogueEngine.Tests;

/// <summary>
/// Round-trip tests for <see cref="ScriptGraphSerializer"/> (project JSON).
/// </summary>
public sealed class ScriptGraphSerializerTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static GameProject BuildSampleProject()
    {
        var project = new GameProject
        {
            Name        = "Test Roguelike",
            Author      = "Test Author",
            Version     = "2.0.0",
            Description = "A test game.",
            DisplayWidth  = 80,
            DisplayHeight = 25,
            FontFamily  = "Consolas",
            FontSizePx  = 14,
        };

        var graph = new ScriptGraph { Name = "Main Graph" };

        var start = NodeFactory.Create(NodeType.Start, 10, 20);
        var varInt = NodeFactory.Create(NodeType.VariableInt, 200, 20);
        varInt.Properties["Value"] = "42";
        var add = NodeFactory.Create(NodeType.MathAdd, 400, 20);

        graph.AddNode(start);
        graph.AddNode(varInt);
        graph.AddNode(add);

        // Connect varInt.Value → add.A
        graph.Connect(varInt.Id, varInt.Outputs.First().Id,
                      add.Id,   add.Inputs.First(p => p.Name == "A").Id);

        project.Graphs.Add(graph);
        project.StartGraphId = graph.Id;
        return project;
    }

    // ── Basic validity ─────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var json = ScriptGraphSerializer.Serialize(BuildSampleProject());
        var doc  = JsonDocument.Parse(json); // throws if invalid
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void Serialize_IsHumanReadable_ContainsNodeTitle()
    {
        var json = ScriptGraphSerializer.Serialize(BuildSampleProject());
        Assert.Contains("Start", json);
        Assert.Contains("Main Graph", json);
    }

    // ── Round-trip: project metadata ───────────────────────────────────────────

    [Fact]
    public void ProjectMetadata_RoundTrips()
    {
        var original = BuildSampleProject();
        var json     = ScriptGraphSerializer.Serialize(original);
        var loaded   = ScriptGraphSerializer.Deserialize(json);

        Assert.Equal(original.Name,          loaded.Name);
        Assert.Equal(original.Author,        loaded.Author);
        Assert.Equal(original.Version,       loaded.Version);
        Assert.Equal(original.Description,   loaded.Description);
        Assert.Equal(original.DisplayWidth,  loaded.DisplayWidth);
        Assert.Equal(original.DisplayHeight, loaded.DisplayHeight);
        Assert.Equal(original.FontFamily,    loaded.FontFamily);
        Assert.Equal(original.FontSizePx,    loaded.FontSizePx);
        Assert.Equal(original.StartGraphId,  loaded.StartGraphId);
    }

    // ── Round-trip: graphs and nodes ───────────────────────────────────────────

    [Fact]
    public void GraphCount_Preserved()
    {
        var original = BuildSampleProject();
        var loaded   = ScriptGraphSerializer.Deserialize(ScriptGraphSerializer.Serialize(original));
        Assert.Equal(original.Graphs.Count, loaded.Graphs.Count);
    }

    [Fact]
    public void GraphName_Preserved()
    {
        var original = BuildSampleProject();
        var loaded   = ScriptGraphSerializer.Deserialize(ScriptGraphSerializer.Serialize(original));
        Assert.Equal("Main Graph", loaded.Graphs[0].Name);
    }

    [Fact]
    public void NodeCount_Preserved()
    {
        var original = BuildSampleProject();
        var loaded   = ScriptGraphSerializer.Deserialize(ScriptGraphSerializer.Serialize(original));
        Assert.Equal(
            original.Graphs[0].Nodes.Count,
            loaded.Graphs[0].Nodes.Count);
    }

    [Fact]
    public void NodeTypes_Preserved()
    {
        var original = BuildSampleProject();
        var loaded   = ScriptGraphSerializer.Deserialize(ScriptGraphSerializer.Serialize(original));

        var origTypes   = original.Graphs[0].Nodes.Select(n => n.Type).OrderBy(t => t).ToList();
        var loadedTypes = loaded.Graphs[0].Nodes.Select(n => n.Type).OrderBy(t => t).ToList();
        Assert.Equal(origTypes, loadedTypes);
    }

    [Fact]
    public void NodeIds_Preserved()
    {
        var original = BuildSampleProject();
        var loaded   = ScriptGraphSerializer.Deserialize(ScriptGraphSerializer.Serialize(original));

        var origIds   = original.Graphs[0].Nodes.Select(n => n.Id).OrderBy(id => id).ToList();
        var loadedIds = loaded.Graphs[0].Nodes.Select(n => n.Id).OrderBy(id => id).ToList();
        Assert.Equal(origIds, loadedIds);
    }

    [Fact]
    public void NodePosition_Preserved()
    {
        var original = BuildSampleProject();
        var loaded   = ScriptGraphSerializer.Deserialize(ScriptGraphSerializer.Serialize(original));

        var origStart   = original.Graphs[0].Nodes.First(n => n.Type == NodeType.Start);
        var loadedStart = loaded.Graphs[0].Nodes.First(n => n.Type == NodeType.Start);
        Assert.Equal(origStart.X, loadedStart.X);
        Assert.Equal(origStart.Y, loadedStart.Y);
    }

    [Fact]
    public void NodeProperties_Preserved()
    {
        var original = BuildSampleProject();
        var loaded   = ScriptGraphSerializer.Deserialize(ScriptGraphSerializer.Serialize(original));

        var origVar   = original.Graphs[0].Nodes.First(n => n.Type == NodeType.VariableInt);
        var loadedVar = loaded.Graphs[0].Nodes.First(n => n.Type == NodeType.VariableInt);
        Assert.Equal("42", loadedVar.Properties["Value"]);
    }

    // ── Round-trip: connections ────────────────────────────────────────────────

    [Fact]
    public void ConnectionCount_Preserved()
    {
        var original = BuildSampleProject();
        var loaded   = ScriptGraphSerializer.Deserialize(ScriptGraphSerializer.Serialize(original));
        Assert.Equal(
            original.Graphs[0].Connections.Count,
            loaded.Graphs[0].Connections.Count);
    }

    [Fact]
    public void ConnectionEndpoints_Preserved()
    {
        var original = BuildSampleProject();
        var loaded   = ScriptGraphSerializer.Deserialize(ScriptGraphSerializer.Serialize(original));

        var origConn   = original.Graphs[0].Connections[0];
        var loadedConn = loaded.Graphs[0].Connections[0];

        Assert.Equal(origConn.SourceNodeId, loadedConn.SourceNodeId);
        Assert.Equal(origConn.SourcePortId, loadedConn.SourcePortId);
        Assert.Equal(origConn.TargetNodeId, loadedConn.TargetNodeId);
        Assert.Equal(origConn.TargetPortId, loadedConn.TargetPortId);
    }

    // ── Ports ──────────────────────────────────────────────────────────────────

    [Fact]
    public void PortIds_Preserved_SoConnectionsResolveAfterLoad()
    {
        var original = BuildSampleProject();
        var json     = ScriptGraphSerializer.Serialize(original);
        var loaded   = ScriptGraphSerializer.Deserialize(json);

        // The connection's TargetPortId must exist in the loaded add-node's inputs.
        var conn   = loaded.Graphs[0].Connections[0];
        var addNode = loaded.Graphs[0].FindNode(conn.TargetNodeId)!;
        var port   = addNode.Inputs.FirstOrDefault(p => p.Id == conn.TargetPortId);
        Assert.NotNull(port);
        Assert.Equal("A", port!.Name);
    }

    // ── File I/O ───────────────────────────────────────────────────────────────

    [Fact]
    public void SaveAndLoadFromFile_RoundTrips()
    {
        var tmpPath = Path.Combine(Path.GetTempPath(), $"rg_{Guid.NewGuid():N}.rogue");
        try
        {
            var original = BuildSampleProject();
            ScriptGraphSerializer.SaveToFile(original, tmpPath);
            var loaded = ScriptGraphSerializer.LoadFromFile(tmpPath);

            Assert.Equal(original.Name, loaded.Name);
            Assert.Equal(original.Graphs[0].Nodes.Count, loaded.Graphs[0].Nodes.Count);
        }
        finally
        {
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
        }
    }

    // ── Edge cases ─────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyProject_RoundTrips()
    {
        var empty = new GameProject { Name = "Empty" };
        var json  = ScriptGraphSerializer.Serialize(empty);
        var loaded = ScriptGraphSerializer.Deserialize(json);

        Assert.Equal("Empty", loaded.Name);
        Assert.Empty(loaded.Graphs);
    }

    [Fact]
    public void AllBuiltinNodeTypes_SerializeWithoutError()
    {
        var project = new GameProject { Name = "AllNodes" };
        var graph   = new ScriptGraph { Name = "G" };

        // Place every registered node type into the graph.
        var x = 0.0;
        foreach (var def in NodeFactory.AllDefinitions)
        {
            var node = NodeFactory.Create(def.Type, x, 0);
            graph.AddNode(node);
            x += 150;
        }

        project.Graphs.Add(graph);

        var json   = ScriptGraphSerializer.Serialize(project);
        var loaded = ScriptGraphSerializer.Deserialize(json);

        Assert.Equal(graph.Nodes.Count, loaded.Graphs[0].Nodes.Count);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsJsonException()
    {
        Assert.Throws<JsonException>(() =>
            ScriptGraphSerializer.Deserialize("{ not valid }"));
    }
}
