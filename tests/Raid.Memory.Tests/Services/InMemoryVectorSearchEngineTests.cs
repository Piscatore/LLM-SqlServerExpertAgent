using Microsoft.Extensions.Logging;
using Moq;
using Raid.Memory.Configuration;
using Raid.Memory.Models;
using Raid.Memory.Services;
using Xunit;

namespace Raid.Memory.Tests.Services;

public class InMemoryVectorSearchEngineTests
{
    private readonly Mock<ILogger<InMemoryVectorSearchEngine>> _loggerMock;
    private readonly MemoryConfiguration _config;
    private readonly InMemoryVectorSearchEngine _vectorSearchEngine;

    public InMemoryVectorSearchEngineTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryVectorSearchEngine>>();
        _config = new MemoryConfiguration
        {
            VectorDatabase = new VectorDatabaseConfiguration
            {
                VectorDimension = 1536,
                DistanceMetric = "cosine"
            }
        };

        _vectorSearchEngine = new InMemoryVectorSearchEngine(_loggerMock.Object, _config);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ShouldReturnConsistentResults()
    {
        // Arrange
        var text = "test text for embedding";

        // Act
        var embedding1 = await _vectorSearchEngine.GenerateEmbeddingAsync(text);
        var embedding2 = await _vectorSearchEngine.GenerateEmbeddingAsync(text);

        // Assert
        Assert.Equal(embedding1.Length, embedding2.Length);
        Assert.Equal(embedding1, embedding2); // Should be deterministic
        Assert.Equal(_config.VectorDatabase.VectorDimension, embedding1.Length);
    }

    [Fact]
    public async Task IndexKnowledgeAsync_ShouldStoreKnowledgeInIndex()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();

