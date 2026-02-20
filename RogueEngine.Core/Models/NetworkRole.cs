namespace RogueEngine.Core.Models;

/// <summary>
/// Describes the role a <see cref="NetworkSession"/> participant plays in
/// the client-server topology.
/// </summary>
public enum NetworkRole
{
    /// <summary>
    /// Standard peer-to-peer topology (the default).
    /// One peer acts as host/relay; all others are clients.
    /// Game logic may run on any peer.
    /// </summary>
    Peer,

    /// <summary>
    /// Dedicated authoritative server.
    /// Runs game logic without a local player representation.
    /// Clients send input messages; the server sends authoritative state updates.
    /// </summary>
    DedicatedServer,

    /// <summary>
    /// Client connected to a <see cref="DedicatedServer"/>.
    /// Sends input to the server and applies received state updates locally.
    /// </summary>
    AuthoritativeClient,
}
