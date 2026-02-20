using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using RogueEngine.Core.Models;

namespace RogueEngine.Core.Engine;

/// <summary>
/// Manages multiplayer network sessions for RogueEngine games.
///
/// <para>
/// <b>Architecture</b>: this manager implements a simple star topology.
/// One player hosts (<see cref="HostAsync"/>) and listens on a TCP port;
/// all other players connect as clients (<see cref="JoinAsync"/>).
/// All messages go through the host, which forwards them to the intended
/// recipients.
/// </para>
///
/// <para>
/// Messages are newline-delimited JSON objects (each a
/// <see cref="NetworkMessage"/> serialised with
/// <c>System.Text.Json</c>).
/// </para>
///
/// <para>
/// For HTML5 export the JavaScript runtime implements the same wire format
/// over <c>WebSocket</c>.  A lightweight WebSocket relay server would be
/// needed to bridge TCP hosts and browser clients; that bridge is outside the
/// engine scope but the message contract is identical.
/// </para>
/// </summary>
public sealed class NetworkSessionManager : IDisposable
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private TcpListener? _listener;
    private TcpClient? _clientSocket;
    private readonly List<TcpClient> _clientConnections = [];
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>The current network session metadata.</summary>
    public NetworkSession Session { get; } = new();

    /// <summary>
    /// Raised on the thread-pool whenever a <see cref="NetworkMessage"/> is
    /// received.  Subscribe before calling <see cref="HostAsync"/> or
    /// <see cref="JoinAsync"/>.
    /// </summary>
    public event Action<NetworkMessage>? MessageReceived;

    /// <summary>
    /// Raised when a new player connects to the hosted session.
    /// Only raised on the host side.
    /// </summary>
    public event Action<NetworkPlayer>? PlayerJoined;

    /// <summary>
    /// Raised when a player disconnects.
    /// </summary>
    public event Action<NetworkPlayer>? PlayerLeft;

    // ── Host ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts listening for incoming connections.  Call from the host player's
    /// machine.
    /// </summary>
    /// <param name="sessionName">Room name displayed in the lobby.</param>
    /// <param name="playerName">The host player's display name.</param>
    /// <param name="port">TCP port to listen on (default 7777).</param>
    /// <param name="maxPlayers">Maximum players including host (1–16).</param>
    public async Task HostAsync(
        string sessionName = "My Game",
        string playerName = "Host",
        int port = 7777,
        int maxPlayers = 4)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Session.Name = sessionName;
        Session.MaxPlayers = Math.Clamp(maxPlayers, 1, 16);
        Session.State = SessionState.Hosting;

        var host = new NetworkPlayer { Name = playerName, IsHost = true };
        Session.LocalPlayer = host;
        Session.Players.Add(host);

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        // Accept incoming connections in the background.
        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    if (Session.Players.Count >= Session.MaxPlayers)
                    {
                        client.Dispose();
                        continue;
                    }
                    lock (_clientConnections) _clientConnections.Add(client);
                    _ = Task.Run(() => ReadLoopAsync(client, _cts.Token), _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch { /* swallow accept errors */ }
            }
        }, _cts.Token);

        await Task.CompletedTask;
    }

    // ── Join ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to a hosted session.
    /// </summary>
    /// <param name="host">Hostname or IP address of the host.</param>
    /// <param name="playerName">The joining player's display name.</param>
    /// <param name="port">TCP port of the host (default 7777).</param>
    public async Task JoinAsync(
        string host = "127.0.0.1",
        string playerName = "Player",
        int port = 7777)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _clientSocket = new TcpClient();
        await _clientSocket.ConnectAsync(host, port);

        var local = new NetworkPlayer { Name = playerName, IsHost = false };
        Session.LocalPlayer = local;
        Session.Players.Add(local);
        Session.State = SessionState.Connected;

        // Send a join announcement.
        await SendRawAsync(_clientSocket,
            new NetworkMessage
            {
                MessageType = "join",
                SenderId = local.Id,
                Payload = playerName,
            });

        _ = Task.Run(() => ReadLoopAsync(_clientSocket, _cts.Token), _cts.Token);
    }

    // ── Send ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Broadcasts a message to all connected players.
    /// On the host this is a true broadcast; on a client it sends to the host
    /// which re-broadcasts to all.
    /// </summary>
    public async Task BroadcastAsync(NetworkMessage message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        message.SenderId ??= Session.LocalPlayer?.Id;

        if (Session.IsHost)
        {
            List<TcpClient> snapshot;
            lock (_clientConnections) snapshot = [.._clientConnections];
            foreach (var client in snapshot)
                await SendRawAsync(client, message).ConfigureAwait(false);
        }
        else if (_clientSocket is not null)
        {
            await SendRawAsync(_clientSocket, message).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disconnects from the session and cleans up resources.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_disposed) return;
        Session.State = SessionState.Disconnecting;
        await _cts.CancelAsync();
        Dispose();
    }

    // ── Message queue (for visual-script polling) ──────────────────────────────

    /// <summary>
    /// Dequeues up to <paramref name="maxCount"/> inbound messages and returns them.
    /// Useful for polling from a game-tick loop.
    /// </summary>
    public IReadOnlyList<NetworkMessage> DrainInbound(int maxCount = 64)
    {
        var result = new List<NetworkMessage>(maxCount);
        while (result.Count < maxCount && Session.InboundMessages.TryDequeue(out var msg))
            result.Add(msg);
        return result;
    }

    // ── Internal helpers ───────────────────────────────────────────────────────

    private async Task ReadLoopAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(client.GetStream(), Encoding.UTF8,
                leaveOpen: true);
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null) break; // connection closed

                var msg = JsonSerializer.Deserialize<NetworkMessage>(line, _json);
                if (msg is null) continue;

                // Handle control messages.
                if (msg.MessageType == "join")
                {
                    var p = new NetworkPlayer { Id = msg.SenderId ?? Guid.NewGuid(), Name = msg.Payload };
                    lock (Session.Players) Session.Players.Add(p);
                    PlayerJoined?.Invoke(p);

                    // If we're the host, re-broadcast to all other clients.
                    if (Session.IsHost)
                        await BroadcastAsync(msg).ConfigureAwait(false);
                }
                else
                {
                    Session.InboundMessages.Enqueue(msg);
                    MessageReceived?.Invoke(msg);

                    // If we're the host, relay to all clients.
                    if (Session.IsHost)
                        await BroadcastAsync(msg).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch
        {
            // Find and remove disconnected client.
            lock (_clientConnections)
            {
                _clientConnections.Remove(client);
                var disconnected = Session.Players.FirstOrDefault(p =>
                    p.Id == client.Client.RemoteEndPoint?.GetHashCode().ToString().Let(Guid.Parse));
                if (disconnected is not null)
                {
                    Session.Players.Remove(disconnected);
                    PlayerLeft?.Invoke(disconnected);
                }
            }
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task SendRawAsync(TcpClient client, NetworkMessage message)
    {
        try
        {
            var json = JsonSerializer.Serialize(message, _json);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            await client.GetStream().WriteAsync(bytes).ConfigureAwait(false);
        }
        catch { /* best-effort; ignore send errors */ }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        _listener?.Stop();
        _clientSocket?.Dispose();
        lock (_clientConnections)
            foreach (var c in _clientConnections) c.Dispose();
    }
}

// Small helper extension used internally.
file static class ObjectExtensions
{
    internal static TResult Let<T, TResult>(this T value, Func<T, TResult> fn) => fn(value);
}