        // Act
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge);

        // Verify by searching
        var results = await _vectorSearchEngine.FindSimilarAsync("test concept", 10, 0.1f);

        // Assert
        Assert.Single(results);
        Assert.Equal(knowledge.Id, results[0].Knowledge.Id);
    }

    [Fact]
    public async Task FindSimilarAsync_ShouldReturnSimilarKnowledge()
    {
        // Arrange
        var knowledge1 = CreateTestKnowledge("database queries", "SQL Server optimization");
        var knowledge2 = CreateTestKnowledge("data retrieval", "MongoDB aggregation");
        var knowledge3 = CreateTestKnowledge("machine learning", "neural networks");

        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge1);
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge2);
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge3);

        // Act - Search for database-related content
        var results = await _vectorSearchEngine.FindSimilarAsync("database optimization", 5, 0.3f);

        // Assert
        Assert.True(results.Count > 0);
        Assert.True(results.All(r => r.Score >= 0.3f));
        Assert.True(results.OrderByDescending(r => r.Score).SequenceEqual(results)); // Should be sorted by similarity
    }

    [Fact]
    public async Task FindSimilarByVectorAsync_ShouldWorkWithDirectVectors()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge);

        var queryVector = await _vectorSearchEngine.GenerateEmbeddingAsync("test concept");

        // Act
        var results = await _vectorSearchEngine.FindSimilarByVectorAsync(queryVector, 5, 0.5f);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Score >= 0.5f);
    }

    [Fact]
    public async Task UpdateKnowledgeIndexAsync_ShouldUpdateExistingKnowledge()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge);

        // Update the knowledge
        knowledge.Concept = "updated concept";
        knowledge.Rule = "updated rule";

        // Act
        await _vectorSearchEngine.UpdateKnowledgeIndexAsync(knowledge);

        // Verify the update
        var results = await _vectorSearchEngine.FindSimilarAsync("updated concept", 5, 0.1f);

        // Assert
        Assert.Single(results);
        Assert.Equal("updated concept", results[0].Knowledge.Concept);
    }

    [Fact]
    public async Task RemoveFromIndexAsync_ShouldRemoveKnowledge()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge);

        // Verify it's indexed
        var beforeResults = await _vectorSearchEngine.FindSimilarAsync("test concept", 5, 0.1f);
        Assert.Single(beforeResults);

        // Act
        await _vectorSearchEngine.RemoveFromIndexAsync(knowledge.Id);

        // Assert
        var afterResults = await _vectorSearchEngine.FindSimilarAsync("test concept", 5, 0.1f);
        Assert.Empty(afterResults);
    }

    [Fact]
    public async Task CalculateSimilarityAsync_ShouldReturnSimilarityScore()
    {
        // Arrange
        var text1 = "database optimization techniques";
        var text2 = "optimizing database performance";
        var text3 = "machine learning algorithms";

        // Act
        var similarity1 = await _vectorSearchEngine.CalculateSimilarityAsync(text1, text2);
        var similarity2 = await _vectorSearchEngine.CalculateSimilarityAsync(text1, text3);

        // Assert
        Assert.InRange(similarity1, 0f, 1f);
        Assert.InRange(similarity2, 0f, 1f);
        Assert.True(similarity1 > similarity2); // Related texts should be more similar
    }

    [Theory]
    [InlineData("cosine")]
    [InlineData("dotproduct")]
    [InlineData("euclidean")]
    public void CalculateVectorSimilarity_ShouldWorkWithDifferentMetrics(string metric)
    {
        // Arrange
        var vector1 = new float[] { 1f, 0f, 0f, 1f };
        var vector2 = new float[] { 0.5f, 0.5f, 0f, 0.8f };

        // Act
        var similarity = _vectorSearchEngine.CalculateVectorSimilarity(vector1, vector2, metric);

        // Assert
        Assert.InRange(similarity, 0f, 1f);
    }

    [Fact]
    public async Task GetIndexStatsAsync_ShouldReturnValidStats()
    {
        // Arrange
        var knowledge1 = CreateTestKnowledge();
        var knowledge2 = CreateTestKnowledge();
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge1);
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge2);

        // Act
        var stats = await _vectorSearchEngine.GetIndexStatsAsync();

        // Assert
        Assert.Contains("total_vectors", stats.Keys);
        Assert.Contains("vector_dimension", stats.Keys);
        Assert.Equal(2, (int)stats["total_vectors"]);
        Assert.Equal(_config.VectorDatabase.VectorDimension, (int)stats["vector_dimension"]);
    }

    [Fact]
    public async Task RebuildIndexAsync_ShouldClearAndRebuildIndex()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge);

        // Verify it's indexed
        var beforeStats = await _vectorSearchEngine.GetIndexStatsAsync();
        Assert.Equal(1, (int)beforeStats["total_vectors"]);

        // Act
        await _vectorSearchEngine.RebuildIndexAsync();

        // Assert
        var afterStats = await _vectorSearchEngine.GetIndexStatsAsync();
        Assert.Equal(0, (int)afterStats["total_vectors"]);
    }

    [Fact]
    public async Task FindSimilarAsync_WithHighThreshold_ShouldReturnFewerResults()
    {
        // Arrange
        var knowledge1 = CreateTestKnowledge("database", "SQL optimization");
        var knowledge2 = CreateTestKnowledge("data", "NoSQL queries");
        var knowledge3 = CreateTestKnowledge("completely different topic", "unrelated content");

        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge1);
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge2);
        await _vectorSearchEngine.IndexKnowledgeAsync(knowledge3);

        // Act
        var lowThresholdResults = await _vectorSearchEngine.FindSimilarAsync("database queries", 10, 0.1f);
        var highThresholdResults = await _vectorSearchEngine.FindSimilarAsync("database queries", 10, 0.8f);

        // Assert
        Assert.True(lowThresholdResults.Count >= highThresholdResults.Count);
        Assert.All(highThresholdResults, r => Assert.True(r.Score >= 0.8f));
    }

    private static Knowledge CreateTestKnowledge(string concept = "test concept", string rule = "test rule")
    {
        return new Knowledge
        {
            Id = Guid.NewGuid().ToString(),
            Domain = "test-domain",
            Concept = concept,
            Rule = rule,
            Confidence = 0.8f,
            Source = "test-source",
            Tags = new List<string> { "test" },
            Metadata = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UsageCount = 0
        };
    }
}