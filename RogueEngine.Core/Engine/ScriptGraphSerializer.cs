using System.Text.Json;
using System.Text.Json.Serialization;
using RogueEngine.Core.Models;

namespace RogueEngine.Core.Engine;

/// <summary>
/// Serialises and deserialises <see cref="GameProject"/> objects to/from JSON.
/// All node port collections and connection collections are preserved so that
/// a project round-trips without data loss.
/// </summary>
public static class ScriptGraphSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // ── Serialise ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Serialises the <paramref name="project"/> to a JSON string.
    /// </summary>
    public static string Serialize(GameProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var dto = ProjectToDto(project);
        return JsonSerializer.Serialize(dto, _options);
    }

    /// <summary>
    /// Serialises the <paramref name="project"/> and writes it to
    /// <paramref name="filePath"/>.
    /// </summary>
    public static void SaveToFile(GameProject project, string filePath)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        File.WriteAllText(filePath, Serialize(project));
    }

    // ── Deserialise ────────────────────────────────────────────────────────────

    /// <summary>
    /// Deserialises a <see cref="GameProject"/> from a JSON string.
    /// </summary>
    /// <exception cref="JsonException">Thrown when the JSON is invalid.</exception>
    public static GameProject Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var dto = JsonSerializer.Deserialize<ProjectDto>(json, _options)
            ?? throw new JsonException("JSON deserialised to null.");
        return DtoToProject(dto);
    }

    /// <summary>
    /// Reads a <see cref="GameProject"/> from a JSON file at
    /// <paramref name="filePath"/>.
    /// </summary>
    public static GameProject LoadFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Deserialize(File.ReadAllText(filePath));
    }

    // ── DTO types ──────────────────────────────────────────────────────────────
    // These plain-data records avoid circular-reference issues and keep the
    // JSON schema stable even if the model classes grow extra fields.

    private sealed record ProjectDto(
        string Name, string Author, string Version, string Description,
        int DisplayWidth, int DisplayHeight,
        string FontFamily, string? CustomFontPath, int FontSizePx,
        Guid? StartGraphId,
        List<GraphDto> Graphs);

    private sealed record GraphDto(
        Guid Id, string Name, string Description,
        List<NodeDto> Nodes,
        List<ConnectionDto> Connections);

    private sealed record NodeDto(
        Guid Id, string Type, string Title, string Comment,
        double X, double Y,
        List<PortDto> Inputs,
        List<PortDto> Outputs,
        Dictionary<string, string> Properties);

    private sealed record PortDto(
        Guid Id, string Name, string DataType, bool IsInput, string DefaultValue);

    private sealed record ConnectionDto(
        Guid Id, Guid SourceNodeId, Guid SourcePortId,
        Guid TargetNodeId, Guid TargetPortId);

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private static ProjectDto ProjectToDto(GameProject p) => new(
        p.Name, p.Author, p.Version, p.Description,
        p.DisplayWidth, p.DisplayHeight,
        p.FontFamily, p.CustomFontPath, p.FontSizePx,
        p.StartGraphId,
        p.Graphs.Select(GraphToDto).ToList());

    private static GraphDto GraphToDto(ScriptGraph g) => new(
        g.Id, g.Name, g.Description,
        g.Nodes.Select(NodeToDto).ToList(),
        g.Connections.Select(ConnectionToDto).ToList());

    private static NodeDto NodeToDto(ScriptNode n) => new(
        n.Id, n.Type.ToString(), n.Title, n.Comment,
        n.X, n.Y,
        n.Inputs.Select(PortToDto).ToList(),
        n.Outputs.Select(PortToDto).ToList(),
        new Dictionary<string, string>(n.Properties));

    private static PortDto PortToDto(NodePort p) =>
        new(p.Id, p.Name, p.DataType.ToString(), p.IsInput, p.DefaultValue);

    private static ConnectionDto ConnectionToDto(NodeConnection c) =>
        new(c.Id, c.SourceNodeId, c.SourcePortId, c.TargetNodeId, c.TargetPortId);

    // ── DTO → model ────────────────────────────────────────────────────────────

    private static GameProject DtoToProject(ProjectDto dto)
    {
        var project = new GameProject
        {
            Name = dto.Name,
            Author = dto.Author,
            Version = dto.Version,
            Description = dto.Description,
            DisplayWidth = dto.DisplayWidth,
            DisplayHeight = dto.DisplayHeight,
            FontFamily = dto.FontFamily,
            CustomFontPath = dto.CustomFontPath,
            FontSizePx = dto.FontSizePx,
            StartGraphId = dto.StartGraphId,
        };
        project.Graphs.AddRange(dto.Graphs.Select(DtoToGraph));
        return project;
    }

    private static ScriptGraph DtoToGraph(GraphDto dto)
    {
        var graph = new ScriptGraph
        {
            // We can't set Id via init because it's init-only with Guid.NewGuid()
            // default – reconstruct via the GUID overload we provide.
            Name = dto.Name,
            Description = dto.Description,
        };
        // Restore Id using reflection-free approach: we keep a private backing field trick.
        // Since ScriptGraph.Id is { get; init; } we set it here through a helper.
        SetId(graph, dto.Id);

        foreach (var nd in dto.Nodes)
            graph.Nodes.Add(DtoToNode(nd));
        foreach (var cd in dto.Connections)
            graph.Connections.Add(DtoToConnection(cd));
        return graph;
    }

    private static ScriptNode DtoToNode(NodeDto dto)
    {
        if (!Enum.TryParse<NodeType>(dto.Type, out var nodeType))
            nodeType = NodeType.InlineExpression;

        var node = new ScriptNode
        {
            Type = nodeType,
            Title = dto.Title,
            Comment = dto.Comment,
            X = dto.X,
            Y = dto.Y,
        };
        SetId(node, dto.Id);
        foreach (var p in dto.Inputs)  node.Inputs.Add(DtoToPort(p));
        foreach (var p in dto.Outputs) node.Outputs.Add(DtoToPort(p));
        foreach (var kv in dto.Properties) node.Properties[kv.Key] = kv.Value;
        return node;
    }

    private static NodePort DtoToPort(PortDto dto)
    {
        if (!Enum.TryParse<PortDataType>(dto.DataType, out var dataType))
            dataType = PortDataType.Any;
        var port = new NodePort
        {
            Name = dto.Name,
            DataType = dataType,
            IsInput = dto.IsInput,
            DefaultValue = dto.DefaultValue,
        };
        SetId(port, dto.Id);
        return port;
    }

    private static NodeConnection DtoToConnection(ConnectionDto dto) => new()
    {
        SourceNodeId = dto.SourceNodeId,
        SourcePortId = dto.SourcePortId,
        TargetNodeId = dto.TargetNodeId,
        TargetPortId = dto.TargetPortId,
    };

    // ── Id restoration helper ──────────────────────────────────────────────────
    // The Id properties use { get; init; } and are set via object initialisers
    // in the model.  For deserialisation we use reflection to restore the
    // persisted Guid values so that connections still resolve after load.

    private static void SetId<T>(T obj, Guid id)
    {
        var prop = typeof(T).GetProperty("Id");
        prop?.SetValue(obj, id);
    }
}
