using System.Diagnostics;
using RogueEngine.Core.Models;

namespace RogueEngine.Core.Engine;

/// <summary>
/// Export targets supported by <see cref="GameExporter"/>.
/// </summary>
public enum ExportTarget
{
    /// <summary>Self-contained Windows x64 executable.</summary>
    Windows,
    /// <summary>Self-contained Linux x64 executable.</summary>
    Linux,
    /// <summary>Single-file HTML5 page playable in any modern browser.</summary>
    Html5,
}

/// <summary>
/// Reports incremental progress during an export operation.
/// </summary>
/// <param name="message">Human-readable status message.</param>
/// <param name="percent">Progress percentage 0–100.</param>
public delegate void ExportProgressCallback(string message, int percent);

/// <summary>
/// Exports a <see cref="GameProject"/> to a deployable artefact.
///
/// <list type="bullet">
///   <item>
///     <term>Windows / Linux</term>
///     <description>
///       Generates a self-contained .NET 8 console application, embeds the
///       serialised game data, and calls <c>dotnet publish</c> with the
///       appropriate runtime identifier (<c>win-x64</c> / <c>linux-x64</c>).
///       Requires the .NET 8 SDK to be installed on the build machine.
///     </description>
///   </item>
///   <item>
///     <term>HTML5</term>
///     <description>
///       Produces a single self-contained <c>index.html</c> file that
///       embeds a JavaScript ASCII runtime and the serialised game data.
///       No server is required – the file can be opened directly in a browser
///       or uploaded to any static-file host (itch.io, GitHub Pages, etc.).
///     </description>
///   </item>
/// </list>
/// </summary>
public sealed class GameExporter
{
    private readonly GameProject _project;
    private readonly ExportProgressCallback? _progress;

