using RogueEngine.Core.Engine;
using RogueEngine.Core.Models;

namespace RogueEngine.Tests;

/// <summary>
/// Tests for <see cref="MorgueFile"/>, <see cref="MorgueFileWriter"/>, and the
/// <see cref="NodeType.GenerateMorgueFile"/> / <see cref="NodeType.OnPlayerDeath"/>
/// visual-script nodes.
/// </summary>
public sealed class MorgueFileTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Entity MakePlayer(string name = "Hero", int x = 5, int y = 10)
    {
        var e = new Entity { Name = name, Glyph = '@', X = x, Y = y };
        e.Properties["HP"]    = "0";
        e.Properties["MaxHP"] = "30";
        e.Properties["STR"]   = "12";
        return e;
    }

    // ── MorgueFile model ───────────────────────────────────────────────────────

    [Fact]
    public void MorgueFile_DefaultValues_AreReasonable()
    {
        var m = new MorgueFile();
        Assert.Equal("Unknown",       m.CharacterName);
        Assert.Equal("Unknown cause", m.Cause);
        Assert.Equal(0,               m.Score);
        Assert.Empty(m.KillLog);
        Assert.Empty(m.VisitedLocations);
        Assert.Empty(m.Notes);
        Assert.Empty(m.Stats);
    }

    [Fact]
    public void MorgueFile_Duration_ReflectsRunEndedMinusStarted()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var end   = start.AddMinutes(45);
        var m = new MorgueFile { RunStarted = start, RunEnded = end };
        Assert.Equal(TimeSpan.FromMinutes(45), m.Duration);
    }

    [Fact]
    public void MorgueFile_ToString_ContainsNameAndCause()
    {
        var m = new MorgueFile { CharacterName = "Mira", Cause = "Poisoned", Score = 500 };
        var str = m.ToString();
        Assert.Contains("Mira",     str);
        Assert.Contains("Poisoned", str);
        Assert.Contains("500",      str);
    }

    // ── MorgueFileWriter.BuildFromRunState ────────────────────────────────────

    [Fact]
    public void BuildFromRunState_NullPlayer_UsesUnknownName()
    {
        var m = MorgueFileWriter.BuildFromRunState(player: null, cause: "Fell");
        Assert.Equal("Unknown", m.CharacterName);
        Assert.Equal("Fell",    m.Cause);
    }

    [Fact]
    public void BuildFromRunState_SetsCharacterName()
    {
        var player = MakePlayer("Aldric");
        var m = MorgueFileWriter.BuildFromRunState(player);
        Assert.Equal("Aldric", m.CharacterName);
    }

    [Fact]
    public void BuildFromRunState_CopiesEntityProperties()
    {
        var player = MakePlayer();
        var m = MorgueFileWriter.BuildFromRunState(player);
        Assert.Equal("0",  m.Stats["HP"]);
        Assert.Equal("30", m.Stats["MaxHP"]);
        Assert.Equal("12", m.Stats["STR"]);
    }

    [Fact]
    public void BuildFromRunState_AddsGlyphAndColour()
    {
        var player = MakePlayer();
        var m = MorgueFileWriter.BuildFromRunState(player);
        Assert.True(m.Stats.ContainsKey("Glyph"));
        Assert.True(m.Stats.ContainsKey("Colour"));
        Assert.Equal("@", m.Stats["Glyph"]);
    }

    [Fact]
    public void BuildFromRunState_PopulatesKillLog()
    {
        var kills = new[] { "rat", "goblin", "troll" };
        var m = MorgueFileWriter.BuildFromRunState(
            player: MakePlayer(), killLog: kills);
        Assert.Equal(3, m.KillLog.Count);
        Assert.Contains("troll", m.KillLog);
    }

    [Fact]
    public void BuildFromRunState_PopulatesVisitedLocations()
    {
        var locs = new[] { "Town", "Dungeon Level 1", "Dungeon Level 2" };
        var m = MorgueFileWriter.BuildFromRunState(
            player: MakePlayer(), visitedLocations: locs);
        Assert.Equal(3, m.VisitedLocations.Count);
        Assert.Equal("Town", m.VisitedLocations[0]);
    }

    [Fact]
    public void BuildFromRunState_SetsScore_AndDepth()
    {
        var m = MorgueFileWriter.BuildFromRunState(
            player: MakePlayer(), score: 1234, maxDepth: 7);
        Assert.Equal(1234, m.Score);
        Assert.Equal(7,    m.MaxDepth);
    }

    [Fact]
    public void BuildFromRunState_SetsTurnsPlayed()
    {
        var m = MorgueFileWriter.BuildFromRunState(
            player: MakePlayer(), turnsPlayed: 500);
        Assert.Equal(500, m.TurnsPlayed);
    }

    // ── MorgueFileWriter.WriteMorgue (text format) ────────────────────────────

    [Fact]
    public void WriteMorgue_ContainsCharacterName()
    {
        var writer = new MorgueFileWriter();
        var m = MorgueFileWriter.BuildFromRunState(MakePlayer("Zara"));
        var text = writer.WriteMorgue(m);
        Assert.Contains("Zara", text);
    }

    [Fact]
    public void WriteMorgue_ContainsCause()
    {
        var writer = new MorgueFileWriter();
        var m = new MorgueFile { Cause = "Eaten by a grue" };
        var text = writer.WriteMorgue(m);
        Assert.Contains("Eaten by a grue", text);
    }

    [Fact]
    public void WriteMorgue_ContainsRunSummarySection()
    {
        var writer = new MorgueFileWriter();
        var text   = writer.WriteMorgue(new MorgueFile());
        Assert.Contains("RUN SUMMARY", text);
    }

    [Fact]
    public void WriteMorgue_ContainsEngineVersion()
    {
        var writer = new MorgueFileWriter();
        var text   = writer.WriteMorgue(new MorgueFile());
        Assert.Contains(MorgueFileWriter.EngineVersion, text);
    }

    [Fact]
    public void WriteMorgue_ContainsStats_WhenPresent()
    {
        var writer = new MorgueFileWriter();
        var m = MorgueFileWriter.BuildFromRunState(MakePlayer());
        var text = writer.WriteMorgue(m);
        Assert.Contains("CHARACTER STATS", text);
        Assert.Contains("HP", text);
    }

    [Fact]
    public void WriteMorgue_ContainsKillSection_WhenPresent()
    {
        var writer = new MorgueFileWriter();
        var m = MorgueFileWriter.BuildFromRunState(
            MakePlayer(),
            killLog: ["goblin", "goblin", "rat"]);
        var text = writer.WriteMorgue(m);
        Assert.Contains("KILLS", text);
        Assert.Contains("2x", text);   // 2 goblins grouped
        Assert.Contains("1x", text);   // 1 rat
        Assert.Contains("Total: 3", text);
    }

    [Fact]
    public void WriteMorgue_ContainsLocationSection_WhenPresent()
    {
        var writer = new MorgueFileWriter();
        var m = MorgueFileWriter.BuildFromRunState(
            MakePlayer(), visitedLocations: ["Fog Town", "The Pit"]);
        var text = writer.WriteMorgue(m);
        Assert.Contains("VISITED LOCATIONS", text);
        Assert.Contains("Fog Town", text);
        Assert.Contains("The Pit", text);
    }

    [Fact]
    public void WriteMorgue_ContainsNotes_WhenPresent()
    {
        var writer = new MorgueFileWriter();
        var m = MorgueFileWriter.BuildFromRunState(
            MakePlayer(), notes: ["[MAP] Created 40x25 map", "[SPAWN] Hero @ (5,10)"]);
        var text = writer.WriteMorgue(m);
        Assert.Contains("GAME LOG", text);
        Assert.Contains("[MAP]", text);
    }

    [Fact]
    public void WriteMorgue_NoKills_OmitsKillSection()
    {
        var writer = new MorgueFileWriter();
        var m = new MorgueFile();
        var text = writer.WriteMorgue(m);
        Assert.DoesNotContain("KILLS", text);
    }

    [Fact]
    public void WriteMorgue_ThrowsOnNull()
    {
        var writer = new MorgueFileWriter();
        Assert.Throws<ArgumentNullException>(() => writer.WriteMorgue(null!));
    }

    // ── MorgueFileWriter.WriteToFile ──────────────────────────────────────────

    [Fact]
    public void WriteToFile_CreatesFile_InSpecifiedDirectory()
    {
        var writer = new MorgueFileWriter();
        var tmpDir = Path.Combine(Path.GetTempPath(), $"MorgueTest_{Guid.NewGuid():N}");
        try
        {
            var m    = MorgueFileWriter.BuildFromRunState(MakePlayer("FileHero"));
            var path = writer.WriteToFile(m, tmpDir);

            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            Assert.StartsWith(tmpDir, path);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void WriteToFile_FileContainsCharacterName()
    {
        var writer = new MorgueFileWriter();
        var tmpDir = Path.Combine(Path.GetTempPath(), $"MorgueTest_{Guid.NewGuid():N}");
        try
        {
            var m    = MorgueFileWriter.BuildFromRunState(MakePlayer("Korvan"));
            var path = writer.WriteToFile(m, tmpDir)!;
            var text = File.ReadAllText(path);
            Assert.Contains("Korvan", text);
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public void WriteToFile_FileNameContainsCharacterName()
    {
        var writer = new MorgueFileWriter();
        var tmpDir = Path.Combine(Path.GetTempPath(), $"MorgueTest_{Guid.NewGuid():N}");
        try
        {
            var m    = MorgueFileWriter.BuildFromRunState(MakePlayer("Lyra"));
            var path = writer.WriteToFile(m, tmpDir)!;
            Assert.Contains("Lyra", Path.GetFileName(path));
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    // ── NodeFactory: Morgue nodes ─────────────────────────────────────────────

    [Fact]
    public void NodeFactory_GenerateMorgueFile_HasDefinition()
    {
        var def = NodeFactory.AllDefinitions.Single(d => d.Type == NodeType.GenerateMorgueFile);
        Assert.Equal("Morgue", def.Category);
        Assert.Contains(def.InputPorts,  p => p.Name == "Entity");
        Assert.Contains(def.OutputPorts, p => p.Name == "FilePath");
        Assert.True(def.DefaultProperties.ContainsKey("Cause"));
    }

    [Fact]
    public void NodeFactory_OnPlayerDeath_HasDefinition()
    {
        var def = NodeFactory.AllDefinitions.Single(d => d.Type == NodeType.OnPlayerDeath);
        Assert.Equal("Morgue", def.Category);
        Assert.Contains(def.OutputPorts, p => p.Name == "Entity");
        Assert.Contains(def.OutputPorts, p => p.Name == "Cause");
    }

    // ── ScriptExecutor: GenerateMorgueFile node ───────────────────────────────

    [Fact]
    public void Script_GenerateMorgueFile_LogsWrittenPath()
    {
        var start  = NodeFactory.Create(NodeType.Start);
        var spawn  = NodeFactory.Create(NodeType.SpawnEntity);
        spawn.Properties["Name"]  = "Hero";
        spawn.Properties["Glyph"] = "@";
        var morgue = NodeFactory.Create(NodeType.GenerateMorgueFile);
        morgue.Properties["Cause"]     = "Killed by a goblin";
        morgue.Properties["Directory"] = Path.Combine(Path.GetTempPath(), $"MGTest_{Guid.NewGuid():N}");

        var graph = new ScriptGraph();
        graph.AddNode(start);
        graph.AddNode(spawn);
        graph.AddNode(morgue);

        graph.Connect(start.Id, start.Outputs.First(p => p.Name == "Exec").Id,
                      spawn.Id, spawn.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(spawn.Id, spawn.Outputs.First(p => p.Name == "Exec").Id,
                      morgue.Id, morgue.Inputs.First(p => p.Name == "Exec").Id);
        graph.Connect(spawn.Id, spawn.Outputs.First(p => p.Name == "Entity").Id,
                      morgue.Id, morgue.Inputs.First(p => p.Name == "Entity").Id);

        var result = new ScriptExecutor(graph).Run();

        Assert.Contains(result.Log, l => l.StartsWith("[MORGUE]"));

        // Clean up temp directory created by the node.
        var dir = morgue.Properties["Directory"];
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
