using System.Text.Json;
using RogueEngine.Core.Engine;
using RogueEngine.Core.Models;

namespace RogueEngine.Tests;

/// <summary>
/// Round-trip tests for <see cref="PersistenceManager"/>.
/// All tests use the in-memory <see cref="PersistenceManager.Serialize"/> /
/// <see cref="PersistenceManager.Deserialize"/> API so no file I/O occurs.
/// </summary>
public sealed class PersistenceTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static PersistenceManager MakeMgr() => new();

    private static OverworldManager MakeMgrWithWorld(out Overworld world)
    {
        var mgr = new OverworldManager();
        world = mgr.CreateOverworld("Test World");
        return mgr;
    }

    // ── JSON validity ──────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ProducesValidJson()
    {
        var pm  = MakeMgr();
        var json = pm.Serialize("slot1");

        // Should not throw.
        var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void Serialize_ContainsEngineVersion()
    {
        var pm  = MakeMgr();
        var json = pm.Serialize("slot1");
        Assert.Contains(PersistenceManager.EngineVersion, json);
    }

    [Fact]
    public void Serialize_ContainsSlotName()
    {
        var pm  = MakeMgr();
        var json = pm.Serialize("my_adventure");
        Assert.Contains("my_adventure", json);
    }

    // ── Global store round-trip ────────────────────────────────────────────────

    [Fact]
    public void GlobalStore_RoundTrips_AllEntries()
    {
        var pm = MakeMgr();
        pm.SetValue("score", "9999");
        pm.SetValue("bossDefeated", "true");
        pm.SetValue("playerName", "Thorin");

        var json    = pm.Serialize("slot1");
        var pm2     = MakeMgr();
        var result  = pm2.Deserialize(json);

        Assert.NotNull(result);
        Assert.Equal("9999",  pm2.GetValue("score"));
        Assert.Equal("true",  pm2.GetValue("bossDefeated"));
        Assert.Equal("Thorin", pm2.GetValue("playerName"));
    }

    [Fact]
    public void GlobalStore_Empty_RoundTripsCleanly()
    {
        var pm   = MakeMgr();
        var json = pm.Serialize("slot1");
        var pm2  = MakeMgr();
        pm2.Deserialize(json);
        Assert.Equal("fallback", pm2.GetValue("missing", "fallback"));
    }

    // ── Entity round-trip ──────────────────────────────────────────────────────

    [Fact]
    public void Entities_PreserveAllFields()
    {
        var originalId = Guid.NewGuid();
        var ent = new Entity
        {
            Name           = "Goblin",
            Glyph          = 'g',
            ForegroundColor = 0x00FF00,
            X              = 12,
            Y              = 7,
            BlocksMovement = true,
        };
        typeof(Entity).GetProperty("Id")!.SetValue(ent, originalId);
        ent.Properties["hp"] = "30";

        var pm   = MakeMgr();
        var json = pm.Serialize("slot1", entities: [ent]);
        var pm2  = MakeMgr();
        var res  = pm2.Deserialize(json);

        Assert.NotNull(res);
        Assert.Single(res!.Entities);
        var loaded = res.Entities[0];
        Assert.Equal(originalId, loaded.Id);
        Assert.Equal("Goblin",   loaded.Name);
        Assert.Equal('g',        loaded.Glyph);
        Assert.Equal(0x00FF00,   loaded.ForegroundColor);
        Assert.Equal(12,         loaded.X);
        Assert.Equal(7,          loaded.Y);
        Assert.True(loaded.BlocksMovement);
        Assert.Equal("30",       loaded.Properties["hp"]);
    }

    [Fact]
    public void MultipleEntities_AllPreserved()
    {
        var entities = Enumerable.Range(0, 5)
            .Select(i => new Entity { Name = $"Entity{i}", X = i, Y = i })
            .ToList();

        var pm   = MakeMgr();
        var json = pm.Serialize("slot1", entities: entities);
        var pm2  = MakeMgr();
        var res  = pm2.Deserialize(json);

        Assert.NotNull(res);
        Assert.Equal(5, res!.Entities.Count);
        for (var i = 0; i < 5; i++)
            Assert.Equal($"Entity{i}", res.Entities[i].Name);
    }

    // ── GameHour round-trip ────────────────────────────────────────────────────

    [Fact]
    public void GameHour_RoundTrips()
    {
        var pm   = MakeMgr();
        var json = pm.Serialize("slot1", gameHour: 14);
        var res  = pm.Deserialize(json);

        Assert.NotNull(res);
        Assert.Equal(14, res!.GameHour);
    }

    // ── Faction round-trip ─────────────────────────────────────────────────────

    [Fact]
    public void FactionRelations_RoundTrip()
    {
        var owMgr = new OverworldManager();
        owMgr.SetFactionRelation("Heroes", "Bandits", -75);
        owMgr.SetFactionRelation("Heroes", "Merchants", 50);

        var pm   = MakeMgr();
        var json = pm.Serialize("slot1", owMgr);

        var owMgr2 = new OverworldManager();
        var pm2    = MakeMgr();
        pm2.Deserialize(json, owMgr2);

        Assert.Equal(-75, owMgr2.GetFactionRelation("Heroes", "Bandits"));
        Assert.Equal(50,  owMgr2.GetFactionRelation("Heroes", "Merchants"));
    }

    [Fact]
    public void FactionRelations_AreBidirectional()
    {
        var owMgr = new OverworldManager();
        owMgr.SetFactionRelation("A", "B", 25);

        var pm   = MakeMgr();
        var json = pm.Serialize("slot1", owMgr);

        var owMgr2 = new OverworldManager();
        var pm2    = MakeMgr();
        pm2.Deserialize(json, owMgr2);

        // Relation should be accessible in either argument order.
        Assert.Equal(25, owMgr2.GetFactionRelation("A", "B"));
        Assert.Equal(25, owMgr2.GetFactionRelation("B", "A"));
    }

    [Fact]
    public void EntityFactions_RoundTrip()
    {
        var owMgr = new OverworldManager();
        var entityId = Guid.NewGuid().ToString();
        owMgr.AssignEntityFaction(entityId, "Bandits");

        var pm   = MakeMgr();
        var json = pm.Serialize("slot1", owMgr);

        var owMgr2 = new OverworldManager();
        var pm2    = MakeMgr();
        pm2.Deserialize(json, owMgr2);

        Assert.Equal("Bandits", owMgr2.GetEntityFaction(entityId));
    }

    // ── Overworld location round-trip ──────────────────────────────────────────

    [Fact]
    public void OverworldLocations_VisitedFlag_RoundTrips()
    {
        var owMgr = MakeMgrWithWorld(out var world);
        var loc   = owMgr.AddLocation(world, "Town", 5, 3);
        world.TravelTo(loc.Id);                 // marks HasBeenVisited = true

        var pm   = MakeMgr();
        var json = pm.Serialize("slot1", owMgr);

        // Create a fresh world with the same location ID.
        var owMgr2 = MakeMgrWithWorld(out var world2);
        var loc2   = owMgr2.AddLocation(world2, "Town", 5, 3);
        typeof(OverworldLocation).GetProperty("Id")!.SetValue(loc2, loc.Id);

        var pm2 = MakeMgr();
        pm2.Deserialize(json, owMgr2);

        Assert.True(loc2.HasBeenVisited);
        Assert.Equal(loc.Id, world2.CurrentLocationId);
    }

    [Fact]
    public void LocationPersistentData_RoundTrips()
    {
        var owMgr = MakeMgrWithWorld(out var world);
        var loc   = owMgr.AddLocation(world, "Cave", 10, 10);
        loc.PersistentData["bossSpawned"] = "true";
        loc.PersistentData["treasureLeft"] = "3";

        var pm   = MakeMgr();
        var json = pm.Serialize("slot1", owMgr);

        var owMgr2 = MakeMgrWithWorld(out var world2);
        var loc2   = owMgr2.AddLocation(world2, "Cave", 10, 10);
        typeof(OverworldLocation).GetProperty("Id")!.SetValue(loc2, loc.Id);

        var pm2 = MakeMgr();
        pm2.Deserialize(json, owMgr2);

        Assert.Equal("true", loc2.PersistentData["bossSpawned"]);
        Assert.Equal("3",    loc2.PersistentData["treasureLeft"]);
    }

    [Fact]
    public void LocationEntities_RoundTrip()
    {
        var owMgr = MakeMgrWithWorld(out var world);
        var loc   = owMgr.AddLocation(world, "Dungeon", 0, 0);
        loc.Entities.Add(new Entity { Name = "Rat", Glyph = 'r', X = 3, Y = 3 });

        var pm   = MakeMgr();
        var json = pm.Serialize("slot1", owMgr);

        var owMgr2 = MakeMgrWithWorld(out var world2);
        var loc2   = owMgr2.AddLocation(world2, "Dungeon", 0, 0);
        typeof(OverworldLocation).GetProperty("Id")!.SetValue(loc2, loc.Id);

        var pm2 = MakeMgr();
        pm2.Deserialize(json, owMgr2);

        Assert.Single(loc2.Entities);
        Assert.Equal("Rat", loc2.Entities[0].Name);
        Assert.Equal('r',   loc2.Entities[0].Glyph);
    }

    // ── File-based API ─────────────────────────────────────────────────────────

    [Fact]
    public void File_SaveAndLoad_RoundTrip()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"rg_test_{Guid.NewGuid():N}");
        try
        {
            var pm = MakeMgr();
            pm.SetValue("level", "5");
            pm.Save("hero", entities: [new Entity { Name = "Hero", Glyph = '@', X = 1, Y = 1 }],
                saveDirectory: tmpDir);

            Assert.True(pm.SlotExists("hero", tmpDir));

            var pm2 = MakeMgr();
            var res = pm2.Load("hero", saveDirectory: tmpDir);

            Assert.NotNull(res);
            Assert.Single(res!.Entities);
            Assert.Equal("Hero", res.Entities[0].Name);
            Assert.Equal("5", pm2.GetValue("level"));
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void File_ListSlots_ReturnsAllSavedSlots()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"rg_test_{Guid.NewGuid():N}");
        try
        {
            var pm = MakeMgr();
            pm.Save("slot1", saveDirectory: tmpDir);
            pm.Save("slot2", saveDirectory: tmpDir);
            pm.Save("slot3", saveDirectory: tmpDir);

            var slots = pm.ListSlots(tmpDir);
            Assert.Equal(3, slots.Count);
            Assert.Contains("slot1", slots);
            Assert.Contains("slot2", slots);
            Assert.Contains("slot3", slots);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void File_DeleteSlot_RemovesFile()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"rg_test_{Guid.NewGuid():N}");
        try
        {
            var pm = MakeMgr();
            pm.Save("temp", saveDirectory: tmpDir);
            Assert.True(pm.SlotExists("temp", tmpDir));

            pm.DeleteSlot("temp", tmpDir);
            Assert.False(pm.SlotExists("temp", tmpDir));
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void File_Load_NonexistentSlot_ReturnsNull()
    {
        var pm = MakeMgr();
        var res = pm.Load("doesNotExist",
            saveDirectory: Path.Combine(Path.GetTempPath(), $"rg_test_{Guid.NewGuid():N}"));
        Assert.Null(res);
    }

    [Fact]
    public void File_ListSlots_NonexistentDirectory_ReturnsEmpty()
    {
        var pm    = MakeMgr();
        var slots = pm.ListSlots(Path.Combine(Path.GetTempPath(), $"rg_missing_{Guid.NewGuid():N}"));
        Assert.Empty(slots);
    }

    // ── Corrupt / invalid input ────────────────────────────────────────────────

    [Fact]
    public void Deserialize_NullOrEmpty_ReturnsNull()
    {
        var pm = MakeMgr();
        Assert.Null(pm.Deserialize(null!));
        Assert.Null(pm.Deserialize(string.Empty));
        Assert.Null(pm.Deserialize("   "));
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsNull()
    {
        var pm = MakeMgr();
        Assert.Null(pm.Deserialize("{not valid json"));
    }

    // ── Idempotency ────────────────────────────────────────────────────────────

    [Fact]
    public void Serialize_ThenDeserialize_ThenSerialize_ProducesEquivalentJson()
    {
        var pm = MakeMgr();
        pm.SetValue("x", "42");
        var json1 = pm.Serialize("s");

        var pm2   = MakeMgr();
        pm2.Deserialize(json1);
        var json2 = pm2.Serialize("s");

        // Both JSONs should contain the same value (field ordering may differ slightly
        // by timestamp, so we just check the key data).
        Assert.Contains("\"x\"", json2);
        Assert.Contains("\"42\"", json2);
    }
}
