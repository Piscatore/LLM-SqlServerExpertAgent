namespace Raid.Memory.Models;

/// <summary>
/// Represents a piece of knowledge learned by the system
/// </summary>
public class Knowledge
{
    /// <summary>
    /// Unique identifier for this knowledge
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Domain or area of expertise (e.g., "SQL Server", "Security", "Performance")
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Specific concept or topic
    /// </summary>
    public string Concept { get; set; } = string.Empty;

    /// <summary>
    /// The rule, pattern, or knowledge statement
    /// </summary>
    public string Rule { get; set; } = string.Empty;

    /// <summary>
    /// Confidence in this knowledge (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; set; } = 1.0f;

    /// <summary>
    /// Source of this knowledge (agent, user, external system)
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Tags for categorization and search
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// When this knowledge was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this knowledge was last used
    /// </summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Number of times this knowledge has been accessed
    /// </summary>
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// Vector embedding for semantic search (serialized as JSON)
    /// </summary>
    public string? EmbeddingVector { get; set; }

    /// <summary>
    /// Related knowledge IDs
    /// </summary>
    public List<string> RelatedKnowledgeIds { get; set; } = new();

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}