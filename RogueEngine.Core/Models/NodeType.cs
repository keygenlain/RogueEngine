namespace RogueEngine.Core.Models;

/// <summary>
/// Defines the category and behaviour of a visual-scripting node.
/// </summary>
public enum NodeType
{
    // ── Variables ────────────────────────────────────────────────────────────
    /// <summary>Holds an integer value.</summary>
    VariableInt,
    /// <summary>Holds a floating-point value.</summary>
    VariableFloat,
    /// <summary>Holds a string value.</summary>
    VariableString,
    /// <summary>Holds a boolean value.</summary>
    VariableBool,

    // ── Math &amp; Logic ──────────────────────────────────────────────────────────
    /// <summary>Adds two numbers together.</summary>
    MathAdd,
    /// <summary>Subtracts the second number from the first.</summary>
    MathSubtract,
    /// <summary>Multiplies two numbers.</summary>
    MathMultiply,
    /// <summary>Divides the first number by the second.</summary>
    MathDivide,
    /// <summary>Returns a random integer within the given range [Min, Max).</summary>
    RandomInt,
    /// <summary>Compares two values; outputs a boolean result.</summary>
    Compare,
    /// <summary>Logical AND of two booleans.</summary>
    LogicAnd,
    /// <summary>Logical OR of two booleans.</summary>
    LogicOr,
    /// <summary>Logical NOT of a boolean.</summary>
    LogicNot,

    // ── Control Flow ──────────────────────────────────────────────────────────
    /// <summary>Branches execution depending on a boolean condition.</summary>
    Branch,
    /// <summary>Iterates a fixed number of times.</summary>
    ForLoop,
    /// <summary>Iterates while a condition is true.</summary>
    WhileLoop,
    /// <summary>Marks the start of a script graph.</summary>
    Start,

    // ── Map &amp; Procgen ─────────────────────────────────────────────────────────
    /// <summary>Creates an empty ASCII map of the given dimensions.</summary>
    CreateMap,
    /// <summary>Fills the map using a cellular-automata cave generator.</summary>
    GenerateCaveCellular,
    /// <summary>Fills the map with BSP-partitioned rectangular rooms.</summary>
    GenerateRoomsBSP,
    /// <summary>Runs a simple drunk-walk corridor generator.</summary>
    GenerateDrunkardWalk,
    /// <summary>Fills a rectangular region of the map with a given cell character.</summary>
    FillRegion,
    /// <summary>Outputs a single cell from the map at (X, Y).</summary>
    GetCell,
    /// <summary>Sets a single cell on the map at (X, Y).</summary>
    SetCell,

    // ── Entity ────────────────────────────────────────────────────────────────
    /// <summary>Creates a new game entity with a given character and colour.</summary>
    SpawnEntity,
    /// <summary>Moves an entity by (DX, DY) tiles.</summary>
    MoveEntity,
    /// <summary>Destroys an entity.</summary>
    DestroyEntity,

    // ── ASCII Display ─────────────────────────────────────────────────────────
    /// <summary>Renders the given map to the ASCII display.</summary>
    RenderMap,
    /// <summary>Draws a single character at a screen position.</summary>
    DrawChar,
    /// <summary>Prints a string of text onto the display at a given position.</summary>
    PrintText,
    /// <summary>Clears the entire ASCII display.</summary>
    ClearDisplay,

    // ── Menus ─────────────────────────────────────────────────────────────────
    /// <summary>Displays a titled menu with selectable items.</summary>
    ShowMenu,
    /// <summary>Adds a text item to the most recent ShowMenu node.</summary>
    AddMenuItem,
    /// <summary>Outputs the index of the item selected in the menu.</summary>
    GetMenuSelection,

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fires when the player presses a key.</summary>
    OnKeyPress,
    /// <summary>Fires once per game tick.</summary>
    OnTick,
    /// <summary>Fires when an entity enters a given tile.</summary>
    OnEntityEnterTile,

    // ── Custom / Extension ────────────────────────────────────────────────────
    /// <summary>Executes an inline C# expression string (advanced users).</summary>
    InlineExpression,
}
