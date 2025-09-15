namespace Raid.Memory.Configuration;

/// <summary>
/// Configuration settings for the Memory Agent
/// </summary>
public class MemoryConfiguration
{
    /// <summary>
    /// Redis connection string for session storage
    /// </summary>
    public string RedisConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// SQL Server connection string for persistent storage
    /// </summary>
    public string SqlConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI settings for embeddings
    /// </summary>
    public OpenAIConfiguration OpenAI { get; set; } = new();

    /// <summary>
    /// Vector database configuration
    /// </summary>
    public VectorDatabaseConfiguration VectorDatabase { get; set; } = new();

    /// <summary>
    /// Memory management settings
    /// </summary>
    public MemoryManagementConfiguration Management { get; set; } = new();
}

/// <summary>
/// Azure OpenAI configuration for embeddings
/// </summary>
public class OpenAIConfiguration
{
    /// <summary>
    /// Azure OpenAI endpoint
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// API key for Azure OpenAI
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name for embeddings model
    /// </summary>
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-ada-002";

    /// <summary>
    /// Maximum tokens for embedding requests
    /// </summary>
    public int MaxTokens { get; set; } = 8191;
}

/// <summary>
/// Vector database configuration
/// </summary>
public class VectorDatabaseConfiguration
{
    /// <summary>
    /// Type of vector database (InMemory, Pinecone, Weaviate, Chroma)
    /// </summary>
    public string Provider { get; set; } = "InMemory";

    /// <summary>
    /// Connection string or endpoint for vector database
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Index name for knowledge vectors
    /// </summary>
    public string IndexName { get; set; } = "raid-knowledge";

    /// <summary>
    /// Vector dimension (1536 for text-embedding-ada-002)
    /// </summary>
    public int VectorDimension { get; set; } = 1536;

    /// <summary>
    /// Distance metric for similarity search
    /// </summary>
    public string DistanceMetric { get; set; } = "cosine";
}

/// <summary>
/// Memory management configuration
/// </summary>
public class MemoryManagementConfiguration
{
    /// <summary>
    /// How long to keep session context in Redis (default: 24 hours)
    /// </summary>
    public TimeSpan SessionContextTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Maximum number of context items per agent session
    /// </summary>
    public int MaxContextItemsPerSession { get; set; } = 100;

    /// <summary>
    /// Minimum confidence threshold for storing knowledge
    /// </summary>
    public float MinKnowledgeConfidence { get; set; } = 0.3f;

    /// <summary>
    /// Maximum number of similar knowledge items to return
    /// </summary>
    public int MaxSimilarKnowledgeResults { get; set; } = 10;

    /// <summary>
    /// How often to run memory cleanup (default: 6 hours)
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Default similarity threshold for vector search
    /// </summary>
    public float DefaultSimilarityThreshold { get; set; } = 0.7f;
}