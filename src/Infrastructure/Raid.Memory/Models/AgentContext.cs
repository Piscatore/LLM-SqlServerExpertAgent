namespace Raid.Memory.Models;

/// <summary>
/// Represents conversation context for an agent session
/// </summary>
public class AgentContext
{
    /// <summary>
    /// Unique identifier for the agent
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Unique session identifier
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Main topic or theme of the conversation
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Key entities mentioned in the conversation
    /// </summary>
    public List<string> Entities { get; set; } = new();

    /// <summary>
    /// Decisions made during the conversation
    /// </summary>
    public List<string> Decisions { get; set; } = new();

    /// <summary>
    /// Outcome or result of the conversation
    /// </summary>
    public string? Outcome { get; set; }

    /// <summary>
    /// When this context was created
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional metadata for the context
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Confidence score for the context accuracy (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; set; } = 1.0f;

    /// <summary>
    /// Tags for categorization and search
    /// </summary>
    public List<string> Tags { get; set; } = new();
}