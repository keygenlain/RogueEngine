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

        // ── Overworld & Persistent Locations ───────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.CreateOverworld,
            Title = "Create Overworld",
            Description = "Creates a new overworld to hold a collection of locations.",
            Category = "Overworld",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Overworld", PortDataType.Overworld)],
            DefaultProperties = { ["Name"] = "World" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.AddLocation,
            Title = "Add Location",
            Description = "Creates a named location and adds it to the overworld.",
            Category = "Overworld",
            InputPorts = [("Exec", PortDataType.Exec), ("Overworld", PortDataType.Overworld)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Location", PortDataType.Location)],
            DefaultProperties = { ["Name"] = "New Location", ["WorldX"] = "0", ["WorldY"] = "0", ["Description"] = "" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.ConnectLocations,
            Title = "Connect Locations",
            Description = "Adds a named exit between two locations.",
            Category = "Overworld",
            InputPorts = [("Exec", PortDataType.Exec), ("From", PortDataType.Location), ("To", PortDataType.Location)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["ExitName"] = "North", ["ReverseExitName"] = "South" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.TravelToLocation,
            Title = "Travel To Location",
            Description = "Moves the player to the given location.",
            Category = "Overworld",
            InputPorts = [("Exec", PortDataType.Exec), ("Overworld", PortDataType.Overworld), ("Location", PortDataType.Location)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.TravelViaExit,
            Title = "Travel Via Exit",
            Description = "Travels through a named exit from the current location.",
            Category = "Overworld",
            InputPorts = [("Exec", PortDataType.Exec), ("Overworld", PortDataType.Overworld)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Arrived", PortDataType.Location)],
            DefaultProperties = { ["ExitName"] = "North" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetCurrentLocation,
            Title = "Get Current Location",
            Description = "Outputs the name and reference of the current overworld location.",
            Category = "Overworld",
            InputPorts = [("Overworld", PortDataType.Overworld)],
            OutputPorts = [("Location", PortDataType.Location), ("Name", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnEnterLocation,
            Title = "On Enter Location",
            Description = "Fires when the player arrives at a new location.",
            Category = "Overworld",
            OutputPorts = [("Exec", PortDataType.Exec), ("Location", PortDataType.Location), ("Name", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnLeaveLocation,
            Title = "On Leave Location",
            Description = "Fires when the player leaves a location.",
            Category = "Overworld",
            OutputPorts = [("Exec", PortDataType.Exec), ("Location", PortDataType.Location), ("ExitUsed", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetLocationData,
            Title = "Get Location Data",
            Description = "Reads a persistent key-value property from the current location.",
            Category = "Overworld",
            InputPorts = [("Location", PortDataType.Location)],
            OutputPorts = [("Value", PortDataType.String)],
            DefaultProperties = { ["Key"] = "key" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SetLocationData,
            Title = "Set Location Data",
            Description = "Writes a persistent key-value property on a location.",
            Category = "Overworld",
            InputPorts = [("Exec", PortDataType.Exec), ("Location", PortDataType.Location), ("Value", PortDataType.String)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Key"] = "key" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GenerateLocation,
            Title = "Generate Location Map",
            Description = "Procedurally generates the map for a location.",
            Category = "Overworld",
            InputPorts = [("Exec", PortDataType.Exec), ("Location", PortDataType.Location)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Map", PortDataType.Map)],
            DefaultProperties = {
                ["Algorithm"] = "Cave",
                ["Width"] = "60", ["Height"] = "20",
                ["FillRatio"] = "0.45", ["Iterations"] = "5",
            },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.RenderOverworld,
            Title = "Render Overworld",
            Description = "Renders all overworld locations as dots on the ASCII display.",
            Category = "Overworld",
            InputPorts = [("Exec", PortDataType.Exec), ("Overworld", PortDataType.Overworld)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };

        // ── Multiplayer ────────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.HostSession,
            Title = "Host Session",
            Description = "Starts hosting a multiplayer session. Players can connect to this machine's IP on the configured port.",
            Category = "Multiplayer",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Session", PortDataType.Session)],
            DefaultProperties = { ["SessionName"] = "My Game", ["Port"] = "7777", ["MaxPlayers"] = "4", ["PlayerName"] = "Host" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.JoinSession,
            Title = "Join Session",
            Description = "Connects to a hosted multiplayer session.",
            Category = "Multiplayer",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Session", PortDataType.Session)],
            DefaultProperties = { ["Host"] = "127.0.0.1", ["Port"] = "7777", ["PlayerName"] = "Player" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.LeaveSession,
            Title = "Leave Session",
            Description = "Disconnects from or shuts down the current multiplayer session.",
            Category = "Multiplayer",
            InputPorts = [("Exec", PortDataType.Exec), ("Session", PortDataType.Session)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.BroadcastMessage,
            Title = "Broadcast Message",
            Description = "Sends a typed message to all players in the session.",
            Category = "Multiplayer",
            InputPorts = [("Exec", PortDataType.Exec), ("Session", PortDataType.Session), ("Payload", PortDataType.String)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["MessageType"] = "chat" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SendMessageToPlayer,
            Title = "Send Message To Player",
            Description = "Sends a typed message to a specific player by name.",
            Category = "Multiplayer",
            InputPorts = [("Exec", PortDataType.Exec), ("Session", PortDataType.Session), ("Payload", PortDataType.String), ("TargetPlayer", PortDataType.String)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["MessageType"] = "direct" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnMessageReceived,
            Title = "On Message Received",
            Description = "Fires when a network message of the specified type is received.",
            Category = "Multiplayer",
            OutputPorts = [("Exec", PortDataType.Exec), ("Sender", PortDataType.String), ("Payload", PortDataType.String)],
            DefaultProperties = { ["MessageType"] = "chat" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetConnectedPlayers,
            Title = "Get Connected Players",
            Description = "Outputs the number of connected players and their names.",
            Category = "Multiplayer",
            InputPorts = [("Session", PortDataType.Session)],
            OutputPorts = [("PlayerCount", PortDataType.Int), ("PlayerNames", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetLocalPlayerName,
            Title = "Get Local Player Name",
            Description = "Outputs the local player's display name.",
            Category = "Multiplayer",
            InputPorts = [("Session", PortDataType.Session)],
            OutputPorts = [("Name", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.IsHost,
            Title = "Is Host",
            Description = "Outputs true when the local client is the session host.",
            Category = "Multiplayer",
            InputPorts = [("Session", PortDataType.Session)],
            OutputPorts = [("Result", PortDataType.Bool)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SyncEntityState,
            Title = "Sync Entity State",
            Description = "Broadcasts an entity's position and glyph to all remote clients.",
            Category = "Multiplayer",
            InputPorts = [("Exec", PortDataType.Exec), ("Session", PortDataType.Session), ("Entity", PortDataType.Entity)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnEntityStateReceived,
            Title = "On Entity State Received",
            Description = "Fires when a remote entity state update is received.",
            Category = "Multiplayer",
            OutputPorts = [("Exec", PortDataType.Exec), ("EntityId", PortDataType.String), ("X", PortDataType.Int), ("Y", PortDataType.Int), ("Glyph", PortDataType.String)],
        };

        // ── Multiplayer: Client-Server ─────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.HostServer,
            Title = "Host Server",
            Description = "Starts a dedicated authoritative server without a local player. Clients connect using Connect To Server.",
            Category = "Multiplayer",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Session", PortDataType.Session)],
            DefaultProperties = { ["SessionName"] = "Dedicated Server", ["Port"] = "7777", ["MaxPlayers"] = "16" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.ConnectToServer,
            Title = "Connect To Server",
            Description = "Connects to a dedicated authoritative server as an AuthoritativeClient. Use instead of Join Session when targeting a HostServer.",
            Category = "Multiplayer",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Session", PortDataType.Session)],
            DefaultProperties = { ["Host"] = "127.0.0.1", ["Port"] = "7777", ["PlayerName"] = "Player" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SendToClient,
            Title = "Send To Client",
            Description = "Sends a directed message from the server to a single client identified by player ID. Only valid on the host/server.",
            Category = "Multiplayer",
            InputPorts = [("Exec", PortDataType.Exec), ("Session", PortDataType.Session), ("Payload", PortDataType.String), ("PlayerId", PortDataType.String)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["MessageType"] = "server-direct" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnClientConnected,
            Title = "On Client Connected",
            Description = "Fires on the server when a new client establishes a connection.",
            Category = "Multiplayer",
            OutputPorts = [("Exec", PortDataType.Exec), ("PlayerName", PortDataType.String), ("PlayerId", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnClientDisconnected,
            Title = "On Client Disconnected",
            Description = "Fires on the server when a connected client drops or explicitly leaves.",
            Category = "Multiplayer",
            OutputPorts = [("Exec", PortDataType.Exec), ("PlayerName", PortDataType.String), ("PlayerId", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetNetworkRole,
            Title = "Get Network Role",
            Description = "Outputs the current network role of this session: Peer, DedicatedServer, or AuthoritativeClient.",
            Category = "Multiplayer",
            InputPorts = [("Session", PortDataType.Session)],
            OutputPorts = [("Role", PortDataType.String)],
        };

        // ── Persistence / Save-Load ────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.SaveGame,
            Title = "Save Game",
            Description = "Saves the full game state (overworld, entities, persistent values) to a named slot.",
            Category = "Persistence",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Success", PortDataType.Bool)],
            DefaultProperties = { ["Slot"] = "slot1" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.LoadGame,
            Title = "Load Game",
            Description = "Loads game state from a named save slot.",
            Category = "Persistence",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Success", PortDataType.Bool)],
            DefaultProperties = { ["Slot"] = "slot1" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.DeleteSave,
            Title = "Delete Save",
            Description = "Deletes a save slot.",
            Category = "Persistence",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Slot"] = "slot1" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SaveSlotExists,
            Title = "Save Slot Exists",
            Description = "Outputs true if the named save slot exists on disk.",
            Category = "Persistence",
            OutputPorts = [("Exists", PortDataType.Bool)],
            DefaultProperties = { ["Slot"] = "slot1" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SetPersistentValue,
            Title = "Set Persistent Value",
            Description = "Writes a key-value pair to the global persistent store (survives save/load).",
            Category = "Persistence",
            InputPorts = [("Exec", PortDataType.Exec), ("Value", PortDataType.String)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Key"] = "myVar" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetPersistentValue,
            Title = "Get Persistent Value",
            Description = "Reads a value from the global persistent store.",
            Category = "Persistence",
            OutputPorts = [("Value", PortDataType.String)],
            DefaultProperties = { ["Key"] = "myVar", ["Default"] = "" },
        };

        // ── Dialogue & Cutscenes ───────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.ShowDialogueLine,
            Title = "Show Dialogue Line",
            Description = "Displays a single speaker-attributed dialogue line.",
            Category = "Dialogue",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Speaker"] = "NPC", ["Text"] = "Hello, adventurer!", ["X"] = "0", ["Y"] = "20" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.ShowDialogueChoice,
            Title = "Show Dialogue Choice",
            Description = "Presents a list of choices and outputs the selected index.",
            Category = "Dialogue",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("SelectedIndex", PortDataType.Int)],
            DefaultProperties = { ["Prompt"] = "What do you do?", ["Choices"] = "Attack\nFlee\nTalk" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnDialogueChoice,
            Title = "On Dialogue Choice",
            Description = "Fires when the player selects a specific dialogue option by index.",
            Category = "Dialogue",
            InputPorts = [("SelectedIndex", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["ChoiceIndex"] = "0" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.StartCutscene,
            Title = "Start Cutscene",
            Description = "Begins a cutscene; pauses normal player input.",
            Category = "Dialogue",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Name"] = "intro" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.EndCutscene,
            Title = "End Cutscene",
            Description = "Ends the active cutscene and restores player input.",
            Category = "Dialogue",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.Wait,
            Title = "Wait",
            Description = "Pauses execution for N game ticks before continuing.",
            Category = "Dialogue",
            InputPorts = [("Exec", PortDataType.Exec), ("Ticks", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Ticks"] = "30" },
        };

        // ── Factions & Relationships ───────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.CreateFaction,
            Title = "Create Faction",
            Description = "Creates a named faction in the game world.",
            Category = "Factions",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Name"] = "Bandits" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.AssignEntityFaction,
            Title = "Assign Entity Faction",
            Description = "Places an entity into a faction.",
            Category = "Factions",
            InputPorts = [("Exec", PortDataType.Exec), ("Entity", PortDataType.Entity)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Faction"] = "Bandits" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SetFactionRelation,
            Title = "Set Faction Relation",
            Description = "Sets the relationship between two factions (-100 hostile → 100 allied).",
            Category = "Factions",
            InputPorts = [("Exec", PortDataType.Exec), ("Value", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["FactionA"] = "Heroes", ["FactionB"] = "Bandits" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetFactionRelation,
            Title = "Get Faction Relation",
            Description = "Outputs the relationship value between two factions.",
            Category = "Factions",
            OutputPorts = [("Value", PortDataType.Int)],
            DefaultProperties = { ["FactionA"] = "Heroes", ["FactionB"] = "Bandits" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetEntityFaction,
            Title = "Get Entity Faction",
            Description = "Outputs the faction name an entity belongs to.",
            Category = "Factions",
            InputPorts = [("Entity", PortDataType.Entity)],
            OutputPorts = [("Faction", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnRelationChanged,
            Title = "On Relation Changed",
            Description = "Fires when the relationship between two factions crosses a threshold.",
            Category = "Factions",
            OutputPorts = [("Exec", PortDataType.Exec), ("NewValue", PortDataType.Int)],
            DefaultProperties = { ["FactionA"] = "Heroes", ["FactionB"] = "Bandits", ["Threshold"] = "0" },
        };

        // ── Day / Night & Time ─────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.AdvanceTime,
            Title = "Advance Time",
            Description = "Advances the in-game clock by N time units.",
            Category = "Time",
            InputPorts = [("Exec", PortDataType.Exec), ("Amount", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec), ("NewHour", PortDataType.Int)],
            DefaultProperties = { ["Amount"] = "1" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.GetTimeOfDay,
            Title = "Get Time Of Day",
            Description = "Outputs the current in-game hour (0–23).",
            Category = "Time",
            OutputPorts = [("Hour", PortDataType.Int)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.IsNight,
            Title = "Is Night",
            Description = "Outputs true when the in-game clock is in the configured night range.",
            Category = "Time",
            OutputPorts = [("Result", PortDataType.Bool)],
            DefaultProperties = { ["NightStart"] = "20", ["NightEnd"] = "6" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnTimeOfDay,
            Title = "On Time Of Day",
            Description = "Fires once when the in-game clock reaches a specific hour.",
            Category = "Time",
            OutputPorts = [("Exec", PortDataType.Exec), ("Hour", PortDataType.Int)],
            DefaultProperties = { ["Hour"] = "6" },
        };

        // ── Scene Tree ─────────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.SceneCreate,
            Title = "Create Scene Node",
            Description = "Creates a new scene node of the specified type and adds it as a child.",
            Category = "Scene",
            InputPorts = [("Exec", PortDataType.Exec), ("Parent", PortDataType.SceneNode)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Node", PortDataType.SceneNode)],
            DefaultProperties = { ["NodeType"] = "SpriteNode", ["Name"] = "NewNode" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SceneAddChild,
            Title = "Add Child",
            Description = "Adds a scene node as a child of another.",
            Category = "Scene",
            InputPorts = [("Exec", PortDataType.Exec), ("Parent", PortDataType.SceneNode), ("Child", PortDataType.SceneNode)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SceneRemoveChild,
            Title = "Remove Child",
            Description = "Removes a node from its parent and exits the tree.",
            Category = "Scene",
            InputPorts = [("Exec", PortDataType.Exec), ("Node", PortDataType.SceneNode)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SceneFindNode,
            Title = "Find Node",
            Description = "Finds a node in the active scene by name.",
            Category = "Scene",
            OutputPorts = [("Node", PortDataType.SceneNode)],
            DefaultProperties = { ["Name"] = "NodeName" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SceneInstantiate,
            Title = "Instantiate Scene",
            Description = "Instantiates a registered scene definition by name.",
            Category = "Scene",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec), ("Root", PortDataType.SceneNode)],
            DefaultProperties = { ["SceneName"] = "main" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SceneChange,
            Title = "Change Scene",
            Description = "Switches the active scene to the named registered scene.",
            Category = "Scene",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["SceneName"] = "main" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SceneGetCurrent,
            Title = "Get Current Scene",
            Description = "Outputs the name of the currently active scene.",
            Category = "Scene",
            OutputPorts = [("Name", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SceneSetPosition,
            Title = "Set Node Position",
            Description = "Sets a GridNode's position on the ASCII grid.",
            Category = "Scene",
            InputPorts = [("Exec", PortDataType.Exec), ("Node", PortDataType.SceneNode), ("X", PortDataType.Int), ("Y", PortDataType.Int)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SceneGetPosition,
            Title = "Get Node Position",
            Description = "Outputs the current grid position of a GridNode.",
            Category = "Scene",
            InputPorts = [("Node", PortDataType.SceneNode)],
            OutputPorts = [("X", PortDataType.Int), ("Y", PortDataType.Int)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SceneSetActive,
            Title = "Set Node Active",
            Description = "Enables or disables a scene node.",
            Category = "Scene",
            InputPorts = [("Exec", PortDataType.Exec), ("Node", PortDataType.SceneNode), ("Active", PortDataType.Bool)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SceneSetVisible,
            Title = "Set Node Visible",
            Description = "Shows or hides a scene node.",
            Category = "Scene",
            InputPorts = [("Exec", PortDataType.Exec), ("Node", PortDataType.SceneNode), ("Visible", PortDataType.Bool)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnTimerTimeout,
            Title = "On Timer Timeout",
            Description = "Fires when a TimerNode with the given name times out.",
            Category = "Scene",
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["TimerName"] = "MyTimer" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnAreaBodyEntered,
            Title = "On Area Body Entered",
            Description = "Fires when an entity enters the named AreaNode.",
            Category = "Scene",
            OutputPorts = [("Exec", PortDataType.Exec), ("Entity", PortDataType.Entity)],
            DefaultProperties = { ["AreaName"] = "MyArea" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnAreaBodyExited,
            Title = "On Area Body Exited",
            Description = "Fires when an entity exits the named AreaNode.",
            Category = "Scene",
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["AreaName"] = "MyArea" },
        };

        // ── Sprite System ──────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.RegisterSprite,
            Title = "Register Sprite",
            Description = "Registers an ASCII-or-graphical sprite definition in the sprite library.",
            Category = "Sprites",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = {
                ["Name"] = "my_sprite", ["Glyph"] = "@",
                ["FgColor"] = "FFFFFF", ["BgColor"] = "000000",
                ["ImagePath"] = "", ["TileX"] = "0", ["TileY"] = "0",
                ["TileWidth"] = "0", ["TileHeight"] = "0",
                ["RenderMode"] = "Auto",
            },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.LoadSpriteSheet,
            Title = "Load Sprite Sheet",
            Description = "Registers a sprite-sheet atlas and creates named tile sprites.",
            Category = "Sprites",
            InputPorts = [("Exec", PortDataType.Exec)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = {
                ["SheetName"] = "tiles", ["ImagePath"] = "tiles.png",
                ["TileWidth"] = "16", ["TileHeight"] = "16",
                ["SpacingX"] = "0", ["SpacingY"] = "0",
                ["MarginX"] = "0", ["MarginY"] = "0",
            },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SpriteSetSprite,
            Title = "Set Sprite",
            Description = "Assigns a named sprite to a SpriteNode.",
            Category = "Sprites",
            InputPorts = [("Exec", PortDataType.Exec), ("Node", PortDataType.SceneNode)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["SpriteName"] = "player" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SpriteGetSprite,
            Title = "Get Sprite",
            Description = "Outputs the current sprite name of a SpriteNode.",
            Category = "Sprites",
            InputPorts = [("Node", PortDataType.SceneNode)],
            OutputPorts = [("SpriteName", PortDataType.String)],
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SpriteSetRenderMode,
            Title = "Set Sprite Render Mode",
            Description = "Sets the render mode for a SpriteNode (AsciiOnly / Auto / GraphicPreferred).",
            Category = "Sprites",
            InputPorts = [("Exec", PortDataType.Exec), ("Node", PortDataType.SceneNode)],
            OutputPorts = [("Exec", PortDataType.Exec)],
            DefaultProperties = { ["Mode"] = "Auto" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.SpriteSetPlaying,
            Title = "Set Sprite Playing",
            Description = "Starts or stops sprite animation on a SpriteNode.",
            Category = "Sprites",
            InputPorts = [("Exec", PortDataType.Exec), ("Node", PortDataType.SceneNode), ("Playing", PortDataType.Bool)],
            OutputPorts = [("Exec", PortDataType.Exec)],
        };

        // ── Morgue File ────────────────────────────────────────────────────────
        yield return new NodeDefinition
        {
            Type = NodeType.GenerateMorgueFile,
            Title = "Generate Morgue File",
            Description = "Generates a post-mortem morgue file for a character when they die. Writes a timestamped .txt record of the run.",
            Category = "Morgue",
            InputPorts = [("Exec", PortDataType.Exec), ("Entity", PortDataType.Entity)],
            OutputPorts = [("Exec", PortDataType.Exec), ("FilePath", PortDataType.String)],
            DefaultProperties = { ["Cause"] = "Unknown cause", ["Directory"] = "morgues" },
        };
        yield return new NodeDefinition
        {
            Type = NodeType.OnPlayerDeath,
            Title = "On Player Death",
            Description = "Event node that fires when the player entity is marked as dead (HP ≤ 0 or triggered by script). Wire to Generate Morgue File.",
            Category = "Morgue",
            OutputPorts = [("Exec", PortDataType.Exec), ("Entity", PortDataType.Entity), ("Cause", PortDataType.String)],
        };
    }
}
