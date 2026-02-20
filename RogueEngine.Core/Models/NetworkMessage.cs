namespace RogueEngine.Core.Models;

/// <summary>
/// A single message exchanged between players in a multiplayer session.
/// Messages are serialised to JSON for transport over TCP/WebSocket.
/// </summary>
public sealed class NetworkMessage
{
    /// <summary>Unique identifier for this message.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Message type tag used by the receiving side to route the message
    /// to the correct handler (e.g. "chat", "state-sync", "input").
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// The player id of the sender, or <see langword="null"/> for server messages.
    /// </summary>
    public Guid? SenderId { get; set; }

    /// <summary>UTC timestamp when the message was created.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Arbitrary string payload.  Structured data should be JSON-encoded and
    /// stored here; simple values (chat text, key names) can be stored directly.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override string ToString() =>
        $"[{MessageType}] from {SenderId?.ToString() ?? "server"}: {Payload}";
}
