using Raid.Memory.Models;

namespace Raid.Memory.Interfaces;

/// <summary>
/// Manages vector embeddings and semantic search capabilities
/// </summary>
public interface IVectorSearchEngine
{
    /// <summary>
    /// Generate vector embedding for text
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Index knowledge item for vector search
    /// </summary>
    Task IndexKnowledgeAsync(Knowledge knowledge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find similar knowledge based on vector similarity
    /// </summary>
    Task<List<SimilarityMatch>> FindSimilarAsync(string query, int topK = 10, float threshold = 0.7f, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find similar knowledge using existing embedding vector
    /// </summary>
    Task<List<SimilarityMatch>> FindSimilarByVectorAsync(float[] vector, int topK = 10, float threshold = 0.7f, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update vector index for existing knowledge
    /// </summary>
    Task UpdateKnowledgeIndexAsync(Knowledge knowledge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove knowledge from vector index
    /// </summary>
    Task RemoveFromIndexAsync(string knowledgeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate similarity between two text strings
    /// </summary>
    Task<float> CalculateSimilarityAsync(string text1, string text2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate similarity between two vectors
    /// </summary>
    float CalculateVectorSimilarity(float[] vector1, float[] vector2, string metric = "cosine");

    /// <summary>
    /// Get vector index statistics
    /// </summary>
    Task<Dictionary<string, object>> GetIndexStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuild the entire vector index
    /// </summary>
    Task RebuildIndexAsync(CancellationToken cancellationToken = default);
}