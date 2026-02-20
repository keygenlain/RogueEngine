using System.Collections.ObjectModel;

namespace RogueEngine.Core.Models;

/// <summary>
/// The complete visual script for a single game behaviour.
/// A <see cref="ScriptGraph"/> owns a collection of <see cref="ScriptNode"/> objects
/// and the <see cref="NodeConnection"/> objects that wire them together.
/// It can be serialised to / deserialised from JSON for persistence.
/// </summary>
public sealed class ScriptGraph
{
    /// <summary>Unique identifier for this graph.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Display name shown in the editor tab.</summary>
    public string Name { get; set; } = "Untitled Graph";

    /// <summary>Optional description of what this graph does.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>All nodes that belong to this graph.</summary>
    public ObservableCollection<ScriptNode> Nodes { get; } = [];

    /// <summary>All connections between node ports in this graph.</summary>
    public ObservableCollection<NodeConnection> Connections { get; } = [];

    // ── Helper methods ─────────────────────────────────────────────────────────

    /// <summary>
    /// Finds and returns the node with the given <paramref name="id"/>,
    /// or <see langword="null"/> if no such node exists.
    /// </summary>
    public ScriptNode? FindNode(Guid id) =>
        Nodes.FirstOrDefault(n => n.Id == id);

    /// <summary>
    /// Finds the single <em>Start</em> node in the graph, if one exists.
    /// Each graph should contain at most one Start node.
    /// </summary>
    public ScriptNode? FindStartNode() =>
        Nodes.FirstOrDefault(n => n.Type == NodeType.Start);

    /// <summary>
    /// Returns all connections whose source is the specified node.
    /// </summary>
    public IEnumerable<NodeConnection> GetOutgoingConnections(Guid nodeId) =>
        Connections.Where(c => c.SourceNodeId == nodeId);

    /// <summary>
    /// Returns all connections whose target is the specified node.
    /// </summary>
    public IEnumerable<NodeConnection> GetIncomingConnections(Guid nodeId) =>
        Connections.Where(c => c.TargetNodeId == nodeId);

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    public void AddNode(ScriptNode node) => Nodes.Add(node);

    /// <summary>
    /// Removes a node and all its associated connections from the graph.
    /// </summary>
    public void RemoveNode(Guid nodeId)
    {
        var node = FindNode(nodeId);
        if (node is null) return;

        // Remove every connection that touches this node.
        var toRemove = Connections
            .Where(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId)
            .ToList();
        foreach (var conn in toRemove)
            Connections.Remove(conn);

        Nodes.Remove(node);
    }

    /// <summary>
    /// Connects an output port of one node to an input port of another.
    /// Duplicate connections are silently ignored.
    /// </summary>
    /// <returns>
    /// The newly created <see cref="NodeConnection"/>, or
    /// <see langword="null"/> if the connection already exists.
    /// </returns>
    public NodeConnection? Connect(
        Guid sourceNodeId, Guid sourcePortId,
        Guid targetNodeId, Guid targetPortId)
    {
        // Prevent duplicates.
        if (Connections.Any(c =>
            c.SourceNodeId == sourceNodeId && c.SourcePortId == sourcePortId &&
            c.TargetNodeId == targetNodeId && c.TargetPortId == targetPortId))
            return null;

        var conn = new NodeConnection
        {
            SourceNodeId = sourceNodeId,
            SourcePortId = sourcePortId,
            TargetNodeId = targetNodeId,
            TargetPortId = targetPortId,
        };
        Connections.Add(conn);
        return conn;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"ScriptGraph '{Name}' ({Nodes.Count} nodes, {Connections.Count} connections)";
}
