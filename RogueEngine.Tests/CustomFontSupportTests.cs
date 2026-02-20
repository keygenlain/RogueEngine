using RogueEngine.Core.Engine;
using RogueEngine.Core.Models;

namespace RogueEngine.Tests;

public sealed class CustomFontSupportTests
{
    [Fact]
    public void ScriptGraphSerializer_RoundTrips_CustomFontPath()
    {
        var project = new GameProject
        {
            Name = "Font Test",
            FontFamily = "Consolas",
            CustomFontPath = "https://example.com/fonts/myfont.woff2",
            FontSizePx = 18,
        };

        var json = ScriptGraphSerializer.Serialize(project);
        var loaded = ScriptGraphSerializer.Deserialize(json);

        Assert.Equal(project.CustomFontPath, loaded.CustomFontPath);
        Assert.Equal(project.FontFamily, loaded.FontFamily);
        Assert.Equal(project.FontSizePx, loaded.FontSizePx);
    }

    [Fact]
    public async Task GameExporter_Html5_Embeds_CustomFontFile_AsFontFace()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"RogueFontTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var fontPath = Path.Combine(tempRoot, "custom.ttf");
            await File.WriteAllBytesAsync(fontPath, [0, 1, 2, 3, 4, 5, 6, 7]);

            var project = new GameProject
            {
                Name = "Font Export Test",
                FontFamily = "Courier New",
                FontSizePx = 16,
                CustomFontPath = fontPath,
            };

            var exporter = new GameExporter(project);
            var outPath = await exporter.ExportAsync(ExportTarget.Html5, tempRoot);
            var html = await File.ReadAllTextAsync(outPath);

            Assert.Contains("@font-face", html);
            Assert.Contains("RogueCustomFont", html);
            Assert.Contains("data:font/ttf;base64", html);
            Assert.Contains("const FONT = '16px \"RogueCustomFont\", \"Courier New\", monospace';", html);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }
}
