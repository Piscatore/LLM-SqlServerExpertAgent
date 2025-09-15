namespace Raid.A2A;

/// <summary>
/// Base class for Agent-to-Agent communication messages
/// </summary>
public class A2AMessage
{
    /// <summary>
    /// Unique message identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Source agent identifier
    /// </summary>
    public string FromAgent { get; set; } = string.Empty;

    /// <summary>
    /// Target agent identifier
    /// </summary>
    public string ToAgent { get; set; } = string.Empty;

    /// <summary>
    /// Message type
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Message payload
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Message timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}