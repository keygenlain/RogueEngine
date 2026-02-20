namespace RogueEngine.Core.Models;

/// <summary>
/// Represents a single input or output connector on a <see cref="ScriptNode"/>.
/// Ports carry typed data between nodes and are the endpoints of
/// <see cref="NodeConnection"/> objects.
/// </summary>
public sealed class NodePort
{
    /// <summary>Unique identifier for this port within the owning node.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Human-readable name displayed on the node (e.g. "Value", "In", "Out").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The data type this port accepts or emits.</summary>
    public PortDataType DataType { get; init; } = PortDataType.Any;

    /// <summary>
    /// <see langword="true"/> when this port receives data (left side of node);
    /// <see langword="false"/> when it produces data (right side of node).
    /// </summary>
    public bool IsInput { get; init; }

    /// <summary>
    /// Default value used when no connection is present.
    /// Stored as a string and parsed at runtime.
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// The resolved runtime value after script execution.
    /// This property is populated by <see cref="Engine.ScriptExecutor"/> at run time.
    /// </summary>
    public object? RuntimeValue { get; set; }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{(IsInput ? "In" : "Out")}:{Name}({DataType})";
}
