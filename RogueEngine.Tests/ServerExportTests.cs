using RogueEngine.Core.Engine;
using RogueEngine.Core.Models;

namespace RogueEngine.Tests;

/// <summary>
/// Tests for the Node.js and Python server export targets added to
/// <see cref="GameExporter"/>.
/// </summary>
public sealed class ServerExportTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static GameProject MakeProject(string name = "TestGame") => new()
    {
        Name          = name,
        DisplayWidth  = 80,
        DisplayHeight = 25,
        FontFamily    = "Consolas",
        FontSizePx    = 14,
    };

    private static string ExportSync(ExportTarget target, GameProject project)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"RgExTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(project);
            return exporter.ExportAsync(target, dir).GetAwaiter().GetResult();
        }
        finally
        {
            // Callers that need the file must copy it before this helper returns;
            // we do NOT delete here so tests can inspect the output.
        }
    }

    // ── ExportTarget enum ──────────────────────────────────────────────────────

    [Fact]
    public void ExportTarget_HasNodeJsServer()
    {
        Assert.True(Enum.IsDefined(typeof(ExportTarget), ExportTarget.NodeJsServer));
    }

    [Fact]
    public void ExportTarget_HasPythonServer()
    {
        Assert.True(Enum.IsDefined(typeof(ExportTarget), ExportTarget.PythonServer));
    }

    // ── Node.js export ─────────────────────────────────────────────────────────

    [Fact]
    public async Task NodeJsServer_Export_CreatesServerJsFile()
    {
        var dir     = Path.Combine(Path.GetTempPath(), $"NodeJsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject("NodeGame"));
            var path = await exporter.ExportAsync(ExportTarget.NodeJsServer, dir);

            Assert.Equal(Path.Combine(dir, "server.js"), path);
            Assert.True(File.Exists(path));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task NodeJsServer_Export_FileContainsRequireNet()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"NodeJsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.NodeJsServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("require('net')", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task NodeJsServer_Export_FileContainsProjectName()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"NodeJsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject("DungeonCrawler"));
            var path = await exporter.ExportAsync(ExportTarget.NodeJsServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("DungeonCrawler", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task NodeJsServer_Export_FileContainsBroadcastFunction()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"NodeJsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.NodeJsServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("function broadcast", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task NodeJsServer_Export_FileContainsTargetPlayerIdRouting()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"NodeJsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.NodeJsServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("targetPlayerId", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task NodeJsServer_Export_FileContainsServerTickBroadcast()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"NodeJsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.NodeJsServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("server-tick", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task NodeJsServer_Export_FileContainsEmbeddedProjectJson()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"NodeJsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var project = MakeProject("EmbedTest");
            var exporter = new GameExporter(project);
            var path = await exporter.ExportAsync(ExportTarget.NodeJsServer, dir);
            var text = await File.ReadAllTextAsync(path);
            // The project name is embedded in the JSON payload.
            Assert.Contains("EmbedTest", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Python export ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PythonServer_Export_CreatesServerPyFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject("PyGame"));
            var path = await exporter.ExportAsync(ExportTarget.PythonServer, dir);

            Assert.Equal(Path.Combine(dir, "server.py"), path);
            Assert.True(File.Exists(path));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task PythonServer_Export_FileContainsImportSocket()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.PythonServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("import socket", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task PythonServer_Export_FileContainsImportThreading()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.PythonServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("import threading", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task PythonServer_Export_FileContainsBroadcastFunction()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.PythonServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("def broadcast", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task PythonServer_Export_FileContainsTargetPlayerIdRouting()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.PythonServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("targetPlayerId", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task PythonServer_Export_FileContainsServerTickBroadcast()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.PythonServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("server-tick", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task PythonServer_Export_FileContainsProjectName()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject("MyCrawler"));
            var path = await exporter.ExportAsync(ExportTarget.PythonServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("MyCrawler", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task PythonServer_Export_FileContainsMainEntryPoint()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.PythonServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("if __name__ == '__main__'", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Wire protocol compatibility checks ─────────────────────────────────────

    [Fact]
    public async Task NodeJsServer_Export_HandlesJoinMessageType()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"NodeJsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.NodeJsServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("'join'", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task PythonServer_Export_HandlesJoinMessageType()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.PythonServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("'join'", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task NodeJsServer_Export_HandlesServerWelcomeMessage()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"NodeJsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.NodeJsServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("server-welcome", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task PythonServer_Export_HandlesServerWelcomeMessage()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"PyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var exporter = new GameExporter(MakeProject());
            var path = await exporter.ExportAsync(ExportTarget.PythonServer, dir);
            var text = await File.ReadAllTextAsync(path);
            Assert.Contains("server-welcome", text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
