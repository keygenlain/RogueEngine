# RogueEngine — Visual Scripting Node Reference

> **129 nodes** across **20 categories.**  
> Every node listed here can be dragged from the palette onto the script canvas and wired together without writing code.  
> Port types: `Exec` (execution flow) · `Int` · `Float` · `String` · `Bool` · `Any` · `Map` · `Cell` · `Entity` · `Location` · `Overworld` · `Session` · `SceneNode`

---

## Table of Contents

1. [Variables (4)](#1-variables)
2. [Math & Logic (9)](#2-math--logic)
3. [Control Flow (4)](#3-control-flow)
4. [Map & Procgen (10)](#4-map--procgen)
5. [Entity (3)](#5-entity)
6. [ASCII Display (4)](#6-ascii-display)
7. [Menus (3)](#7-menus)
8. [Events (3)](#8-events)
9. [Overworld (12)](#9-overworld)
10. [Multiplayer (17)](#10-multiplayer)
11. [Persistence / Save-Load (6)](#11-persistence--save-load)
12. [Dialogue & Cutscenes (6)](#12-dialogue--cutscenes)
13. [Factions & Relationships (6)](#13-factions--relationships)
14. [Time (4)](#14-time)
15. [Scene Tree (14)](#15-scene-tree)
16. [Sprites (6)](#16-sprites)
17. [Morgue (2)](#17-morgue)
18. [Advanced (1)](#18-advanced)
19. [Battle (7)](#19-battle)
20. [RPG (8)](#20-rpg)

---

## 1. Variables

Nodes that store a single typed value and expose it as an output port.  
All variable nodes have **no inputs** and a single `Value` output.  
The value is set via the **Properties panel**.

| Title | Enum key | Output type | Default |
|---|---|---|---|
| **Int Variable** | `VariableInt` | `Int` | `0` |
| **Float Variable** | `VariableFloat` | `Float` | `0.0` |
| **String Variable** | `VariableString` | `String` | _(empty)_ |
| **Bool Variable** | `VariableBool` | `Bool` | `false` |

---

## 2. Math & Logic

Stateless computation nodes. Connect numeric or boolean ports and read the result.

| Title | Enum key | Inputs | Output | Notes |
|---|---|---|---|---|
| **Add** | `MathAdd` | `A: Float`, `B: Float` | `Result: Float` | A + B |
| **Subtract** | `MathSubtract` | `A: Float`, `B: Float` | `Result: Float` | A − B |
| **Multiply** | `MathMultiply` | `A: Float`, `B: Float` | `Result: Float` | A × B |
| **Divide** | `MathDivide` | `A: Float`, `B: Float` | `Result: Float` | A ÷ B; returns 0 when B = 0 |
| **Random Int** | `RandomInt` | `Min: Int`, `Max: Int` | `Value: Int` | Random in \[Min, Max); props: `Min=0`, `Max=100` |
| **Compare** | `Compare` | `A: Any`, `B: Any` | `Result: Bool` | Prop `Operator`: `==` `!=` `<` `<=` `>` `>=` |
| **AND** | `LogicAnd` | `A: Bool`, `B: Bool` | `Result: Bool` | A && B |
| **OR** | `LogicOr` | `A: Bool`, `B: Bool` | `Result: Bool` | A \|\| B |
| **NOT** | `LogicNot` | `A: Bool` | `Result: Bool` | !A |

---

## 3. Control Flow

Nodes that route execution through the graph.

| Title | Enum key | Inputs | Outputs | Notes |
|---|---|---|---|---|
| **Start** | `Start` | — | `Exec` | Entry point; every script must have exactly one |
| **Branch** | `Branch` | `Exec`, `Condition: Bool` | `True: Exec`, `False: Exec` | If/else split |
| **For Loop** | `ForLoop` | `Exec`, `Count: Int` | `Loop Body: Exec`, `Index: Int`, `Completed: Exec` | Iterates N times; prop `Count=10` |
| **While Loop** | `WhileLoop` | `Exec`, `Condition: Bool` | `Loop Body: Exec`, `Completed: Exec` | Loops while condition is true |

---

## 4. Map & Procgen

Nodes for creating, filling, and querying ASCII maps.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Create Map** | `CreateMap` | `Exec`, `Width: Int`, `Height: Int` | `Exec`, `Map` | `Width=80`, `Height=24` |
| **Cave (Cellular Automata)** | `GenerateCaveCellular` | `Exec`, `Map`, `FillRatio: Float`, `Iterations: Int` | `Exec`, `Map` | `FillRatio=0.45`, `Iterations=5` |
| **Rooms (BSP)** | `GenerateRoomsBSP` | `Exec`, `Map`, `MinRoomSize: Int`, `MaxRoomSize: Int` | `Exec`, `Map` | `MinRoomSize=4`, `MaxRoomSize=12` |
| **Drunkard Walk** | `GenerateDrunkardWalk` | `Exec`, `Map`, `Steps: Int` | `Exec`, `Map` | `Steps=500` |
| **Fill Region** | `FillRegion` | `Exec`, `Map`, `X/Y: Int`, `Width/Height: Int` | `Exec` | `Char`, `FgColor`, `BgColor` |
| **Get Cell** | `GetCell` | `Map`, `X: Int`, `Y: Int` | `Cell` | Read a single cell |
| **Set Cell** | `SetCell` | `Exec`, `Map`, `X/Y: Int`, `Cell` | `Exec` | Write a single cell |
| **Define Custom Room** | `DefineCustomRoom` | `Exec` | `Exec` | `Name=room1`, `Layout=###\n#.#\n###` — registers a named template (`#` wall, `.` floor, space = transparent) |
| **Place Custom Room** | `PlaceCustomRoom` | `Exec`, `Map`, `X: Int`, `Y: Int` | `Exec`, `Map` | `RoomName=room1` — stamps a registered template onto the map |
| **Custom Procgen Start** | `CustomProcgenStart` | — | `Exec`, `Map`, `Seed: Int` | Entry point for a custom procgen graph; use instead of **Start** |

### Custom room workflow

1. Add a **Define Custom Room** node and set `Name` + `Layout`.  
2. After generating the base map (BSP / Cave / etc.), add **Place Custom Room** with the matching `RoomName` and an `X` / `Y` position.

### Custom procgen method workflow

1. Create a new script graph (e.g. *"my_dungeon"*).  
2. Place **Custom Procgen Start** as the entry point — it outputs the `Map` to fill and an integer `Seed`.  
3. Wire up any combination of generation nodes (Fill Region, Define Custom Room, Place Custom Room, etc.).  
4. In **Generate Location**, set `Algorithm=Custom` and `ProcgenGraph=my_dungeon`.

---

## 5. Entity

Nodes for spawning and manipulating game entities.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Spawn Entity** | `SpawnEntity` | `Exec`, `X: Int`, `Y: Int` | `Exec`, `Entity` | `Name`, `Glyph=@`, `FgColor=FFFFFF` |
| **Move Entity** | `MoveEntity` | `Exec`, `Entity`, `DX: Int`, `DY: Int` | `Exec` | — |
| **Destroy Entity** | `DestroyEntity` | `Exec`, `Entity` | `Exec` | Removes entity from world |

---

## 6. ASCII Display

Nodes that write to the terminal / ASCII framebuffer.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Render Map** | `RenderMap` | `Exec`, `Map` | `Exec` | Renders full map |
| **Draw Char** | `DrawChar` | `Exec`, `X: Int`, `Y: Int` | `Exec` | `Char=@`, `FgColor`, `BgColor` |
| **Print Text** | `PrintText` | `Exec`, `Text: String`, `X: Int`, `Y: Int` | `Exec` | Prints a string |
| **Clear Display** | `ClearDisplay` | `Exec` | `Exec` | Clears entire screen |

---

## 7. Menus

Nodes for building and reading simple text-based menus.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Show Menu** | `ShowMenu` | `Exec` | `Exec`, `SelectedIndex: Int` | `Title=Menu`, `Items` (newline-separated) |
| **Add Menu Item** | `AddMenuItem` | `Exec`, `Label: String` | `Exec` | Appends to the most recent ShowMenu |
| **Get Menu Selection** | `GetMenuSelection` | — | `SelectedIndex: Int` | Reads the last selection |

---

## 8. Events

Event source nodes — they fire and push execution forward when something happens in the game world.

| Title | Enum key | Outputs | Notes |
|---|---|---|---|
| **On Key Press** | `OnKeyPress` | `Exec`, `Key: String` | Fires each time any key is pressed |
| **On Tick** | `OnTick` | `Exec`, `TickCount: Int` | Fires once per game tick |
| **On Entity Enter Tile** | `OnEntityEnterTile` | `Exec`, `Entity`, `X: Int`, `Y: Int` | Fires when any entity moves onto a tile |

---

## 9. Overworld

Nodes for building and navigating a persistent overworld map of named locations.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Create Overworld** | `CreateOverworld` | `Exec` | `Exec`, `Overworld` | `Name=World` |
| **Add Location** | `AddLocation` | `Exec`, `Overworld` | `Exec`, `Location` | `Name`, `WorldX`, `WorldY`, `Description` |
| **Connect Locations** | `ConnectLocations` | `Exec`, `From: Location`, `To: Location` | `Exec` | `ExitName=North`, `ReverseExitName=South` |
| **Travel To Location** | `TravelToLocation` | `Exec`, `Overworld`, `Location` | `Exec` | Teleports player |
| **Travel Via Exit** | `TravelViaExit` | `Exec`, `Overworld` | `Exec`, `Arrived: Location` | `ExitName=North` |
| **Get Current Location** | `GetCurrentLocation` | `Overworld` | `Location`, `Name: String` | — |
| **On Enter Location** | `OnEnterLocation` | — | `Exec`, `Location`, `Name: String` | Event: player arrived |
| **On Leave Location** | `OnLeaveLocation` | — | `Exec`, `Location`, `ExitUsed: String` | Event: player left |
| **Get Location Data** | `GetLocationData` | `Location` | `Value: String` | `Key=key` |
| **Set Location Data** | `SetLocationData` | `Exec`, `Location`, `Value: String` | `Exec` | `Key=key` |
| **Generate Location Map** | `GenerateLocation` | `Exec`, `Location` | `Exec`, `Map` | `Algorithm` (Cave/BSP/Drunkard), `Width`, `Height`, `FillRatio`, `Iterations` |
| **Render Overworld** | `RenderOverworld` | `Exec`, `Overworld` | `Exec` | Renders dot-map of all locations |

---

## 10. Multiplayer

Nodes for networked play. Two sub-groups: **peer-to-peer sessions** and **dedicated client-server**.

### Peer-to-Peer

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Host Session** | `HostSession` | `Exec` | `Exec`, `Session` | `SessionName`, `Port=7777`, `MaxPlayers=4`, `PlayerName=Host` |
| **Join Session** | `JoinSession` | `Exec` | `Exec`, `Session` | `Host=127.0.0.1`, `Port=7777`, `PlayerName=Player` |
| **Leave Session** | `LeaveSession` | `Exec`, `Session` | `Exec` | Disconnect or shut down |
| **Broadcast Message** | `BroadcastMessage` | `Exec`, `Session`, `Payload: String` | `Exec` | `MessageType=chat` |
| **Send Message To Player** | `SendMessageToPlayer` | `Exec`, `Session`, `Payload: String`, `TargetPlayer: String` | `Exec` | `MessageType=direct` |
| **On Message Received** | `OnMessageReceived` | — | `Exec`, `Sender: String`, `Payload: String` | `MessageType=chat` |
| **Get Connected Players** | `GetConnectedPlayers` | `Session` | `PlayerCount: Int`, `PlayerNames: String` | — |
| **Get Local Player Name** | `GetLocalPlayerName` | `Session` | `Name: String` | — |
| **Is Host** | `IsHost` | `Session` | `Result: Bool` | — |
| **Sync Entity State** | `SyncEntityState` | `Exec`, `Session`, `Entity` | `Exec` | Broadcasts position + glyph |
| **On Entity State Received** | `OnEntityStateReceived` | — | `Exec`, `EntityId: String`, `X: Int`, `Y: Int`, `Glyph: String` | — |

### Dedicated Client-Server

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Host Server** | `HostServer` | `Exec` | `Exec`, `Session` | `SessionName=Dedicated Server`, `Port=7777`, `MaxPlayers=16` |
| **Connect To Server** | `ConnectToServer` | `Exec` | `Exec`, `Session` | `Host=127.0.0.1`, `Port=7777`, `PlayerName=Player` |
| **Send To Client** | `SendToClient` | `Exec`, `Session`, `Payload: String`, `PlayerId: String` | `Exec` | `MessageType=server-direct` |
| **On Client Connected** | `OnClientConnected` | — | `Exec`, `PlayerName: String`, `PlayerId: String` | Server-side event |
| **On Client Disconnected** | `OnClientDisconnected` | — | `Exec`, `PlayerName: String`, `PlayerId: String` | Server-side event |
| **Get Network Role** | `GetNetworkRole` | `Session` | `Role: String` | Returns `Peer` / `DedicatedServer` / `AuthoritativeClient` |

> **Export note:** dedicated server graphs can be exported as a standalone **Node.js server** (`server.js`) or **Python script** (`server.py`) via *File → Export → Server*.

---

## 11. Persistence / Save-Load

Nodes for reading and writing game state to JSON save files.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Save Game** | `SaveGame` | `Exec` | `Exec`, `Success: Bool` | `Slot=slot1` |
| **Load Game** | `LoadGame` | `Exec` | `Exec`, `Success: Bool` | `Slot=slot1` |
| **Delete Save** | `DeleteSave` | `Exec` | `Exec` | `Slot=slot1` |
| **Save Slot Exists** | `SaveSlotExists` | — | `Exists: Bool` | `Slot=slot1` |
| **Set Persistent Value** | `SetPersistentValue` | `Exec`, `Value: String` | `Exec` | `Key=myVar` |
| **Get Persistent Value** | `GetPersistentValue` | — | `Value: String` | `Key=myVar`, `Default=` |

Save files are written as JSON (`<slot>.json`) in the project's `saves/` directory. They capture: entities, overworld state, faction relations, entity-faction assignments, and the global persistent key-value store.

---

## 12. Dialogue & Cutscenes

Nodes for presenting narrative content, branching dialogue, and scripted sequences.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Show Dialogue Line** | `ShowDialogueLine` | `Exec` | `Exec` | `Speaker=NPC`, `Text`, `X=0`, `Y=20` |
| **Show Dialogue Choice** | `ShowDialogueChoice` | `Exec` | `Exec`, `SelectedIndex: Int` | `Prompt`, `Choices` (newline-separated) |
| **On Dialogue Choice** | `OnDialogueChoice` | `SelectedIndex: Int` | `Exec` | `ChoiceIndex=0` |
| **Start Cutscene** | `StartCutscene` | `Exec` | `Exec` | `Name=intro`; pauses normal input |
| **End Cutscene** | `EndCutscene` | `Exec` | `Exec` | Restores normal input |
| **Wait** | `Wait` | `Exec`, `Ticks: Int` | `Exec` | `Ticks=30`; pause in-graph execution |

---

## 13. Factions & Relationships

Nodes for managing named factions and their relationship scores (−100 hostile → 100 allied).

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Create Faction** | `CreateFaction` | `Exec` | `Exec` | `Name=Bandits` |
| **Assign Entity Faction** | `AssignEntityFaction` | `Exec`, `Entity` | `Exec` | `Faction=Bandits` |
| **Set Faction Relation** | `SetFactionRelation` | `Exec`, `Value: Int` | `Exec` | `FactionA`, `FactionB` |
| **Get Faction Relation** | `GetFactionRelation` | — | `Value: Int` | `FactionA`, `FactionB` |
| **Get Entity Faction** | `GetEntityFaction` | `Entity` | `Faction: String` | — |
| **On Relation Changed** | `OnRelationChanged` | — | `Exec`, `NewValue: Int` | `FactionA`, `FactionB`, `Threshold=0` |

---

## 14. Time

Nodes for an in-game day/night clock.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Advance Time** | `AdvanceTime` | `Exec`, `Amount: Int` | `Exec`, `NewHour: Int` | `Amount=1` |
| **Get Time Of Day** | `GetTimeOfDay` | — | `Hour: Int` | Current in-game hour (0–23) |
| **Is Night** | `IsNight` | — | `Result: Bool` | `NightStart=20`, `NightEnd=6` |
| **On Time Of Day** | `OnTimeOfDay` | — | `Exec`, `Hour: Int` | Fires once at `Hour=6` |

---

## 15. Scene Tree

Godot-style hierarchical scene tree. Nodes hold children, have positions on the ASCII grid, and can run attached scripts.

**Built-in scene node types** (set via the `NodeType` property): `GridNode` · `SpriteNode` · `EntityNode` · `MapNode` · `LabelNode` · `TimerNode` · `AreaNode` · `CameraNode`

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Create Scene Node** | `SceneCreate` | `Exec`, `Parent: SceneNode` | `Exec`, `Node: SceneNode` | `NodeType=SpriteNode`, `Name=NewNode` |
| **Add Child** | `SceneAddChild` | `Exec`, `Parent: SceneNode`, `Child: SceneNode` | `Exec` | — |
| **Remove Child** | `SceneRemoveChild` | `Exec`, `Node: SceneNode` | `Exec` | Detaches from parent |
| **Find Node** | `SceneFindNode` | — | `Node: SceneNode` | `Name=NodeName` |
| **Instantiate Scene** | `SceneInstantiate` | `Exec` | `Exec`, `Root: SceneNode` | `SceneName=main` |
| **Change Scene** | `SceneChange` | `Exec` | `Exec` | `SceneName=main` |
| **Get Current Scene** | `SceneGetCurrent` | — | `Name: String` | — |
| **Set Node Position** | `SceneSetPosition` | `Exec`, `Node: SceneNode`, `X: Int`, `Y: Int` | `Exec` | — |
| **Get Node Position** | `SceneGetPosition` | `Node: SceneNode` | `X: Int`, `Y: Int` | — |
| **Set Node Active** | `SceneSetActive` | `Exec`, `Node: SceneNode`, `Active: Bool` | `Exec` | Enable/disable |
| **Set Node Visible** | `SceneSetVisible` | `Exec`, `Node: SceneNode`, `Visible: Bool` | `Exec` | Show/hide |
| **On Timer Timeout** | `OnTimerTimeout` | — | `Exec` | `TimerName=MyTimer` |
| **On Area Body Entered** | `OnAreaBodyEntered` | — | `Exec`, `Entity` | `AreaName=MyArea` |
| **On Area Body Exited** | `OnAreaBodyExited` | — | `Exec` | `AreaName=MyArea` |

---

## 16. Sprites

Nodes for registering sprites (ASCII glyph + optional graphic tile) and controlling `SpriteNode`s.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Register Sprite** | `RegisterSprite` | `Exec` | `Exec` | `Name`, `Glyph=@`, `FgColor`, `BgColor`, `ImagePath`, `TileX/Y/Width/Height`, `RenderMode=Auto` |
| **Load Sprite Sheet** | `LoadSpriteSheet` | `Exec` | `Exec` | `SheetName=tiles`, `ImagePath=tiles.png`, `TileWidth=16`, `TileHeight=16`, spacing/margin |
| **Set Sprite** | `SpriteSetSprite` | `Exec`, `Node: SceneNode` | `Exec` | `SpriteName=player` |
| **Get Sprite** | `SpriteGetSprite` | `Node: SceneNode` | `SpriteName: String` | — |
| **Set Sprite Render Mode** | `SpriteSetRenderMode` | `Exec`, `Node: SceneNode` | `Exec` | `Mode`: `AsciiOnly` / `Auto` / `GraphicPreferred` |
| **Set Sprite Playing** | `SpriteSetPlaying` | `Exec`, `Node: SceneNode`, `Playing: Bool` | `Exec` | Start/stop animation |

---

## 17. Morgue

Nodes for recording a character's death in a persistent post-mortem log file (classic roguelike feature).

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Generate Morgue File** | `GenerateMorgueFile` | `Exec`, `Entity` | `Exec`, `FilePath: String` | `Cause=Unknown cause`, `Directory=morgues` |
| **On Player Death** | `OnPlayerDeath` | — | `Exec`, `Entity`, `Cause: String` | Fires when HP ≤ 0 or triggered by script |

Typical wiring: **On Player Death → Generate Morgue File → (optional) Show Dialogue Line**

---

## 18. Advanced

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Inline Expression** | `InlineExpression` | `Exec` | `Exec`, `Result: Any` | `Expression=1 + 1` — evaluates a C# expression string |

---

## 19. Battle

Nodes for building turn-based and real-time combat systems.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Roll Dice** | `RollDice` | `Count: Int`, `Sides: Int` | `Result: Int` | `Count=1`, `Sides=6`; e.g. 2d6 |
| **Apply Damage** | `ApplyDamage` | `Exec`, `Entity`, `Amount: Float` | `Exec`, `NewHP: Float`, `IsDead: Bool` | Reduces entity `HP` stat, clamped at 0 |
| **Heal Entity** | `HealEntity` | `Exec`, `Entity`, `Amount: Float` | `Exec`, `NewHP: Float` | Increases `HP`, capped at `MaxHP` |
| **Is Entity Dead** | `IsEntityDead` | `Entity` | `IsDead: Bool` | True when `HP` ≤ 0 |
| **Start Combat** | `StartCombat` | `Exec` | `Exec` | Resets turn-order state |
| **End Turn** | `EndTurn` | `Exec` | `Exec` | Advances to the next combatant |
| **Get Initiative** | `GetInitiative` | `Entity` | `Initiative: Int` | `Modifier=0`; rolls 1d20 + Modifier |

Typical combat loop: **Start Combat → Get Initiative → [branch on IsDead] → Apply Damage → End Turn → …**

---

## 20. RPG

Nodes for experience, levelling, inventory, and equipment.

| Title | Enum key | Inputs | Outputs | Key properties |
|---|---|---|---|---|
| **Add Experience** | `AddExperience` | `Exec`, `Entity`, `Amount: Float` | `Exec`, `NewXP: Float`, `LeveledUp: Bool` | `XPToNextLevel=100` |
| **Get Level** | `GetLevel` | `Entity` | `Level: Int` | Reads `Level` stat |
| **Add To Inventory** | `AddToInventory` | `Exec`, `Entity`, `ItemName: String` | `Exec` | Appends to comma-separated `Inventory` property |
| **Remove From Inventory** | `RemoveFromInventory` | `Exec`, `Entity`, `Index: Int` | `Exec`, `ItemName: String` | Removes item at zero-based index |
| **Get Inventory Item** | `GetInventoryItem` | `Entity`, `Index: Int` | `ItemName: String` | Reads item name at index |
| **Get Inventory Size** | `GetInventorySize` | `Entity` | `Count: Int` | Number of items in inventory |
| **Equip Item** | `EquipItem` | `Exec`, `Entity`, `ItemName: String` | `Exec` | `Slot=weapon`; moves item from inventory to `Equip_<Slot>` property |
| **Get Equipped Item** | `GetEquippedItem` | `Entity` | `ItemName: String` | `Slot=weapon`; reads `Equip_<Slot>` property |

> **Storage note:** inventory items are stored as a comma-separated string in `entity.Properties["Inventory"]`.  Equipment occupies `entity.Properties["Equip_weapon"]`, `"Equip_armour"`, etc.

---

## Summary Table

| Category | Node count |
|---|---|
| Variables | 4 |
| Math & Logic | 9 |
| Control Flow | 4 |
| Map & Procgen | 10 |
| Entity | 3 |
| ASCII Display | 4 |
| Menus | 3 |
| Events | 3 |
| Overworld | 12 |
| Multiplayer | 17 |
| Persistence / Save-Load | 6 |
| Dialogue & Cutscenes | 6 |
| Factions & Relationships | 6 |
| Time | 4 |
| Scene Tree | 14 |
| Sprites | 6 |
| Morgue | 2 |
| Advanced | 1 |
| Battle | 7 |
| RPG | 8 |
| **Total** | **129** |
