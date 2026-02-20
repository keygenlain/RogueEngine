namespace RogueEngine.Core.Models;

/// <summary>
/// Specifies the data type flowing through a node port.
/// </summary>
public enum PortDataType
{
    /// <summary>Execution flow (white pin); no data is carried.</summary>
    Exec,
    /// <summary>Integer number.</summary>
    Int,
    /// <summary>Floating-point number.</summary>
    Float,
    /// <summary>Boolean (true/false).</summary>
    Bool,
    /// <summary>Text string.</summary>
    String,
    /// <summary>Reference to an <see cref="AsciiMap"/>.</summary>
    Map,
    /// <summary>Reference to an <see cref="Entity"/>.</summary>
    Entity,
    /// <summary>A single <see cref="AsciiCell"/>.</summary>
    Cell,
    /// <summary>Any type (for polymorphic ports).</summary>
    Any,
}