    /// <summary>
    /// Creates a new exporter for the given <paramref name="project"/>.
    /// </summary>
    /// <param name="project">The project to export.</param>
    /// <param name="progress">Optional callback for progress reporting.</param>
    public GameExporter(GameProject project, ExportProgressCallback? progress = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        _project = project;
        _progress = progress;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Exports the project to the specified <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="target">The export platform.</param>
    /// <param name="outputDirectory">
    /// Directory where exported files are written.
    /// Created automatically if it does not exist.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// The path to the primary exported artefact
    /// (the executable or the HTML file).
    /// </returns>
    /// <exception cref="ExportException">
    /// Thrown when the export fails (e.g. SDK not found, build error).
    /// </exception>
    public async Task<string> ExportAsync(
        ExportTarget target,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        Report("Serialising project…", 5);
        var projectJson = ScriptGraphSerializer.Serialize(_project);

        return target switch
        {
            ExportTarget.Windows => await ExportNativeAsync("win-x64", ".exe", projectJson, outputDirectory, cancellationToken),
            ExportTarget.Linux   => await ExportNativeAsync("linux-x64", "", projectJson, outputDirectory, cancellationToken),
            ExportTarget.Html5   => ExportHtml5(projectJson, outputDirectory),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };
    }

    // ── Windows / Linux export ─────────────────────────────────────────────────

    private async Task<string> ExportNativeAsync(
        string rid, string exeExtension,
        string projectJson, string outputDirectory,
        CancellationToken ct)
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"RogueExport_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);

        try
        {
            Report($"Generating runtime project ({rid})…", 15);
            WriteNativeProject(tmpDir, projectJson);

            Report("Calling dotnet publish…", 30);
            var exeName = SanitiseName(_project.Name);
            await RunPublishAsync(tmpDir, rid, exeName, outputDirectory, ct);

            var exePath = Path.Combine(outputDirectory, exeName + exeExtension);
            Report("Export complete.", 100);
            return exePath;
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private void WriteNativeProject(string dir, string projectJson)
    {
        // ── .csproj ────────────────────────────────────────────────────────────
        File.WriteAllText(Path.Combine(dir, "GameRunner.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net8.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <PublishSingleFile>true</PublishSingleFile>
                <SelfContained>true</SelfContained>
                <TrimmerRootDescriptor>linker.xml</TrimmerRootDescriptor>
              </PropertyGroup>
            </Project>
            """);

        // ── Linker descriptor (keeps enum names for JSON) ──────────────────────
        File.WriteAllText(Path.Combine(dir, "linker.xml"), """
            <linker>
              <assembly fullname="GameRunner" preserve="all"/>
            </linker>
            """);

        // ── Embedded game data ─────────────────────────────────────────────────
        File.WriteAllText(Path.Combine(dir, "GameData.json"), projectJson);

        // ── Runtime source ─────────────────────────────────────────────────────
        File.WriteAllText(Path.Combine(dir, "Program.cs"),
            BuildConsoleProgramSource(_project));
    }

    private static async Task RunPublishAsync(
        string projectDir, string rid, string exeName,
        string outputDir, CancellationToken ct)
    {
        var args = $"publish \"{Path.Combine(projectDir, "GameRunner.csproj")}\" " +
                   $"-r {rid} --self-contained true " +
                   $"-c Release " +
                   $"-o \"{outputDir}\"";

        var psi = new ProcessStartInfo("dotnet", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = projectDir,
        };

        using var proc = Process.Start(psi)
            ?? throw new ExportException("Failed to start dotnet process.");

        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
            throw new ExportException(
                $"dotnet publish exited with code {proc.ExitCode}.\n{stderr}\n{stdout}");
    }

    // ── HTML5 export ───────────────────────────────────────────────────────────

    private string ExportHtml5(string projectJson, string outputDirectory)
    {
        Report("Generating HTML5 export…", 20);
        var html = BuildHtml5Page(projectJson);
        var outPath = Path.Combine(outputDirectory, "index.html");
        File.WriteAllText(outPath, html);
        Report("HTML5 export complete.", 100);
        return outPath;
    }

    // ── Source generation: Console Program.cs ─────────────────────────────────

    private static string BuildConsoleProgramSource(GameProject project) => $$"""
        // Auto-generated by RogueEngine – do not edit.
        using System;
        using System.IO;
        using System.Collections.Generic;
        using System.Linq;
        using System.Text.Json;
        using System.Text.Json.Serialization;

        // ── Minimal embedded runtime ───────────────────────────────────────────
        {{EmbeddedRuntimeSource}}
        // ── Entry point ────────────────────────────────────────────────────────

        var json = File.ReadAllText("GameData.json");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
        var project = JsonSerializer.Deserialize<GameProjectData>(json, options)!;

        Console.Title = project.Name;
        Console.CursorVisible = false;

        var runner = new ConsoleGameRunner(project);
        runner.Run();
        """;

    // ── Embedded C# runtime (single-file, no external deps) ───────────────────

    private const string EmbeddedRuntimeSource = """
        // ── Data model ─────────────────────────────────────────────────────────
        record GameProjectData(string Name, int DisplayWidth, int DisplayHeight,
            Guid? StartGraphId, List<GraphData> Graphs);
        record GraphData(Guid Id, string Name, List<NodeData> Nodes,
            List<ConnectionData> Connections);
        record NodeData(Guid Id, string Type, string Title, double X, double Y,
            List<PortData> Inputs, List<PortData> Outputs,
            Dictionary<string, string> Properties);
        record PortData(Guid Id, string Name, string DataType, bool IsInput,
            string DefaultValue);
        record ConnectionData(Guid Id, Guid SourceNodeId, Guid SourcePortId,
            Guid TargetNodeId, Guid TargetPortId);

        // ── Console runner ──────────────────────────────────────────────────────
        sealed class ConsoleGameRunner
        {
            private readonly GameProjectData _project;
            private readonly char[,] _screen;
            private readonly ConsoleColor[,] _fg;
            private readonly ConsoleColor[,] _bg;
            private int _tickCount;

            public ConsoleGameRunner(GameProjectData project)
            {
                _project = project;
                _screen = new char[project.DisplayWidth, project.DisplayHeight];
                _fg = new ConsoleColor[project.DisplayWidth, project.DisplayHeight];
                _bg = new ConsoleColor[project.DisplayWidth, project.DisplayHeight];
                ClearScreen();
            }

            public void Run()
            {
                var graph = _project.Graphs.FirstOrDefault(
                    g => g.Id == _project.StartGraphId) ?? _project.Graphs.FirstOrDefault();
                if (graph is null) { Console.WriteLine("No graphs found."); return; }

                var executor = new MiniExecutor(graph, _screen, _fg, _bg,
                    _project.DisplayWidth, _project.DisplayHeight);
                executor.Run();
                Render();

                // Main game loop: tick on each keypress.
                while (true)
                {
                    var key = Console.ReadKey(intercept: true);
                    executor.FireKeyPress(key.KeyChar.ToString());
                    executor.Tick(++_tickCount);
                    Render();
                }
            }

            private void ClearScreen()
            {
                for (var x = 0; x < _project.DisplayWidth; x++)
                for (var y = 0; y < _project.DisplayHeight; y++)
                {
                    _screen[x, y] = ' ';
                    _fg[x, y] = ConsoleColor.Gray;
                    _bg[x, y] = ConsoleColor.Black;
                }
            }

            private void Render()
            {
                Console.SetCursorPosition(0, 0);
                for (var y = 0; y < _project.DisplayHeight; y++)
                {
                    for (var x = 0; x < _project.DisplayWidth; x++)
                    {
                        Console.ForegroundColor = _fg[x, y];
                        Console.BackgroundColor = _bg[x, y];
                        Console.Write(_screen[x, y]);
                    }
                    Console.ResetColor();
                    if (y < _project.DisplayHeight - 1) Console.WriteLine();
                }
            }
        }

        // ── Minimal script executor ─────────────────────────────────────────────
        sealed class MiniExecutor
        {
            private readonly GraphData _graph;
            private readonly char[,] _screen;
            private readonly ConsoleColor[,] _fg;
            private readonly ConsoleColor[,] _bg;
            private readonly int _width, _height;
            private readonly Dictionary<Guid, object?> _vals = new();
            private readonly HashSet<Guid> _inProg = new();
            private int _lastMenu;

            public MiniExecutor(GraphData graph, char[,] screen,
                ConsoleColor[,] fg, ConsoleColor[,] bg, int w, int h)
            {
                _graph = graph; _screen = screen; _fg = fg; _bg = bg;
                _width = w; _height = h;
            }

            public void Run()
            {
                _vals.Clear(); _inProg.Clear();
                var start = _graph.Nodes.FirstOrDefault(n =>
                    n.Type.Equals("start", StringComparison.OrdinalIgnoreCase));
                if (start is null) return;
                ExecChain(start, "Exec");
            }

            public void Tick(int tick)
            {
                foreach (var n in _graph.Nodes.Where(n =>
                    n.Type.Equals("onTick", StringComparison.OrdinalIgnoreCase)))
                {
                    SetPort(n, "TickCount", tick);
                    ExecChain(n, "Exec");
                }
            }

            public void FireKeyPress(string key)
            {
                foreach (var n in _graph.Nodes.Where(n =>
                    n.Type.Equals("onKeyPress", StringComparison.OrdinalIgnoreCase)))
                {
                    SetPort(n, "Key", key);
                    ExecChain(n, "Exec");
                }
            }

            private void ExecChain(NodeData node, string port)
            {
                var srcPort = node.Outputs.FirstOrDefault(p => p.Name == port);
                if (srcPort is null) return;
                var conn = _graph.Connections.FirstOrDefault(
                    c => c.SourceNodeId == node.Id && c.SourcePortId == srcPort.Id);
                if (conn is null) return;
                var next = _graph.Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
                if (next is null) return;
                ExecNode(next);
            }

            private void ExecNode(NodeData node)
            {
                var t = node.Type.ToLowerInvariant();
                switch (t)
                {
                    case "variableint":
                        SetPort(node, "Value", ParseInt(node.Properties.GetValueOrDefault("Value","0")));
                        break;
                    case "variablefloat":
                        SetPort(node, "Value", ParseFloat(node.Properties.GetValueOrDefault("Value","0")));
                        break;
                    case "variablestring":
                        SetPort(node, "Value", node.Properties.GetValueOrDefault("Value",""));
                        break;
                    case "variablebool":
                        SetPort(node, "Value", ParseBool(node.Properties.GetValueOrDefault("Value","false")));
                        break;
                    case "mathadd":
                        SetPort(node, "Result", ResolveFloat(node,"A") + ResolveFloat(node,"B")); break;
                    case "mathsubtract":
                        SetPort(node, "Result", ResolveFloat(node,"A") - ResolveFloat(node,"B")); break;
                    case "mathmultiply":
                        SetPort(node, "Result", ResolveFloat(node,"A") * ResolveFloat(node,"B")); break;
                    case "mathdivide":
                    { var b = ResolveFloat(node,"B"); SetPort(node,"Result", b==0?0:ResolveFloat(node,"A")/b); break; }
                    case "randomint":
                    { var mn=ResolveInt(node,"Min"); var mx=ResolveInt(node,"Max");
                      if(mx<=mn)mx=mn+1;
                      SetPort(node,"Value", new Random().Next(mn,mx)); break; }
                    case "branch":
                        ExecChain(node, ResolveBool(node,"Condition") ? "True" : "False"); break;
                    case "start": ExecChain(node, "Exec"); break;
                    case "forloop":
                    { var cnt=Math.Min(ResolveInt(node,"Count"),10000);
                      for(var i=0;i<cnt;i++){ SetPort(node,"Index",i); ExecChain(node,"Loop Body"); }
                      ExecChain(node,"Completed"); break; }
                    case "createmap":
                    { var w=Math.Max(1,ResolveInt(node,"Width")); var h=Math.Max(1,ResolveInt(node,"Height"));
                      // Store map as (w,h) pair; rendering uses the screen directly.
                      SetPort(node,"Map",(w,h));
                      ExecChain(node,"Exec"); break; }
                    case "printtext":
                    { var txt=ResolveString(node,"Text"); var px=ResolveInt(node,"X"); var py=ResolveInt(node,"Y");
                      for(var i=0;i<txt.Length;i++)
                      { var tx=px+i; if(tx>=0&&tx<_width&&py>=0&&py<_height) _screen[tx,py]=txt[i]; }
                      ExecChain(node,"Exec"); break; }
                    case "drawchar":
                    { var ch=node.Properties.GetValueOrDefault("Char","?");
                      var px=ResolveInt(node,"X"); var py=ResolveInt(node,"Y");
                      if(px>=0&&px<_width&&py>=0&&py<_height) _screen[px,py]=ch.Length>0?ch[0]:' ';
                      ExecChain(node,"Exec"); break; }
                    case "cleardisplay":
                    { for(var x=0;x<_width;x++) for(var y=0;y<_height;y++) _screen[x,y]=' ';
                      ExecChain(node,"Exec"); break; }
                    case "showmenu":
                    { var title=node.Properties.GetValueOrDefault("Title","Menu");
                      var items=node.Properties.GetValueOrDefault("Items","").Split('\n');
                      // Render menu inline then wait for key.
                      int mx2=2, my2=2;
                      PrintAt(mx2, my2++, title, ConsoleColor.Yellow);
                      for(var i=0;i<items.Length;i++) PrintAt(mx2, my2++, $"{i+1}. {items[i]}", ConsoleColor.White);
                      var k=Console.ReadKey(true);
                      _lastMenu = int.TryParse(k.KeyChar.ToString(), out var sel) ? sel-1 : 0;
                      SetPort(node,"SelectedIndex",_lastMenu);
                      ExecChain(node,"Exec"); break; }
                    default:
                        ExecChain(node,"Exec"); break;
                }
            }

            private void PrintAt(int x, int y, string text, ConsoleColor color)
            {
                for(var i=0;i<text.Length;i++)
                { var tx=x+i; if(tx>=0&&tx<_width&&y>=0&&y<_height){ _screen[tx,y]=text[i]; _fg[tx,y]=color; } }
            }

            private void SetPort(NodeData node, string name, object? val)
            {
                var p=node.Outputs.FirstOrDefault(x=>x.Name==name); if(p is null) return;
                _vals[p.Id]=val;
            }

            private object? ResolveAny(NodeData node, string name)
            {
                var port=node.Inputs.FirstOrDefault(p=>p.Name==name); if(port is null) return null;
                var conn=_graph.Connections.FirstOrDefault(c=>c.TargetNodeId==node.Id&&c.TargetPortId==port.Id);
                if(conn is not null)
                {
                    if(_vals.TryGetValue(conn.SourcePortId, out var cv)) return cv;
                    var src=_graph.Nodes.FirstOrDefault(n=>n.Id==conn.SourceNodeId);
                    if(src is not null && !_inProg.Contains(src.Id))
                    { _inProg.Add(src.Id); try{ ExecNode(src); }finally{ _inProg.Remove(src.Id); }
                      if(_vals.TryGetValue(conn.SourcePortId,out var cv2)) return cv2; }
                }
                if(node.Properties.TryGetValue(name, out var pv) && !string.IsNullOrEmpty(pv)) return pv;
                return port.DefaultValue;
            }

            private int ResolveInt(NodeData n, string p) =>
                ResolveAny(n,p) switch { int i=>i, float f=>(int)f, double d=>(int)d, string s=>ParseInt(s), _=>0 };
            private float ResolveFloat(NodeData n, string p) =>
                ResolveAny(n,p) switch { float f=>f, int i=>(float)i, double d=>(float)d, string s=>ParseFloat(s), _=>0f };
            private bool ResolveBool(NodeData n, string p) =>
                ResolveAny(n,p) switch { bool b=>b, int i=>i!=0, string s=>ParseBool(s), _=>false };
            private string ResolveString(NodeData n, string p) => ResolveAny(n,p)?.ToString() ?? "";

            static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;
            static float ParseFloat(string s) => float.TryParse(s,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
            static bool ParseBool(string s) => s.Equals("true", StringComparison.OrdinalIgnoreCase)||s=="1";
        }
        """;

    // ── HTML5 page generation ──────────────────────────────────────────────────

    private string BuildHtml5Page(string projectJson)
    {
        var escapedJson = projectJson.Replace("</script>", "<\\/script>");
        var title = System.Security.SecurityElement.Escape(_project.Name) ?? _project.Name;
        var fontFamily = _project.FontFamily;
        var fontSize = _project.FontSizePx;
        var cols = _project.DisplayWidth;
        var rows = _project.DisplayHeight;

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1"/>
              <title>{{title}}</title>
              <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body { background: #000; display: flex; justify-content: center;
                       align-items: flex-start; min-height: 100vh; padding: 20px; }
                #game-container { position: relative; }
                canvas#gameCanvas {
                  display: block;
                  image-rendering: pixelated;
                  cursor: default;
                  outline: none;
                  background: #000;
                }
                #status-bar {
                  font-family: monospace; font-size: 11px; color: #555;
                  margin-top: 4px; text-align: center;
                }
              </style>
            </head>
            <body>
              <div id="game-container">
                <canvas id="gameCanvas" tabindex="0"></canvas>
                <div id="status-bar">{{System.Security.SecurityElement.Escape(_project.Name)}} — Press any key to start</div>
              </div>
              <script>
            //==============================================================
            // RogueEngine HTML5 Runtime  (auto-generated — do not edit)
            //==============================================================

            const PROJECT = {{projectJson}};

            //──────────────────────────────────────────────────────────────
            // ASCII Display
            //──────────────────────────────────────────────────────────────
            const COLS = {{cols}}, ROWS = {{rows}};
            const FONT = '{{fontSize}}px "{{fontFamily}}", monospace';

            // Measure a single character to size the canvas.
            (function sizeCanvas() {
              const tmp = document.createElement('canvas');
              const ctx = tmp.getContext('2d');
              ctx.font = FONT;
              const m = ctx.measureText('M');
              window._charW = Math.ceil(m.width);
              window._charH = {{fontSize}} + 4;
              const canvas = document.getElementById('gameCanvas');
              canvas.width  = window._charW * COLS;
              canvas.height = window._charH * ROWS;
            })();

            const canvas = document.getElementById('gameCanvas');
            const ctx = canvas.getContext('2d');

            // Screen buffers: [col][row]
            let screen    = Array.from({length:COLS}, ()=>Array(ROWS).fill(' '));
            let screenFg  = Array.from({length:COLS}, ()=>Array(ROWS).fill('#aaaaaa'));
            let screenBg  = Array.from({length:COLS}, ()=>Array(ROWS).fill('#000000'));

            function clearScreen() {
              for (let x=0;x<COLS;x++) for (let y=0;y<ROWS;y++) {
                screen[x][y]=' '; screenFg[x][y]='#aaaaaa'; screenBg[x][y]='#000000';
              }
            }

            function renderScreen() {
              const cw = window._charW, ch = window._charH;
              ctx.font = FONT;
              ctx.textBaseline = 'top';
              for (let y=0;y<ROWS;y++) for (let x=0;x<COLS;x++) {
                ctx.fillStyle = screenBg[x][y];
                ctx.fillRect(x*cw, y*ch, cw, ch);
                ctx.fillStyle = screenFg[x][y];
                ctx.fillText(screen[x][y], x*cw, y*ch);
              }
            }

            function drawCharAt(x, y, ch, fg='#ffffff', bg='#000000') {
              if (x<0||x>=COLS||y<0||y>=ROWS) return;
              screen[x][y]=ch; screenFg[x][y]=fg; screenBg[x][y]=bg;
            }

            function printTextAt(x, y, text, fg='#ffffff', bg='#000000') {
              for (let i=0;i<text.length;i++) drawCharAt(x+i, y, text[i], fg, bg);
            }

            //──────────────────────────────────────────────────────────────
            // Colour helpers
            //──────────────────────────────────────────────────────────────
            function hexColor(rgb) {
              if (!rgb) return '#ffffff';
              const s = String(rgb).replace('#','');
              const n = parseInt(s, 16);
              if (isNaN(n)) return '#ffffff';
              return '#' + n.toString(16).padStart(6,'0');
            }

            //──────────────────────────────────────────────────────────────
            // Graph helpers
            //──────────────────────────────────────────────────────────────
            function findPort(node, name, isInput) {
              const arr = isInput ? node.inputs : node.outputs;
              return arr.find(p => p.name === name);
            }
            function findConnection(graph, nodeId, portId, isSource) {
              return graph.connections.find(c =>
                isSource
                  ? c.sourceNodeId === nodeId && c.sourcePortId === portId
                  : c.targetNodeId === nodeId && c.targetPortId === portId);
            }
            function findNode(graph, id) { return graph.nodes.find(n => n.id === id); }

            //──────────────────────────────────────────────────────────────
            // Mini executor
            //──────────────────────────────────────────────────────────────
            class Executor {
              constructor(graph) {
                this.graph   = graph;
                this.vals    = {};    // portId → value
                this.inProg  = new Set();
                this.lastMenu = 0;
                this.activeMap = null;
                this.entities  = [];
              }

              run() { this.vals={}; this.inProg.clear();
                const s = this.graph.nodes.find(n=>n.type==='start'||n.type==='Start');
                if (s) this.execChain(s,'Exec');
              }
              tick(t) {
                this.graph.nodes.filter(n=>n.type==='onTick'||n.type==='OnTick').forEach(n=>{
                  this.setPort(n,'TickCount',t); this.execChain(n,'Exec');
                });
              }
              fireKey(key) {
                this.graph.nodes.filter(n=>n.type==='onKeyPress'||n.type==='OnKeyPress').forEach(n=>{
                  this.setPort(n,'Key',key); this.execChain(n,'Exec');
                });
              }

              execChain(node, outPortName) {
                const p = findPort(node, outPortName, false);
                if (!p) return;
                const conn = findConnection(this.graph, node.id, p.id, true);
                if (!conn) return;
                const next = findNode(this.graph, conn.targetNodeId);
                if (next) this.execNode(next);
              }

              execNode(node) {
                const t = node.type;
                switch(t) {
                  case 'start': case 'Start':
                    this.execChain(node,'Exec'); break;

                  case 'variableInt': case 'VariableInt':
                    this.setPort(node,'Value', parseInt(node.properties.Value||'0',10)||0); break;
                  case 'variableFloat': case 'VariableFloat':
                    this.setPort(node,'Value', parseFloat(node.properties.Value||'0')||0); break;
                  case 'variableString': case 'VariableString':
                    this.setPort(node,'Value', node.properties.Value||''); break;
                  case 'variableBool': case 'VariableBool':
                    this.setPort(node,'Value',
                      (node.properties.Value||'').toLowerCase()==='true'); break;

                  case 'mathAdd': case 'MathAdd':
                    this.setPort(node,'Result', this.rF(node,'A')+this.rF(node,'B')); break;
                  case 'mathSubtract': case 'MathSubtract':
                    this.setPort(node,'Result', this.rF(node,'A')-this.rF(node,'B')); break;
                  case 'mathMultiply': case 'MathMultiply':
                    this.setPort(node,'Result', this.rF(node,'A')*this.rF(node,'B')); break;
                  case 'mathDivide': case 'MathDivide':
                    {const b=this.rF(node,'B'); this.setPort(node,'Result',b===0?0:this.rF(node,'A')/b);} break;
                  case 'randomInt': case 'RandomInt':
                    {const mn=this.rI(node,'Min'),mx=this.rI(node,'Max');
                     this.setPort(node,'Value', mn+Math.floor(Math.random()*(Math.max(mx,mn+1)-mn)));} break;
                  case 'compare': case 'Compare':
                    {const a=this.rA(node,'A'),b=this.rA(node,'B'),op=node.properties.Operator||'==';
                     let r=false;const fa=parseFloat(a),fb=parseFloat(b);
                     if(!isNaN(fa)&&!isNaN(fb)){
                       if(op==='==')r=fa===fb; else if(op==='!=')r=fa!==fb;
                       else if(op==='>')r=fa>fb; else if(op==='<')r=fa<fb;
                       else if(op==='>=')r=fa>=fb; else if(op==='<=')r=fa<=fb;
                     } else r=(op==='==')?String(a)===String(b):String(a)!==String(b);
                     this.setPort(node,'Result',r);} break;
                  case 'logicAnd': case 'LogicAnd':
                    this.setPort(node,'Result',this.rB(node,'A')&&this.rB(node,'B')); break;
                  case 'logicOr': case 'LogicOr':
                    this.setPort(node,'Result',this.rB(node,'A')||this.rB(node,'B')); break;
                  case 'logicNot': case 'LogicNot':
                    this.setPort(node,'Result',!this.rB(node,'A')); break;

                  case 'branch': case 'Branch':
                    this.execChain(node, this.rB(node,'Condition')?'True':'False'); break;
                  case 'forLoop': case 'ForLoop':
                    {const cnt=Math.min(this.rI(node,'Count'),10000);
                     for(let i=0;i<cnt;i++){this.setPort(node,'Index',i);this.execChain(node,'Loop Body');}
                     this.execChain(node,'Completed');} break;

                  case 'createMap': case 'CreateMap':
                    {const w=Math.max(1,this.rI(node,'Width')||parseInt(node.properties.Width||'80'));
                     const h=Math.max(1,this.rI(node,'Height')||parseInt(node.properties.Height||'24'));
                     const m=this.createMap(w,h);
                     this.activeMap=m; this.setPort(node,'Map',m);
                     this.execChain(node,'Exec');} break;
                  case 'generateCaveCellular': case 'GenerateCaveCellular':
                    {const m=this.rM(node,'Map')||this.activeMap;
                     if(m){this.genCave(m,parseFloat(node.properties.FillRatio||'0.45'),
                       parseInt(node.properties.Iterations||'5'));
                       this.activeMap=m; this.setPort(node,'Map',m);}
                     this.execChain(node,'Exec');} break;
                  case 'generateRoomsBSP': case 'GenerateRoomsBSP':
                    {const m=this.rM(node,'Map')||this.activeMap;
                     if(m){this.genBSP(m,parseInt(node.properties.MinRoomSize||'4'),
                       parseInt(node.properties.MaxRoomSize||'12'));
                       this.activeMap=m; this.setPort(node,'Map',m);}
                     this.execChain(node,'Exec');} break;
                  case 'generateDrunkardWalk': case 'GenerateDrunkardWalk':
                    {const m=this.rM(node,'Map')||this.activeMap;
                     if(m){this.genDrunk(m,parseInt(node.properties.Steps||'500'));
                       this.activeMap=m; this.setPort(node,'Map',m);}
                     this.execChain(node,'Exec');} break;
                  case 'renderMap': case 'RenderMap':
                    {const m=this.rM(node,'Map')||this.activeMap;
                     if(m) this.renderMapToScreen(m); this.execChain(node,'Exec');} break;
                  case 'drawChar': case 'DrawChar':
                    {const ch=node.properties.Char||'?';
                     const fg=hexColor(node.properties.FgColor);
                     const bg=hexColor(node.properties.BgColor);
                     drawCharAt(this.rI(node,'X'),this.rI(node,'Y'),ch[0]||' ',fg,bg);
                     this.execChain(node,'Exec');} break;
                  case 'printText': case 'PrintText':
                    {printTextAt(this.rI(node,'X'),this.rI(node,'Y'),this.rS(node,'Text'));
                     this.execChain(node,'Exec');} break;
                  case 'clearDisplay': case 'ClearDisplay':
                    clearScreen(); this.execChain(node,'Exec'); break;
                  case 'fillRegion': case 'FillRegion':
                    {const m=this.rM(node,'Map')||this.activeMap;
                     const x=this.rI(node,'X'),y=this.rI(node,'Y');
                     const w=this.rI(node,'Width'),h=this.rI(node,'Height');
                     const ch=node.properties.Char||'#';
                     const fg=hexColor(node.properties.FgColor);
                     const bg=hexColor(node.properties.BgColor);
                     if(m) for(let cx=x;cx<x+w&&cx<m.width;cx++)
                              for(let cy=y;cy<y+h&&cy<m.height;cy++)
                                {m.chars[cx][cy]=ch[0];m.fg[cx][cy]=fg;m.bg[cx][cy]=bg;}
                     this.execChain(node,'Exec');} break;
                  case 'getCell': case 'GetCell':
                    {const m=this.rM(node,'Map')||this.activeMap;
                     const x=this.rI(node,'X'),y=this.rI(node,'Y');
                     if(m&&x>=0&&x<m.width&&y>=0&&y<m.height)
                       this.setPort(node,'Cell',{ch:m.chars[x][y],fg:m.fg[x][y],bg:m.bg[x][y]});
                    } break;
                  case 'setCell': case 'SetCell':
                    {const m=this.rM(node,'Map')||this.activeMap;
                     const cell=this.rA(node,'Cell');
                     const x=this.rI(node,'X'),y=this.rI(node,'Y');
                     if(m&&cell&&x>=0&&x<m.width&&y>=0&&y<m.height)
                       {m.chars[x][y]=cell.ch;m.fg[x][y]=cell.fg;m.bg[x][y]=cell.bg;}
                     this.execChain(node,'Exec');} break;
                  case 'spawnEntity': case 'SpawnEntity':
                    {const ent={id:crypto.randomUUID(),
                       name:node.properties.Name||'Entity',
                       glyph:(node.properties.Glyph||'@')[0],
                       fg:hexColor(node.properties.FgColor),
                       x:this.rI(node,'X'), y:this.rI(node,'Y')};
                     this.entities.push(ent);
                     this.setPort(node,'Entity',ent);
                     this.execChain(node,'Exec');} break;
                  case 'moveEntity': case 'MoveEntity':
                    {const e=this.rA(node,'Entity');
                     if(e){e.x+=this.rI(node,'DX');e.y+=this.rI(node,'DY');}
                     this.execChain(node,'Exec');} break;
                  case 'destroyEntity': case 'DestroyEntity':
                    {const e=this.rA(node,'Entity');
                     if(e) this.entities=this.entities.filter(x=>x!==e);
                     this.execChain(node,'Exec');} break;
                  case 'showMenu': case 'ShowMenu':
                    {// Render a simple overlay menu.
                     const title=node.properties.Title||'Menu';
                     const items=(node.properties.Items||'').split('\n');
                     let my=2;
                     printTextAt(2,my++,'╔══ '+title+' ══╗','#ffff00','#000080');
                     items.forEach((it,i)=>printTextAt(2,my++,` ${i+1}. ${it} `,'#ffffff','#000080'));
                     printTextAt(2,my,'╚'+('═'.repeat(title.length+6))+'╝','#ffff00','#000080');
                     renderScreen();
                     // Return selection via stored key or 0.
                     this.setPort(node,'SelectedIndex',this.lastMenu||0);
                     this.execChain(node,'Exec');} break;
                  default:
                    this.execChain(node,'Exec'); break;
                }
              }

              // ── Map helpers ──────────────────────────────────────────────────
              createMap(w,h) {
                return {
                  width:w, height:h,
                  chars: Array.from({length:w},()=>Array(h).fill(' ')),
                  fg:    Array.from({length:w},()=>Array(h).fill('#aaaaaa')),
                  bg:    Array.from({length:w},()=>Array(h).fill('#000000')),
                };
              }

              genCave(m, fillRatio=0.45, iters=5) {
                const {width:W,height:H}=m;
                let g=Array.from({length:W},(_,x)=>Array.from({length:H},(_,y)=>
                  x===0||y===0||x===W-1||y===H-1||Math.random()<fillRatio));
                for(let i=0;i<iters;i++){
                  const n=Array.from({length:W},()=>Array(H).fill(false));
                  for(let x=0;x<W;x++) for(let y=0;y<H;y++){
                    if(x===0||y===0||x===W-1||y===H-1){n[x][y]=true;continue;}
                    let cnt=0;
                    for(let nx=x-1;nx<=x+1;nx++) for(let ny=y-1;ny<=y+1;ny++){
                      if(nx===x&&ny===y)continue;
                      if(nx<0||ny<0||nx>=W||ny>=H||g[nx][ny])cnt++;
                    }
                    n[x][y]=cnt>=5;
                  }
                  g=n;
                }
                for(let x=0;x<W;x++) for(let y=0;y<H;y++){
                  if(g[x][y]){m.chars[x][y]='#';m.fg[x][y]='#888888';}
                  else{m.chars[x][y]='.';m.fg[x][y]='#aaaaaa';}
                }
              }

              genBSP(m, minRoom=4, maxRoom=12) {
                const {width:W,height:H}=m;
                for(let x=0;x<W;x++) for(let y=0;y<H;y++){m.chars[x][y]='#';m.fg[x][y]='#888888';}
                const rooms=[];
                const partition=(x,y,w,h)=>{
                  if(w<12&&h<12){
                    const rw=Math.max(3,minRoom+Math.floor(Math.random()*(Math.min(maxRoom,w-2)-minRoom+1)));
                    const rh=Math.max(3,minRoom+Math.floor(Math.random()*(Math.min(maxRoom,h-2)-minRoom+1)));
                    const rx=x+1+Math.floor(Math.random()*Math.max(1,w-rw-1));
                    const ry=y+1+Math.floor(Math.random()*Math.max(1,h-rh-1));
                    for(let cx=rx;cx<rx+rw&&cx<W;cx++) for(let cy=ry;cy<ry+rh&&cy<H;cy++)
                      {m.chars[cx][cy]='.';m.fg[cx][cy]='#aaaaaa';}
                    rooms.push({cx:Math.floor(rx+rw/2),cy:Math.floor(ry+rh/2)});
                    return;
                  }
                  if(h>w){const s=y+6+Math.floor(Math.random()*(h-12));partition(x,y,w,s-y);partition(x,s,w,h-(s-y));}
                  else{const s=x+6+Math.floor(Math.random()*(w-12));partition(x,y,s-x,h);partition(s,y,w-(s-x),h);}
                };
                partition(0,0,W,H);
                for(let i=1;i<rooms.length;i++){
                  let {cx:x1,cy:y1}=rooms[i-1],{cx:x2,cy:y2}=rooms[i];
                  for(let x=Math.min(x1,x2);x<=Math.max(x1,x2);x++)
                    if(x>=0&&x<W&&y1>=0&&y1<H){m.chars[x][y1]='.';m.fg[x][y1]='#886644';}
                  for(let y=Math.min(y1,y2);y<=Math.max(y1,y2);y++)
                    if(x2>=0&&x2<W&&y>=0&&y<H){m.chars[x2][y]='.';m.fg[x2][y]='#886644';}
                }
              }

              genDrunk(m, steps=500) {
                const {width:W,height:H}=m;
                for(let x=0;x<W;x++) for(let y=0;y<H;y++){m.chars[x][y]='#';m.fg[x][y]='#888888';}
                let x=Math.floor(W/2),y=Math.floor(H/2);
                const dx=[0,0,-1,1],dy=[-1,1,0,0];
                for(let s=0;s<steps;s++){
                  m.chars[x][y]='.';m.fg[x][y]='#aaaaaa';
                  const d=Math.floor(Math.random()*4);
                  const nx=x+dx[d],ny=y+dy[d];
                  if(nx>0&&ny>0&&nx<W-1&&ny<H-1){x=nx;y=ny;}
                }
              }

              renderMapToScreen(m) {
                const maxX=Math.min(m.width,COLS),maxY=Math.min(m.height,ROWS);
                for(let x=0;x<maxX;x++) for(let y=0;y<maxY;y++)
                  drawCharAt(x,y,m.chars[x][y],m.fg[x][y],m.bg[x][y]);
                // Draw entities on top.
                for(const e of this.entities)
                  if(e.x>=0&&e.x<COLS&&e.y>=0&&e.y<ROWS)
                    drawCharAt(e.x,e.y,e.glyph,e.fg,'#000000');
              }

              // ── Port resolution ──────────────────────────────────────────────
              setPort(node, name, val) {
                const p=findPort(node,name,false); if(!p) return; this.vals[p.id]=val;
              }
              rA(node, name) {
                const port=findPort(node,name,true); if(!port) return null;
                const conn=findConnection(this.graph,node.id,port.id,false);
                if(conn) {
                  if(this.vals[conn.sourcePortId]!==undefined) return this.vals[conn.sourcePortId];
                  const src=findNode(this.graph,conn.sourceNodeId);
                  if(src&&!this.inProg.has(src.id)){
                    this.inProg.add(src.id);try{this.execNode(src);}finally{this.inProg.delete(src.id);}
                    if(this.vals[conn.sourcePortId]!==undefined) return this.vals[conn.sourcePortId];
                  }
                }
                if(node.properties[name]!=null) return node.properties[name];
                return port.defaultValue||null;
              }
              rI(node,name){const v=this.rA(node,name);return typeof v==='number'?Math.round(v):parseInt(String(v||'0'),10)||0;}
              rF(node,name){const v=this.rA(node,name);return typeof v==='number'?v:parseFloat(String(v||'0'))||0;}
              rB(node,name){const v=this.rA(node,name);if(typeof v==='boolean')return v;if(typeof v==='number')return v!==0;return String(v||'').toLowerCase()==='true';}
              rS(node,name){return String(this.rA(node,name)||'');}
              rM(node,name){const v=this.rA(node,name);return(v&&typeof v==='object'&&'chars'in v)?v:null;}
            }

            //──────────────────────────────────────────────────────────────
            // Game loop
            //──────────────────────────────────────────────────────────────
            const graph = PROJECT.graphs.find(g=>g.id===PROJECT.startGraphId)||PROJECT.graphs[0];
            const executor = graph ? new Executor(graph) : null;
            let tickCount = 0;
            let started = false;

            function startGame() {
              if (!executor) { printTextAt(2,2,'No graph found.'); renderScreen(); return; }
              clearScreen();
              executor.run();
              renderScreen();
              started = true;
              document.getElementById('status-bar').textContent =
                '{{System.Security.SecurityElement.Escape(_project.Name)}} — Use keyboard to play';
            }

            document.addEventListener('DOMContentLoaded', () => {
              canvas.focus();
              startGame();
            });

            window.addEventListener('keydown', (e) => {
              if (!executor) return;
              if (!started) { startGame(); return; }
              executor.fireKey(e.key);
              executor.tick(++tickCount);
              renderScreen();
              e.preventDefault();
            });

            // Auto-start after a short delay.
            setTimeout(() => { if (!started) startGame(); }, 200);
              </script>
            </body>
            </html>
            """;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string SanitiseName(string name) =>
        new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');

    private void Report(string message, int percent) =>
        _progress?.Invoke(message, percent);
}

/// <summary>
/// Thrown when a <see cref="GameExporter"/> operation fails.
/// </summary>
public sealed class ExportException : Exception
{
    /// <inheritdoc/>
    public ExportException(string message) : base(message) { }
    /// <inheritdoc/>
    public ExportException(string message, Exception inner) : base(message, inner) { }
}
