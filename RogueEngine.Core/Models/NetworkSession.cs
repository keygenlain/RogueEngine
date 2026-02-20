namespace RogueEngine.Core.Models;

/// <summary>
/// Represents a connected player in a multiplayer session.
/// </summary>
public sealed class NetworkPlayer
{
    /// <summary>Unique identifier assigned when the player connects.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Player display name.</summary>
    public string Name { get; set; } = "Player";

    /// <summary>Whether this player is the session host.</summary>
    public bool IsHost { get; set; }

    /// <summary>Current ping in milliseconds (<see langword="null"/> if not measured yet).</summary>
    public int? PingMs { get; set; }

    /// <inheritdoc/>
    public override string ToString() =>
        $"[{(IsHost ? "Host" : "Client")}] {Name} ({Id})";
}

/// <summary>
/// Describes the current state of a multiplayer network session.
/// </summary>
public enum SessionState
{
    /// <summary>Session has not been started.</summary>
    Idle,
    /// <summary>Host is listening for incoming connections.</summary>
    Hosting,
    /// <summary>Client is connected to a host.</summary>
    Connected,
    /// <summary>Session is shutting down.</summary>
    Disconnecting,
}

/// <summary>
/// Tracks all metadata for a live multiplayer session.
/// The actual transport logic lives in <see cref="Engine.NetworkSessionManager"/>.
/// </summary>
public sealed class NetworkSession
{
    /// <summary>Unique identifier for this session.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Human-readable session name / room name.</summary>
    public string Name { get; set; } = "My Game";

    /// <summary>Maximum number of players allowed (1–16).</summary>
    public int MaxPlayers { get; set; } = 4;

    /// <summary>Current state of the session.</summary>
    public SessionState State { get; set; } = SessionState.Idle;

    /// <summary>The player representing the local client.</summary>
    public NetworkPlayer? LocalPlayer { get; set; }

    /// <summary>All players currently in this session (including the host).</summary>
    public List<NetworkPlayer> Players { get; } = [];

    /// <summary>Queued inbound messages waiting to be processed by the script.</summary>
    public Queue<NetworkMessage> InboundMessages { get; } = new();

    /// <summary>Whether the local player is the session host.</summary>
    public bool IsHost => LocalPlayer?.IsHost ?? false;

    /// <summary>Number of players currently in the session.</summary>
    public int PlayerCount => Players.Count;

    /// <inheritdoc/>
    public override string ToString() =>
        $"Session '{Name}' [{State}] {PlayerCount}/{MaxPlayers}";
}
