namespace RogueEngine.Core.Models;

/// <summary>
/// Defines all metadata needed to instantiate a node of a given <see cref="NodeType"/>.
/// Used by the editor palette and by <see cref="NodeFactory"/> to build ready-to-use
/// <see cref="ScriptNode"/> objects.
/// </summary>
public sealed class NodeDefinition
{
    /// <summary>The node type this definition describes.</summary>
    public NodeType Type { get; init; }

    /// <summary>Display title shown on the node header.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Brief description shown in the palette tooltip.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Category used to group nodes in the palette.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Port templates for the input side of the node.</summary>
    public List<(string Name, PortDataType DataType)> InputPorts { get; init; } = [];

    /// <summary>Port templates for the output side of the node.</summary>
    public List<(string Name, PortDataType DataType)> OutputPorts { get; init; } = [];

    /// <summary>
    /// Default property values keyed by property name.
    /// Copied into every newly created node instance.
    /// </summary>
    public Dictionary<string, string> DefaultProperties { get; init; } = [];
}
