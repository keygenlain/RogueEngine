using RogueEngine.Core.Models;

namespace RogueEngine.Core.Engine;

/// <summary>
/// Central registry of all built-in node definitions and factory for
/// creating ready-to-use <see cref="ScriptNode"/> instances.
/// </summary>
public static class NodeFactory
{
    // ── Registry ───────────────────────────────────────────────────────────────

    private static readonly Dictionary<NodeType, NodeDefinition> _definitions =
        BuildDefinitions()
        .ToDictionary(d => d.Type);

    /// <summary>
    /// Returns all registered <see cref="NodeDefinition"/> objects,
    /// ordered by category then title.
    /// </summary>
    public static IReadOnlyCollection<NodeDefinition> AllDefinitions =>
        _definitions.Values
            .OrderBy(d => d.Category)
            .ThenBy(d => d.Title)
            .ToList();

    /// <summary>
    /// Returns the <see cref="NodeDefinition"/> for <paramref name="type"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no definition is registered for the given type.
    /// </exception>
    public static NodeDefinition GetDefinition(NodeType type) => _definitions[type];

    // ── Factory ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="ScriptNode"/> of the given type,
    /// fully initialised with ports and default properties,
    /// placed at position (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    public static ScriptNode Create(NodeType type, double x = 0, double y = 0)
    {
        var def = _definitions[type];
        var node = new ScriptNode
        {
            Type = type,
            Title = def.Title,
            X = x,
            Y = y,
        };

        foreach (var (name, dataType) in def.InputPorts)
            node.Inputs.Add(new NodePort { Name = name, DataType = dataType, IsInput = true });

        foreach (var (name, dataType) in def.OutputPorts)
            node.Outputs.Add(new NodePort { Name = name, DataType = dataType, IsInput = false });

        foreach (var kv in def.DefaultProperties)
            node.Properties[kv.Key] = kv.Value;

        return node;
    }

    // ── Definition building ────────────────────────────────────────────────────

