namespace RogueEngine.Core.Models;

/// <summary>
/// Represents a directed data-link between an output port on one node
/// and an input port on another node inside a <see cref="ScriptGraph"/>.
/// </summary>
public sealed class NodeConnection
{
    /// <summary>Unique identifier for this connection.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    // ── Source (output side) ──────────────────────────────────────────────────

    /// <summary>The node that produces the value.</summary>
    public Guid SourceNodeId { get; init; }

    /// <summary>The output port on the source node that provides the value.</summary>
    public Guid SourcePortId { get; init; }

    // ── Target (input side) ───────────────────────────────────────────────────

    /// <summary>The node that consumes the value.</summary>
    public Guid TargetNodeId { get; init; }

    /// <summary>The input port on the target node that receives the value.</summary>
    public Guid TargetPortId { get; init; }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{SourceNodeId}/{SourcePortId} → {TargetNodeId}/{TargetPortId}";
}
