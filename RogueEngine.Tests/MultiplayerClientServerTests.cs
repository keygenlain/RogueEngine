using RogueEngine.Core.Engine;
using RogueEngine.Core.Models;

namespace RogueEngine.Tests;

/// <summary>
/// Tests for client-server multiplayer additions:
/// <see cref="NetworkRole"/>, <see cref="NetworkMessage.TargetPlayerId"/>,
/// <see cref="NetworkSession.Role"/>, <see cref="NetworkSessionManager.SendToClientAsync"/>,
/// and the corresponding visual-script nodes.
/// </summary>
public sealed class MultiplayerClientServerTests
{
    // ── NetworkRole enum ───────────────────────────────────────────────────────

    [Fact]
    public void NetworkRole_HasExpectedValues()
    {
        Assert.Equal(3, Enum.GetValues<NetworkRole>().Length);
        Assert.Contains(NetworkRole.Peer,               Enum.GetValues<NetworkRole>());
        Assert.Contains(NetworkRole.DedicatedServer,    Enum.GetValues<NetworkRole>());
        Assert.Contains(NetworkRole.AuthoritativeClient, Enum.GetValues<NetworkRole>());
    }

    // ── NetworkSession.Role ───────────────────────────────────────────────────

    [Fact]
    public void NetworkSession_DefaultRole_IsPeer()
    {
        var session = new NetworkSession();
        Assert.Equal(NetworkRole.Peer, session.Role);
    }

    [Fact]
    public void NetworkSession_Role_CanBeSetToDedicatedServer()
    {
        var session = new NetworkSession { Role = NetworkRole.DedicatedServer };
        Assert.Equal(NetworkRole.DedicatedServer, session.Role);
    }

    [Fact]
    public void NetworkSession_Role_CanBeSetToAuthoritativeClient()
    {
        var session = new NetworkSession { Role = NetworkRole.AuthoritativeClient };
        Assert.Equal(NetworkRole.AuthoritativeClient, session.Role);
    }

    // ── NetworkMessage.TargetPlayerId ─────────────────────────────────────────

    [Fact]
    public void NetworkMessage_TargetPlayerId_DefaultsToNull()
    {
        var msg = new NetworkMessage();
        Assert.Null(msg.TargetPlayerId);
    }

    [Fact]
    public void NetworkMessage_TargetPlayerId_CanBeSet()
    {
        var id  = Guid.NewGuid();
        var msg = new NetworkMessage { TargetPlayerId = id };
        Assert.Equal(id, msg.TargetPlayerId);
    }

    // ── NetworkSessionManager.HostAsServerAsync ────────────────────────────────

    [Fact]
    public async Task HostAsServerAsync_SetsRoleToDedicatedServer()
    {
        using var mgr = new NetworkSessionManager();
        // Use a random high port to avoid conflicts in CI.
        var port = 19500 + (Environment.TickCount & 0x3FF);
        await mgr.HostAsServerAsync("TestServer", port, 8);

        Assert.Equal(NetworkRole.DedicatedServer, mgr.Session.Role);
        Assert.Equal(SessionState.Hosting,        mgr.Session.State);
    }

    [Fact]
    public async Task HostAsServerAsync_HasNoLocalPlayer()
    {
        using var mgr = new NetworkSessionManager();
        var port = 19600 + (Environment.TickCount & 0x3FF);
        await mgr.HostAsServerAsync(port: port);

        Assert.Null(mgr.Session.LocalPlayer);
    }

    [Fact]
    public async Task HostAsServerAsync_SessionName_IsSet()
    {
        using var mgr = new NetworkSessionManager();
        var port = 19700 + (Environment.TickCount & 0x3FF);
        await mgr.HostAsServerAsync("My Dedicated Server", port);

        Assert.Equal("My Dedicated Server", mgr.Session.Name);
    }

    [Fact]
    public async Task HostAsServerAsync_MaxPlayers_IsClamped()
    {
        using var mgr = new NetworkSessionManager();
        var port = 19800 + (Environment.TickCount & 0x3FF);
        await mgr.HostAsServerAsync(maxPlayers: 100);   // above 64 cap

        Assert.Equal(64, mgr.Session.MaxPlayers);
    }

    // ── JoinAsync role parameter ──────────────────────────────────────────────

    [Fact]
    public void NetworkSessionManager_JoinAsync_SetsAuthoritativeClientRole()
    {
        // We don't actually connect (no host running), just verify the Session
        // role is assigned before the TCP connect attempt fails.
        using var mgr = new NetworkSessionManager();
        // Fire-and-forget; ignore connection error.
        var task = mgr.JoinAsync("127.0.0.1", "P", 9, NetworkRole.AuthoritativeClient);
        task.ContinueWith(_ => { }); // suppress unobserved exception

        // The role is set synchronously before the await.
        Assert.Equal(NetworkRole.AuthoritativeClient, mgr.Session.Role);
    }

    // ── SendToClientAsync – directed send by TargetPlayerId ───────────────────

    [Fact]
    public async Task SendToClientAsync_SetsTargetPlayerIdOnMessage()
    {
        using var mgr = new NetworkSessionManager();
        var receivedMessages = new List<NetworkMessage>();
        mgr.MessageReceived += m => receivedMessages.Add(m);

        var playerId = Guid.NewGuid();
        // No actual TCP socket is mapped; the method silently no-ops but still
        // copies the TargetPlayerId onto the message object.
        var msg = new NetworkMessage { MessageType = "test", Payload = "hello" };
        await mgr.SendToClientAsync(playerId, msg);

        Assert.Equal(playerId, msg.TargetPlayerId);
    }

