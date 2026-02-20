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

    // ── Overworld &amp; Persistent Locations ─────────────────────────────────────
    /// <summary>Creates a new overworld that holds a collection of locations.</summary>
    CreateOverworld,
    /// <summary>Adds a named location to an existing overworld.</summary>
    AddLocation,
    /// <summary>Connects two locations via a named exit (e.g. "North").</summary>
    ConnectLocations,
    /// <summary>Moves the player to a named location in the overworld.</summary>
    TravelToLocation,
    /// <summary>Travels via a named exit from the current location.</summary>
    TravelViaExit,
    /// <summary>Outputs the name of the current overworld location.</summary>
    GetCurrentLocation,
    /// <summary>Fires when the player arrives at a new location.</summary>
    OnEnterLocation,
    /// <summary>Fires when the player leaves a location.</summary>
    OnLeaveLocation,
    /// <summary>Reads a persistent key-value property from the current location.</summary>
    GetLocationData,
    /// <summary>Writes a persistent key-value property on the current location.</summary>
    SetLocationData,
    /// <summary>
    /// Generates or re-generates the map for a location using the specified
    /// procgen algorithm and assigns it to the location's Map slot.
    /// </summary>
    GenerateLocation,
    /// <summary>Renders the overworld as a dot-map on the ASCII display.</summary>
    RenderOverworld,

    // ── Multiplayer ───────────────────────────────────────────────────────────
    /// <summary>Starts hosting a multiplayer session on the given port.</summary>
    HostSession,
    /// <summary>Joins an existing multiplayer session by host:port.</summary>
    JoinSession,
    /// <summary>Disconnects from or shuts down the current session.</summary>
    LeaveSession,
    /// <summary>Broadcasts a typed message to all players in the session.</summary>
    BroadcastMessage,
    /// <summary>Sends a message to a specific player by player ID.</summary>
    SendMessageToPlayer,
    /// <summary>Fires when a network message of a given type is received.</summary>
    OnMessageReceived,
    /// <summary>Outputs the list of connected player names as a newline string.</summary>
    GetConnectedPlayers,
    /// <summary>Outputs the local player's name.</summary>
    GetLocalPlayerName,
    /// <summary>Outputs <see langword="true"/> when the local client is the host.</summary>
    IsHost,
    /// <summary>Synchronises an entity's position to all remote clients.</summary>
    SyncEntityState,
    /// <summary>Fires when a remote entity position update is received.</summary>
    OnEntityStateReceived,

    // ── Multiplayer: Client-Server ────────────────────────────────────────────
    /// <summary>
    /// Starts a dedicated authoritative server with no local player.
    /// Clients send input; the server sends state updates.
    /// </summary>
    HostServer,
    /// <summary>
    /// Connects to a dedicated authoritative server as a
    /// <see cref="NetworkRole.AuthoritativeClient"/>.
    /// </summary>
    ConnectToServer,
    /// <summary>
    /// Sends a directed message from the server to a single connected client.
    /// Only available when running as a dedicated server or host.
    /// </summary>
    SendToClient,
    /// <summary>Fires on the server when a new client connects.</summary>
    OnClientConnected,
    /// <summary>Fires on the server when a client disconnects.</summary>
    OnClientDisconnected,
    /// <summary>Outputs the current <see cref="NetworkRole"/> of the session.</summary>
    GetNetworkRole,

    // ── Persistence / Save-Load ───────────────────────────────────────────────
    /// <summary>Saves the full game state to a named save slot.</summary>
    SaveGame,
    /// <summary>Loads game state from a named save slot.</summary>
    LoadGame,
    /// <summary>Deletes a save slot.</summary>
    DeleteSave,
    /// <summary>Outputs <see langword="true"/> if a save slot exists.</summary>
    SaveSlotExists,
    /// <summary>Writes a typed value to the global persistent store.</summary>
    SetPersistentValue,
    /// <summary>Reads a typed value from the global persistent store.</summary>
    GetPersistentValue,

    // ── Dialogue &amp; Cutscenes ─────────────────────────────────────────────────
    /// <summary>Displays a speaker-attributed line of dialogue.</summary>
    ShowDialogueLine,
    /// <summary>Presents a choice list and fires on the chosen index.</summary>
    ShowDialogueChoice,
    /// <summary>Fires when dialogue choice index N is selected.</summary>
    OnDialogueChoice,
    /// <summary>Starts a cutscene sequence; pauses normal input.</summary>
    StartCutscene,
    /// <summary>Ends the active cutscene and restores input.</summary>
    EndCutscene,
    /// <summary>Pauses script execution for a given number of ticks.</summary>
    Wait,

    // ── Factions &amp; Relationships ────────────────────────────────────────────
    /// <summary>Creates a named faction.</summary>
    CreateFaction,
    /// <summary>Assigns an entity to a faction.</summary>
    AssignEntityFaction,
    /// <summary>Sets the relationship value between two factions (-100 to 100).</summary>
    SetFactionRelation,
    /// <summary>Outputs the current relationship value between two factions.</summary>
    GetFactionRelation,
    /// <summary>Outputs the faction name of an entity.</summary>
    GetEntityFaction,
    /// <summary>Fires when a faction's relationship crosses a threshold.</summary>
    OnRelationChanged,

    // ── Day / Night &amp; Time ──────────────────────────────────────────────────
    /// <summary>Advances the in-game clock by a given number of time units.</summary>
    AdvanceTime,
    /// <summary>Outputs the current in-game hour (0–23).</summary>
    GetTimeOfDay,
    /// <summary>Outputs true during the configured night hours.</summary>
    IsNight,
    /// <summary>Fires at a specific in-game hour.</summary>
    OnTimeOfDay,

    // ── Scene Tree ────────────────────────────────────────────────────────────
    /// <summary>Creates a new scene root node.</summary>
    SceneCreate,
    /// <summary>Adds a child node to a parent scene node.</summary>
    SceneAddChild,
    /// <summary>Removes a child node from its parent.</summary>
    SceneRemoveChild,
    /// <summary>Finds a node in the active scene by name and type.</summary>
    SceneFindNode,
    /// <summary>Instantiates a registered scene definition by name.</summary>
    SceneInstantiate,
    /// <summary>Switches the active scene to a named registered scene.</summary>
    SceneChange,
    /// <summary>Outputs the name of the currently active scene.</summary>
    SceneGetCurrent,
    /// <summary>Sets a node's grid position.</summary>
    SceneSetPosition,
    /// <summary>Outputs a node's current grid position.</summary>
    SceneGetPosition,
    /// <summary>Toggles a node's Active flag.</summary>
    SceneSetActive,
    /// <summary>Toggles a node's Visible flag.</summary>
    SceneSetVisible,
    /// <summary>Fires when a TimerNode times out.</summary>
    OnTimerTimeout,
    /// <summary>Fires when an entity enters an AreaNode.</summary>
    OnAreaBodyEntered,
    /// <summary>Fires when an entity exits an AreaNode.</summary>
    OnAreaBodyExited,

    // ── Sprite System ─────────────────────────────────────────────────────────
    /// <summary>Registers a sprite definition in the sprite library.</summary>
    RegisterSprite,
    /// <summary>Loads a sprite sheet atlas into the sprite library.</summary>
    LoadSpriteSheet,
    /// <summary>Sets the sprite used by a SpriteNode by name.</summary>
    SpriteSetSprite,
    /// <summary>Outputs the current sprite name of a SpriteNode.</summary>
    SpriteGetSprite,
    /// <summary>Sets the render mode for a SpriteNode (ASCII / Graphic / Auto).</summary>
    SpriteSetRenderMode,
    /// <summary>Starts or stops a sprite animation.</summary>
    SpriteSetPlaying,

    // ── Custom / Extension ────────────────────────────────────────────────────
    /// <summary>Executes an inline C# expression string (advanced users).</summary>
    InlineExpression,

    // ── Morgue File ───────────────────────────────────────────────────────────
    /// <summary>
    /// Generates a post-mortem morgue file recording the character's run.
    /// Called when the player dies or the run ends.
    /// </summary>
    GenerateMorgueFile,
    /// <summary>
    /// Event node that fires when the player entity is marked dead
    /// (HP ≤ 0 or explicitly triggered by script logic).
    /// </summary>
    OnPlayerDeath,

    // ── Roguelike Core ────────────────────────────────────────────────────────
    /// <summary>
    /// Computes a field-of-view set on a Map from an Origin tile using
    /// recursive shadowcasting out to the given Radius.
    /// Outputs a list of visible tile positions as "x,y" strings.
    /// </summary>
    ComputeFOV,
    /// <summary>
    /// Finds the shortest path on a Map from Start to End using A*, treating
    /// '#' tiles as walls.  Outputs the NextStep location and a Success flag.
    /// </summary>
    FindPathAStar,
    /// <summary>
    /// Reads a named stat from an entity's Properties dictionary and outputs
    /// it as an Any value.
    /// </summary>
    GetEntityStat,
    /// <summary>
    /// Control-flow node that pauses the Exec chain until a key is pressed,
    /// then resumes execution and outputs the pressed Key as a String.
    /// </summary>
    WaitForInput,

    /// <summary>
    /// Checks whether moving an Entity by (DX, DY) would place it on a tile
    /// occupied by another entity, indicating a bump interaction.
    /// Outputs <see langword="true"/> and the blocking Target entity if blocked.
    /// </summary>
    CheckEntityBump,
    /// <summary>
    /// Reads the "Type" key from an entity's Properties dictionary and outputs
    /// it as a String (e.g. "enemy", "npc", "item").
    /// </summary>
    GetEntityType,
    /// <summary>
    /// Writes a value into an entity's Properties dictionary under the given
    /// StatName key.
    /// </summary>
    SetEntityStat,
    /// <summary>
    /// Reads a numeric stat from an entity's Properties, applies an arithmetic
    /// operator (+, -, *, /) with the given Amount, writes the result back, and
    /// outputs the new value as a Float.
    /// </summary>
    ModifyEntityStat,
    /// <summary>
    /// Returns all live entities whose (X, Y) position matches the given tile
    /// coordinates.  Outputs Count and the First matching entity.
    /// </summary>
    GetEntitiesAtTile,

    // ── Map & Procgen: Custom Rooms ───────────────────────────────────────────
    /// <summary>
    /// Registers a named custom room template using a multiline layout string
    /// where '#' is wall and '.' is floor.  Call before <see cref="PlaceCustomRoom"/>.
    /// </summary>
    DefineCustomRoom,
    /// <summary>
    /// Stamps a previously registered custom room template onto a Map at (X, Y).
    /// </summary>
    PlaceCustomRoom,
    /// <summary>
    /// Entry-point node for a custom procgen graph.  Replaces <see cref="Start"/>
    /// when the graph is used as a procgen method.  Provides Map and Seed outputs
    /// so the graph receives the map to fill and the deterministic seed.
    /// </summary>
    CustomProcgenStart,

    // ── Battle System ─────────────────────────────────────────────────────────
    /// <summary>
    /// Rolls Count dice each with Sides faces and outputs the total.
    /// </summary>
    RollDice,
    /// <summary>
    /// Reduces an entity's "HP" stat by Amount.  Outputs the new HP value and
    /// a flag indicating whether the entity is now dead (HP ≤ 0).
    /// </summary>
    ApplyDamage,
    /// <summary>
    /// Increases an entity's "HP" stat by Amount, capped at "MaxHP".
    /// Outputs the new HP value.
    /// </summary>
    HealEntity,
    /// <summary>
    /// Outputs <see langword="true"/> when an entity's "HP" stat is ≤ 0.
    /// </summary>
    IsEntityDead,
    /// <summary>
    /// Signals the start of a turn-based combat encounter.
    /// Resets internal turn-order state.
    /// </summary>
    StartCombat,
    /// <summary>
    /// Advances to the next combatant in the turn order.
    /// </summary>
    EndTurn,
    /// <summary>
    /// Rolls initiative for an entity (1d20 + Modifier) and outputs the result.
    /// </summary>
    GetInitiative,

    // ── RPG System ────────────────────────────────────────────────────────────
    /// <summary>
    /// Adds XP to an entity and checks whether it has levelled up.
    /// </summary>
    AddExperience,
    /// <summary>
    /// Outputs the current level of an entity (read from the "Level" stat).
    /// </summary>
    GetLevel,
    /// <summary>
    /// Appends a named item to an entity's inventory list (stored in the
    /// "Inventory" property as a comma-separated string).
    /// </summary>
    AddToInventory,
    /// <summary>
    /// Removes the item at the given index from an entity's inventory and
    /// outputs the item's name.
    /// </summary>
    RemoveFromInventory,
    /// <summary>
    /// Outputs the name of the item at the given index in an entity's inventory.
    /// </summary>
    GetInventoryItem,
    /// <summary>
    /// Outputs the number of items in an entity's inventory.
    /// </summary>
    GetInventorySize,
    /// <summary>
    /// Moves an item from an entity's inventory into a named equipment slot
    /// (stored as "Equip_&lt;Slot&gt;" in entity Properties).
    /// </summary>
    EquipItem,
    /// <summary>
    /// Outputs the name of the item currently equipped in the given slot.
    /// </summary>
    GetEquippedItem,
}
