using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace RogueEngine.Core.Models;

/// <summary>
/// A single node inside the visual script graph.
/// A node performs a discrete operation (e.g. add two numbers, generate a map,
/// display a menu) and exposes typed <see cref="Inputs"/> and <see cref="Outputs"/>
/// as connection endpoints.
/// </summary>
public sealed class ScriptNode
{
    /// <summary>Unique identifier used to reference this node in connections.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>What operation this node performs.</summary>
    public NodeType Type { get; init; }

    /// <summary>
    /// Human-readable title displayed on the node header.
    /// Defaults to the string representation of <see cref="Type"/>.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional user-supplied comment or description for this node instance.
    /// Shown as a tooltip in the editor.
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    // ── Canvas position ────────────────────────────────────────────────────────

    /// <summary>Horizontal position on the script-editor canvas (in pixels).</summary>
    public double X { get; set; }

    /// <summary>Vertical position on the script-editor canvas (in pixels).</summary>
    public double Y { get; set; }

    // ── Ports ──────────────────────────────────────────────────────────────────

    /// <summary>Input ports on this node (left side).</summary>
    public ObservableCollection<NodePort> Inputs { get; } = [];

    /// <summary>Output ports on this node (right side).</summary>
    public ObservableCollection<NodePort> Outputs { get; } = [];

    // ── Custom properties ──────────────────────────────────────────────────────

    /// <summary>
    /// Extra key-value properties used by specific node types
    /// (e.g. a VariableInt stores its value here under key "Value").
    /// </summary>
    public Dictionary<string, string> Properties { get; } = [];

    /// <inheritdoc/>
    public override string ToString() => $"[{Type}] {Title} ({Id})";
}