    // ── Visual-script node types ──────────────────────────────────────────────

    [Fact]
    public void NodeFactory_HostServer_HasDefinition()
    {
        var def = NodeFactory.AllDefinitions.Single(d => d.Type == NodeType.HostServer);
        Assert.Equal("Multiplayer", def.Category);
        Assert.Contains(def.OutputPorts, p => p.Name == "Session");
    }

    [Fact]
    public void NodeFactory_ConnectToServer_HasDefinition()
    {
        var def = NodeFactory.AllDefinitions.Single(d => d.Type == NodeType.ConnectToServer);
        Assert.Equal("Multiplayer", def.Category);
        Assert.Contains(def.DefaultProperties, kv => kv.Key == "Host");
    }

    [Fact]
    public void NodeFactory_SendToClient_HasDefinition()
    {
        var def = NodeFactory.AllDefinitions.Single(d => d.Type == NodeType.SendToClient);
        Assert.Equal("Multiplayer", def.Category);
        Assert.Contains(def.InputPorts, p => p.Name == "PlayerId");
    }

    [Fact]
    public void NodeFactory_OnClientConnected_HasDefinition()
    {
        var def = NodeFactory.AllDefinitions.Single(d => d.Type == NodeType.OnClientConnected);
        Assert.Equal("Multiplayer", def.Category);
        Assert.Contains(def.OutputPorts, p => p.Name == "PlayerName");
        Assert.Contains(def.OutputPorts, p => p.Name == "PlayerId");
    }

    [Fact]
    public void NodeFactory_OnClientDisconnected_HasDefinition()
    {
        var def = NodeFactory.AllDefinitions.Single(d => d.Type == NodeType.OnClientDisconnected);
        Assert.Equal("Multiplayer", def.Category);
    }

    [Fact]
    public void NodeFactory_GetNetworkRole_HasDefinition()
    {
        var def = NodeFactory.AllDefinitions.Single(d => d.Type == NodeType.GetNetworkRole);
        Assert.Equal("Multiplayer", def.Category);
        Assert.Contains(def.OutputPorts, p => p.Name == "Role");
    }

    // ── ScriptExecutor: HostServer node ───────────────────────────────────────

    [Fact]
    public void Script_HostServer_SetsSessionOnOutput()
    {
        var start  = NodeFactory.Create(NodeType.Start);
        var hsSrv  = NodeFactory.Create(NodeType.HostServer);
        hsSrv.Properties["Port"] = "19900";
        hsSrv.Properties["SessionName"] = "ScriptServer";

        var graph = new ScriptGraph();
        graph.AddNode(start);
        graph.AddNode(hsSrv);
        graph.Connect(start.Id, start.Outputs.First(p => p.Name == "Exec").Id,
                      hsSrv.Id, hsSrv.Inputs.First(p => p.Name == "Exec").Id);

        var executor = new ScriptExecutor(graph);
        var result   = executor.Run();

        Assert.Contains(result.Log, l => l.Contains("ScriptServer"));
        Assert.NotNull(executor.Network);
        Assert.Equal(NetworkRole.DedicatedServer, executor.Network!.Session.Role);
    }

    // ── ScriptExecutor: ConnectToServer node ──────────────────────────────────

    [Fact]
    public void Script_ConnectToServer_SetsAuthoritativeClientRole()
    {
        var start  = NodeFactory.Create(NodeType.Start);
        var conn   = NodeFactory.Create(NodeType.ConnectToServer);
        conn.Properties["Host"]       = "127.0.0.1";
        conn.Properties["Port"]       = "9";    // unreachable – fire-and-forget
        conn.Properties["PlayerName"] = "TestClient";

        var graph = new ScriptGraph();
        graph.AddNode(start);
        graph.AddNode(conn);
        graph.Connect(start.Id, start.Outputs.First(p => p.Name == "Exec").Id,
                      conn.Id,  conn.Inputs.First(p => p.Name == "Exec").Id);

        var executor = new ScriptExecutor(graph);
        executor.Run();

        Assert.NotNull(executor.Network);
        Assert.Equal(NetworkRole.AuthoritativeClient, executor.Network!.Session.Role);
    }

    // ── ScriptExecutor: GetNetworkRole node ───────────────────────────────────

    [Fact]
    public void Script_GetNetworkRole_OutputsPeerByDefault()
    {
        var node  = NodeFactory.Create(NodeType.GetNetworkRole);
        var graph = new ScriptGraph();
        graph.AddNode(node);

        var executor = new ScriptExecutor(graph);
        executor.EvaluateNode(node);

        var role = node.Outputs.First(p => p.Name == "Role").RuntimeValue as string;
        Assert.Equal(nameof(NetworkRole.Peer), role);
    }

    // ── ScriptExecutor: SendToClient node ─────────────────────────────────────

    [Fact]
    public void Script_SendToClient_LogsWithPlayerId()
    {
        var start = NodeFactory.Create(NodeType.Start);
        var send  = NodeFactory.Create(NodeType.SendToClient);
        var id    = Guid.NewGuid().ToString();
        send.Properties["MessageType"] = "state";
        send.Properties["PlayerId"]    = id;

        var graph = new ScriptGraph();
        graph.AddNode(start);
        graph.AddNode(send);
        graph.Connect(start.Id, start.Outputs.First(p => p.Name == "Exec").Id,
                      send.Id,  send.Inputs.First(p => p.Name == "Exec").Id);

        var result = new ScriptExecutor(graph).Run();
        Assert.Contains(result.Log, l => l.Contains(id));
    }
}
