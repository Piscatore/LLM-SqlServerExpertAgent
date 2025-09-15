using Microsoft.Extensions.Logging;
using Raid.Memory.Configuration;
using Raid.Memory.Interfaces;
using Raid.Memory.Models;
using System.Numerics;
using System.Text.Json;

namespace Raid.Memory.Services;

/// <summary>
/// In-memory implementation of vector search engine
/// Good for development and small-scale deployments
/// </summary>
public class InMemoryVectorSearchEngine : IVectorSearchEngine
{
    private readonly ILogger<InMemoryVectorSearchEngine> _logger;
    private readonly MemoryConfiguration _config;
    private readonly Dictionary<string, (Knowledge knowledge, float[] vector)> _vectorIndex;
    private readonly object _indexLock = new();

    public InMemoryVectorSearchEngine(
        ILogger<InMemoryVectorSearchEngine> logger,
        MemoryConfiguration config)
    {
        _logger = logger;
        _config = config;
        _vectorIndex = new Dictionary<string, (Knowledge, float[])>();
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or empty", nameof(text));

        try
        {
            // TODO: Integrate with Azure OpenAI for real embeddings
            // For now, create a simple hash-based embedding for development
            var hashEmbedding = CreateHashBasedEmbedding(text);

            _logger.LogDebug("Generated embedding for text length: {Length}", text.Length);
            return Task.FromResult(hashEmbedding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text");
            throw;
        }
    }

    public Task IndexKnowledgeAsync(Knowledge knowledge, CancellationToken cancellationToken = default)
    {
        if (knowledge == null)
            throw new ArgumentNullException(nameof(knowledge));

        return Task.Run(() =>
        {
            lock (_indexLock)
            {
                try
                {
                    float[] vector;

                    if (!string.IsNullOrEmpty(knowledge.EmbeddingVector))
                    {
                        // Use existing embedding
                        vector = JsonSerializer.Deserialize<float[]>(knowledge.EmbeddingVector)
                                ?? throw new InvalidOperationException("Failed to deserialize embedding vector");
                    }
                    else
                    {
                        // Generate new embedding
                        var combinedText = $"{knowledge.Domain} {knowledge.Concept} {knowledge.Rule}";
                        vector = CreateHashBasedEmbedding(combinedText);
                        knowledge.EmbeddingVector = JsonSerializer.Serialize(vector);
                    }

                    _vectorIndex[knowledge.Id] = (knowledge, vector);
                    _logger.LogDebug("Indexed knowledge: {Id} in domain: {Domain}", knowledge.Id, knowledge.Domain);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to index knowledge: {Id}", knowledge.Id);
                    throw;
                }
            }
        }, cancellationToken);
    }

    public async Task<List<SimilarityMatch>> FindSimilarAsync(string query, int topK = 10, float threshold = 0.7f, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SimilarityMatch>();

        try
        {
            var queryVector = await GenerateEmbeddingAsync(query, cancellationToken);
            return await FindSimilarByVectorAsync(queryVector, topK, threshold, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find similar knowledge for query: {Query}", query);
            throw;
        }
    }

    public Task<List<SimilarityMatch>> FindSimilarByVectorAsync(float[] vector, int topK = 10, float threshold = 0.7f, CancellationToken cancellationToken = default)
    {
        if (vector == null)
            throw new ArgumentNullException(nameof(vector));

        return Task.Run(() =>
        {
            lock (_indexLock)
            {
                var matches = new List<SimilarityMatch>();

                foreach (var (id, (knowledge, indexVector)) in _vectorIndex)
                {
                    var similarity = CalculateVectorSimilarity(vector, indexVector, _config.VectorDatabase.DistanceMetric);

                    if (similarity >= threshold)
                    {
                        matches.Add(new SimilarityMatch
                        {
                            Knowledge = knowledge,
                            Score = similarity,
                            DistanceMetric = _config.VectorDatabase.DistanceMetric
                        });
                    }
                }

                // Sort by similarity score (descending) and take top K
                return matches
                    .OrderByDescending(m => m.Score)
                    .Take(topK)
                    .ToList();
            }
        }, cancellationToken);
    }

    public Task UpdateKnowledgeIndexAsync(Knowledge knowledge, CancellationToken cancellationToken = default)
    {
        return IndexKnowledgeAsync(knowledge, cancellationToken);
    }

    public Task RemoveFromIndexAsync(string knowledgeId, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_indexLock)
            {
                if (_vectorIndex.Remove(knowledgeId))
                {
                    _logger.LogDebug("Removed knowledge from index: {Id}", knowledgeId);
                }
                else
                {
                    _logger.LogWarning("Knowledge not found in index: {Id}", knowledgeId);
                }
            }
        }, cancellationToken);
    }

    public async Task<float> CalculateSimilarityAsync(string text1, string text2, CancellationToken cancellationToken = default)
    {
        var vector1 = await GenerateEmbeddingAsync(text1, cancellationToken);
        var vector2 = await GenerateEmbeddingAsync(text2, cancellationToken);

        return CalculateVectorSimilarity(vector1, vector2);
    }

    public float CalculateVectorSimilarity(float[] vector1, float[] vector2, string metric = "cosine")
    {
        if (vector1.Length != vector2.Length)
            throw new ArgumentException("Vectors must have the same dimension");

        return metric.ToLowerInvariant() switch
        {
            "cosine" => CalculateCosineSimilarity(vector1, vector2),
            "dot" => CalculateDotProduct(vector1, vector2),
            "euclidean" => 1.0f / (1.0f + CalculateEuclideanDistance(vector1, vector2)),
            _ => throw new ArgumentException($"Unsupported distance metric: {metric}")
        };
    }

    public Task<Dictionary<string, object>> GetIndexStatsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_indexLock)
            {
                var stats = new Dictionary<string, object>
                {
                    ["total_knowledge_items"] = _vectorIndex.Count,
                    ["vector_dimension"] = _config.VectorDatabase.VectorDimension,
                    ["distance_metric"] = _config.VectorDatabase.DistanceMetric,
                    ["index_type"] = "in_memory"
                };

                if (_vectorIndex.Any())
                {
                    var domains = _vectorIndex.Values.Select(v => v.knowledge.Domain).Distinct().ToList();
                    stats["domains"] = domains;
                    stats["domain_count"] = domains.Count;
                }

                return stats;
            }
        }, cancellationToken);
    }

    public Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            lock (_indexLock)
            {
                _logger.LogInformation("Rebuilding in-memory vector index");

                var knowledgeItems = _vectorIndex.Values.Select(v => v.knowledge).ToList();
                _vectorIndex.Clear();

                foreach (var knowledge in knowledgeItems)
                {
                    // Re-index each knowledge item
                    var combinedText = $"{knowledge.Domain} {knowledge.Concept} {knowledge.Rule}";
                    var vector = CreateHashBasedEmbedding(combinedText);
                    knowledge.EmbeddingVector = JsonSerializer.Serialize(vector);
                    _vectorIndex[knowledge.Id] = (knowledge, vector);
                }

                _logger.LogInformation("Rebuilt vector index with {Count} items", _vectorIndex.Count);
            }
        }, cancellationToken);
    }

    #region Private Helper Methods

    private float[] CreateHashBasedEmbedding(string text)
    {
        // Simple hash-based embedding for development
        // In production, this would be replaced with Azure OpenAI embeddings

        var hash = text.GetHashCode();
        var random = new Random(hash);
        var embedding = new float[_config.VectorDatabase.VectorDimension];

        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range: -1 to 1
        }

        // Normalize the vector
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= magnitude;
            }
        }

        return embedding;
    }

    private static float CalculateCosineSimilarity(float[] vector1, float[] vector2)
    {
        var dot = CalculateDotProduct(vector1, vector2);
        var magnitude1 = (float)Math.Sqrt(vector1.Sum(x => x * x));
        var magnitude2 = (float)Math.Sqrt(vector2.Sum(x => x * x));

        if (magnitude1 == 0 || magnitude2 == 0)
            return 0;

        return dot / (magnitude1 * magnitude2);
    }

    private static float CalculateDotProduct(float[] vector1, float[] vector2)
    {
        return vector1.Zip(vector2, (a, b) => a * b).Sum();
    }

    private static float CalculateEuclideanDistance(float[] vector1, float[] vector2)
    {
        return (float)Math.Sqrt(vector1.Zip(vector2, (a, b) => (a - b) * (a - b)).Sum());
    }

    #endregion
}