    private static IEnumerable<NodeDefinition> BuildDefinitions()
    {
        // ── Variables ────────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.VariableInt,
            Title = "Int Variable",
            Description = "Stores and outputs an integer value.",
            Category = "Variables",
            OutputPorts = [("Value", PortDataType.Int)],
            DefaultProperties = { ["Value"] = "0" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.VariableFloat,
            Title = "Float Variable",
            Description = "Stores and outputs a floating-point value.",
            Category = "Variables",
            OutputPorts = [("Value", PortDataType.Float)],
            DefaultProperties = { ["Value"] = "0.0" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.VariableString,
            Title = "String Variable",
            Description = "Stores and outputs a text string.",
            Category = "Variables",
            OutputPorts = [("Value", PortDataType.String)],
            DefaultProperties = { ["Value"] = "" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.VariableBool,
            Title = "Bool Variable",
            Description = "Stores and outputs a boolean (true/false) value.",
            Category = "Variables",
            OutputPorts = [("Value", PortDataType.Bool)],
            DefaultProperties = { ["Value"] = "false" },
        };

        // ── Math & Logic ──────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.MathAdd,
            Title = "Add",
            Description = "Outputs A + B.",
            Category = "Math & Logic",
            InputPorts = [("A", PortDataType.Float), ("B", PortDataType.Float)],
            OutputPorts = [("Result", PortDataType.Float)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.MathSubtract,
            Title = "Subtract",
            Description = "Outputs A - B.",
            Category = "Math & Logic",
            InputPorts = [("A", PortDataType.Float), ("B", PortDataType.Float)],
            OutputPorts = [("Result", PortDataType.Float)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.MathMultiply,
            Title = "Multiply",
            Description = "Outputs A × B.",
            Category = "Math & Logic",
            InputPorts = [("A", PortDataType.Float), ("B", PortDataType.Float)],
            OutputPorts = [("Result", PortDataType.Float)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.MathDivide,
            Title = "Divide",
            Description = "Outputs A ÷ B. Returns 0 when B is 0.",
            Category = "Math & Logic",
            InputPorts = [("A", PortDataType.Float), ("B", PortDataType.Float)],
            OutputPorts = [("Result", PortDataType.Float)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.RandomInt,
            Title = "Random Int",
            Description = "Returns a random integer in [Min, Max).",
            Category = "Math & Logic",
            InputPorts = [("Min", PortDataType.Int), ("Max", PortDataType.Int)],
            OutputPorts = [("Value", PortDataType.Int)],
            DefaultProperties = { ["Min"] = "0", ["Max"] = "100" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.Compare,
            Title = "Compare",
            Description = "Compares A and B using the selected operator. Outputs boolean.",
            Category = "Math & Logic",
            InputPorts = [("A", PortDataType.Any), ("B", PortDataType.Any)],
            OutputPorts = [("Result", PortDataType.Bool)],
            DefaultProperties = { ["Operator"] = "==" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.LogicAnd,
            Title = "AND",
            Description = "Outputs A && B.",
            Category = "Math & Logic",
            InputPorts = [("A", PortDataType.Bool), ("B", PortDataType.Bool)],
            OutputPorts = [("Result", PortDataType.Bool)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.LogicOr,
            Title = "OR",
            Description = "Outputs A || B.",
            Category = "Math & Logic",
            InputPorts = [("A", PortDataType.Bool), ("B", PortDataType.Bool)],
            OutputPorts = [("Result", PortDataType.Bool)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.LogicNot,
            Title = "NOT",
            Description = "Outputs !A.",
            Category = "Math & Logic",
            InputPorts = [("A", PortDataType.Bool)],
            OutputPorts = [("Result", PortDataType.Bool)],
        };

        // ── Control Flow ──────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.Start,
            Title = "Start",
            Description = "Entry point of the script. Execution begins here.",
            Category = "Control Flow",
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.Branch,
            Title = "Branch",
            Description = "Routes execution to True or False based on a condition.",
            Category = "Control Flow",
            InputPorts = [("Exec", PortDataType.Exec), ("Condition", PortDataType.Bool)],
            OutputPorts = [("True", PortDataType.Exec), ("False", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.ForLoop,
            Title = "For Loop",
            Description = "Executes the Loop Body N times, then continues.",
            Category = "Control Flow",
            InputPorts = [("Exec", PortDataType.Exec), ("Count", PortDataType.Int)],
            OutputPorts = [("Loop Body", PortDataType.Exec), ("Index", PortDataType.Int), ("Completed", PortDataType.Exec)],
            DefaultProperties = { ["Count"] = "10" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.WhileLoop,
            Title = "While Loop",
            Description = "Executes the Loop Body while the condition is true.",
            Category = "Control Flow",
            InputPorts = [("Exec", PortDataType.Exec), ("Condition", PortDataType.Bool)],
            OutputPorts = [("Loop Body", PortDataType.Exec), ("Completed", PortDataType.Exec)],
        };

        // ── Map & Procgen ──────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.CreateMap,
            Title = "Create Map",
            Description = "Creates a blank ASCII map of the specified size.",
            Category = "Map & Procgen",
            InputPorts = [("Exec", PortDataType.Exec), ("Width", PortDataType.Int), ("Height", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map)],
            DefaultProperties = { ["Width"] = "80", ["Height"] = "24" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GenerateCaveCellular,
            Title = "Cave (Cellular Automata)",
            Description = "Fills the map with a cave generated via cellular automata.",
            Category = "Map & Procgen",
            InputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map), ("FillRatio", PortDataType.Float), ("Iterations", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map)],
            DefaultProperties = { ["FillRatio"] = "0.45", ["Iterations"] = "5" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GenerateRoomsBSP,
            Title = "Rooms (BSP)",
            Description = "Fills the map with rectangular rooms connected by corridors using BSP.",
            Category = "Map & Procgen",
            InputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map), ("MinRoomSize", PortDataType.Int), ("MaxRoomSize", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map)],
            DefaultProperties = { ["MinRoomSize"] = "4", ["MaxRoomSize"] = "12" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GenerateDrunkardWalk,
            Title = "Drunkard Walk",
            Description = "Carves a winding path through the map.",
            Category = "Map & Procgen",
            InputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map), ("Steps", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map)],
            DefaultProperties = { ["Steps"] = "500" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.FillRegion,
            Title = "Fill Region",
            Description = "Fills a rectangular area of the map with a given character.",
            Category = "Map & Procgen",
            InputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map), ("X", PortDataType.Int), ("Y", PortDataType.Int), ("Width", PortDataType.Int), ("Height", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Char"] = "#", ["FgColor"] = "FFFFFF", ["BgColor"] = "000000" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetCell,
            Title = "Get Cell",
            Description = "Reads the cell at (X, Y) from the map.",
            Category = "Map & Procgen",
            InputPorts = [("Map", PortDataType.Map), ("X", PortDataType.Int), ("Y", PortDataType.Int)],
            OutputPorts = [("Cell", PortDataType.Cell)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SetCell,
            Title = "Set Cell",
            Description = "Writes a cell at (X, Y) on the map.",
            Category = "Map & Procgen",
            InputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map), ("X", PortDataType.Int), ("Y", PortDataType.Int), ("Cell", PortDataType.Cell)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };

        // ── Entity ─────────────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.SpawnEntity,
            Title = "Spawn Entity",
            Description = "Creates a new entity and places it at (X, Y).",
            Category = "Entity",
            InputPorts = [("Exec", PortDataType.Exec), ("X", PortDataType.Int), ("Y", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Entity", PortDataType.Entity)],
            DefaultProperties = { ["Name"] = "Entity", ["Glyph"] = "@", ["FgColor"] = "FFFFFF" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.MoveEntity,
            Title = "Move Entity",
            Description = "Moves an entity by (DX, DY).",
            Category = "Entity",
            InputPorts = [("Exec", PortDataType.Exec), ("Entity", PortDataType.Entity), ("DX", PortDataType.Int), ("DY", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.DestroyEntity,
            Title = "Destroy Entity",
            Description = "Removes the entity from the world.",
            Category = "Entity",
            InputPorts = [("Exec", PortDataType.Exec), ("Entity", PortDataType.Entity)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };

        // ── ASCII Display ──────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.RenderMap,
            Title = "Render Map",
            Description = "Renders the map to the ASCII display.",
            Category = "ASCII Display",
            InputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.DrawChar,
            Title = "Draw Char",
            Description = "Draws a single character at screen position (X, Y).",
            Category = "ASCII Display",
            InputPorts = [("Exec", PortDataType.Exec), ("X", PortDataType.Int), ("Y", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Char"] = "@", ["FgColor"] = "FFFFFF", ["BgColor"] = "000000" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.PrintText,
            Title = "Print Text",
            Description = "Prints a string of text at (X, Y).",
            Category = "ASCII Display",
            InputPorts = [("Exec", PortDataType.Exec), ("Text", PortDataType.String), ("X", PortDataType.Int), ("Y", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.ClearDisplay,
            Title = "Clear Display",
            Description = "Clears the entire ASCII display.",
            Category = "ASCII Display",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };

        // ── Menus ──────────────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.ShowMenu,
            Title = "Show Menu",
            Description = "Displays a titled menu and waits for selection.",
            Category = "Menus",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("SelectedIndex", PortDataType.Int)],
            DefaultProperties = { ["Title"] = "Menu", ["Items"] = "Option 1\nOption 2\nOption 3" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.AddMenuItem,
            Title = "Add Menu Item",
            Description = "Adds a selectable item to the most recent ShowMenu.",
            Category = "Menus",
            InputPorts = [("Exec", PortDataType.Exec), ("Label", PortDataType.String)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetMenuSelection,
            Title = "Get Menu Selection",
            Description = "Outputs the index of the last menu item selected.",
            Category = "Menus",
            OutputPorts = [("SelectedIndex", PortDataType.Int)],
        };

        // ── Events ─────────────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.OnKeyPress,
            Title = "On Key Press",
            Description = "Fires when the player presses a key.",
            Category = "Events",
            OutputPorts = [("Exec", PortDataType.Exec), ("Key", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnTick,
            Title = "On Tick",
            Description = "Fires once per game tick.",
            Category = "Events",
            OutputPorts = [("Exec", PortDataType.Exec), ("TickCount", PortDataType.Int)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnEntityEnterTile,
            Title = "On Entity Enter Tile",
            Description = "Fires when an entity moves onto a specific tile.",
            Category = "Events",
            OutputPorts = [("Exec", PortDataType.Exec), ("Entity", PortDataType.Entity), ("X", PortDataType.Int), ("Y", PortDataType.Int)],
        };

        // ── Custom / Extension ─────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.InlineExpression,
            Title = "Inline Expression",
            Description = "Evaluates a C# expression string at runtime.",
            Category = "Advanced",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Result", PortDataType.Any)],
            DefaultProperties = { ["Expression"] = "1 + 1" },
        };
    }
}
