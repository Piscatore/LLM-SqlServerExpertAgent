using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Raid.Memory.Configuration;
using Raid.Memory.Data;
using Raid.Memory.Interfaces;
using Raid.Memory.Models;
using Raid.Memory.Services;
using Xunit;

namespace Raid.Memory.Tests.Services;

public class SqlKnowledgeBaseTests : IDisposable
{
    private readonly Mock<ILogger<SqlKnowledgeBase>> _loggerMock;
    private readonly Mock<IVectorSearchEngine> _vectorSearchMock;
    private readonly MemoryConfiguration _config;
    private readonly MemoryDbContext _dbContext;
    private readonly SqlKnowledgeBase _sqlKnowledgeBase;

    public SqlKnowledgeBaseTests()
    {
        _loggerMock = new Mock<ILogger<SqlKnowledgeBase>>();
        _vectorSearchMock = new Mock<IVectorSearchEngine>();

        _config = new MemoryConfiguration
        {
            Management = new MemoryManagementConfiguration
            {
                MinKnowledgeConfidence = 0.3f
            }
        };

        var options = new DbContextOptionsBuilder<MemoryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MemoryDbContext(options);
        _sqlKnowledgeBase = new SqlKnowledgeBase(_loggerMock.Object, _config, _dbContext, _vectorSearchMock.Object);

        // Setup vector search mock
        _vectorSearchMock
            .Setup(vs => vs.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]); // Mock embedding
    }

    [Fact]
    public async Task StoreKnowledgeAsync_ShouldStoreNewKnowledge()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();

        // Act
        var result = await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge);

        // Assert
        Assert.Equal(knowledge.Id, result);
        var storedKnowledge = await _dbContext.Knowledge.FirstOrDefaultAsync(k => k.Id == knowledge.Id);
        Assert.NotNull(storedKnowledge);
        Assert.Equal(knowledge.Concept, storedKnowledge.Concept);

        _vectorSearchMock.Verify(
            vs => vs.IndexKnowledgeAsync(knowledge, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreKnowledgeAsync_ShouldUpdateExistingKnowledge()
    {
        // Arrange
        var originalKnowledge = CreateTestKnowledge();
        await _sqlKnowledgeBase.StoreKnowledgeAsync(originalKnowledge);

        // Create duplicate with higher confidence
        var duplicateKnowledge = CreateTestKnowledge();
        duplicateKnowledge.Id = Guid.NewGuid().ToString(); // Different ID
        duplicateKnowledge.Domain = originalKnowledge.Domain;
        duplicateKnowledge.Concept = originalKnowledge.Concept;
        duplicateKnowledge.Rule = originalKnowledge.Rule;
        duplicateKnowledge.Confidence = 0.9f; // Higher confidence
        duplicateKnowledge.Tags = new List<string> { "new-tag" };

        // Act
        var result = await _sqlKnowledgeBase.StoreKnowledgeAsync(duplicateKnowledge);

        // Assert
        Assert.Equal(originalKnowledge.Id, result); // Should return original ID
        var updatedKnowledge = await _dbContext.Knowledge.FirstOrDefaultAsync(k => k.Id == originalKnowledge.Id);
        Assert.NotNull(updatedKnowledge);
        Assert.Equal(0.9f, updatedKnowledge.Confidence); // Should have higher confidence
        Assert.True(updatedKnowledge.Tags.Contains("new-tag")); // Should merge tags
        Assert.True(updatedKnowledge.UsageCount > 0); // Should increment usage
    }

    [Fact]
    public async Task GetKnowledgeAsync_ShouldReturnKnowledgeAndUpdateUsage()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();
        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge);

        // Act
        var result = await _sqlKnowledgeBase.GetKnowledgeAsync(knowledge.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(knowledge.Id, result.Id);
        Assert.True(result.UsageCount > 0);
        Assert.True(result.LastUsedAt.HasValue);
    }

    [Fact]
    public async Task GetKnowledgeAsync_ShouldReturnNullForNonExistent()
    {
        // Act
        var result = await _sqlKnowledgeBase.GetKnowledgeAsync("non-existent-id");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task QuerySimilarKnowledgeAsync_ShouldReturnSimilarKnowledge()
    {
        // Arrange
        var knowledge1 = CreateTestKnowledge("database", "SQL optimization");
        var knowledge2 = CreateTestKnowledge("data", "NoSQL queries");
        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge1);
        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge2);

        var mockMatches = new List<SimilarityMatch>
        {
            new() { Knowledge = knowledge1, Score = 0.85f },
            new() { Knowledge = knowledge2, Score = 0.72f }
        };

        _vectorSearchMock
            .Setup(vs => vs.FindSimilarAsync("database", 10, 0.7f, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockMatches);

        // Act
        var results = await _sqlKnowledgeBase.QuerySimilarKnowledgeAsync("database", 0.7f, 10);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.True(results.All(r => r.Score >= 0.7f));

        // Verify usage was updated
        var updatedKnowledge1 = await _dbContext.Knowledge.FirstOrDefaultAsync(k => k.Id == knowledge1.Id);
        var updatedKnowledge2 = await _dbContext.Knowledge.FirstOrDefaultAsync(k => k.Id == knowledge2.Id);
        Assert.True(updatedKnowledge1!.UsageCount > 0);
        Assert.True(updatedKnowledge2!.UsageCount > 0);
    }

    [Fact]
    public async Task GetKnowledgeByDomainAsync_ShouldReturnDomainSpecificKnowledge()
    {
        // Arrange
        var domain1Knowledge = CreateTestKnowledge();
        domain1Knowledge.Domain = "domain1";
        var domain2Knowledge = CreateTestKnowledge();
        domain2Knowledge.Domain = "domain2";

        await _sqlKnowledgeBase.StoreKnowledgeAsync(domain1Knowledge);
        await _sqlKnowledgeBase.StoreKnowledgeAsync(domain2Knowledge);

        // Act
        var results = await _sqlKnowledgeBase.GetKnowledgeByDomainAsync("domain1");

        // Assert
        Assert.Single(results);
        Assert.Equal("domain1", results[0].Domain);
    }

    [Fact]
    public async Task GetKnowledgeByTagsAsync_ShouldReturnTaggedKnowledge()
    {
        // Arrange
        var knowledge1 = CreateTestKnowledge();
        knowledge1.Tags = new List<string> { "tag1", "tag2" };
        var knowledge2 = CreateTestKnowledge();
        knowledge2.Tags = new List<string> { "tag2", "tag3" };
        var knowledge3 = CreateTestKnowledge();
        knowledge3.Tags = new List<string> { "tag4" };

        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge1);
        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge2);
        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge3);

        // Act
        var results = await _sqlKnowledgeBase.GetKnowledgeByTagsAsync(new List<string> { "tag2" });

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, k => Assert.Contains("tag2", k.Tags));
    }

    [Fact]
    public async Task UpdateKnowledgeAsync_ShouldUpdateKnowledge()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();
        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge);

        knowledge.Concept = "updated concept";
        knowledge.Confidence = 0.95f;

        // Act
        await _sqlKnowledgeBase.UpdateKnowledgeAsync(knowledge);

        // Assert
        var updatedKnowledge = await _dbContext.Knowledge.FirstOrDefaultAsync(k => k.Id == knowledge.Id);
        Assert.NotNull(updatedKnowledge);
        Assert.Equal("updated concept", updatedKnowledge.Concept);
        Assert.Equal(0.95f, updatedKnowledge.Confidence);

        _vectorSearchMock.Verify(
            vs => vs.UpdateKnowledgeIndexAsync(knowledge, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteKnowledgeAsync_ShouldRemoveKnowledge()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();
        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge);

        // Act
        await _sqlKnowledgeBase.DeleteKnowledgeAsync(knowledge.Id);

        // Assert
        var deletedKnowledge = await _dbContext.Knowledge.FirstOrDefaultAsync(k => k.Id == knowledge.Id);
        Assert.Null(deletedKnowledge);

        _vectorSearchMock.Verify(
            vs => vs.RemoveFromIndexAsync(knowledge.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LinkKnowledgeAsync_ShouldCreateRelationship()
    {
        // Arrange
        var sourceKnowledge = CreateTestKnowledge();
        var targetKnowledge = CreateTestKnowledge();
        await _sqlKnowledgeBase.StoreKnowledgeAsync(sourceKnowledge);
        await _sqlKnowledgeBase.StoreKnowledgeAsync(targetKnowledge);

        // Act
        await _sqlKnowledgeBase.LinkKnowledgeAsync(sourceKnowledge.Id, targetKnowledge.Id, "related-to");

        // Assert
        var updatedSource = await _dbContext.Knowledge.FirstOrDefaultAsync(k => k.Id == sourceKnowledge.Id);
        Assert.NotNull(updatedSource);
        Assert.Contains(targetKnowledge.Id, updatedSource.RelatedKnowledgeIds);
        Assert.Equal("related-to", updatedSource.Metadata[$"relationship_to_{targetKnowledge.Id}"]);
    }

    [Fact]
    public async Task GetRelatedKnowledgeAsync_ShouldReturnRelatedKnowledge()
    {
        // Arrange
        var sourceKnowledge = CreateTestKnowledge();
        var relatedKnowledge = CreateTestKnowledge();
        await _sqlKnowledgeBase.StoreKnowledgeAsync(sourceKnowledge);
        await _sqlKnowledgeBase.StoreKnowledgeAsync(relatedKnowledge);
        await _sqlKnowledgeBase.LinkKnowledgeAsync(sourceKnowledge.Id, relatedKnowledge.Id, "related-to");

        // Act
        var results = await _sqlKnowledgeBase.GetRelatedKnowledgeAsync(sourceKnowledge.Id);

        // Assert
        Assert.Single(results);
        Assert.Equal(relatedKnowledge.Id, results[0].Id);
    }

    [Fact]
    public async Task SearchKnowledgeAsync_ShouldFindMatchingKnowledge()
    {
        // Arrange
        var knowledge1 = CreateTestKnowledge("database optimization", "SQL performance tuning");
        knowledge1.Tags = new List<string> { "database", "performance" };
        var knowledge2 = CreateTestKnowledge("web development", "React components");
        knowledge2.Tags = new List<string> { "web", "frontend" };

        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge1);
        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge2);

        // Act - Search by concept
        var conceptResults = await _sqlKnowledgeBase.SearchKnowledgeAsync("database");

        // Act - Search by rule
        var ruleResults = await _sqlKnowledgeBase.SearchKnowledgeAsync("performance");

        // Act - Search by tag
        var tagResults = await _sqlKnowledgeBase.SearchKnowledgeAsync("frontend");

        // Assert
        Assert.Single(conceptResults);
        Assert.Equal(knowledge1.Id, conceptResults[0].Id);

        Assert.Single(ruleResults);
        Assert.Equal(knowledge1.Id, ruleResults[0].Id);

        Assert.Single(tagResults);
        Assert.Equal(knowledge2.Id, tagResults[0].Id);
    }

    [Fact]
    public async Task GetKnowledgeStatsAsync_ShouldReturnValidStatistics()
    {
        // Arrange
        var knowledge1 = CreateTestKnowledge();
        knowledge1.Domain = "domain1";
        knowledge1.UsageCount = 5;
        var knowledge2 = CreateTestKnowledge();
        knowledge2.Domain = "domain2";
        knowledge2.UsageCount = 3;
        var knowledge3 = CreateTestKnowledge();
        knowledge3.Domain = "domain1";
        knowledge3.UsageCount = 8;

        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge1);
        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge2);
        await _sqlKnowledgeBase.StoreKnowledgeAsync(knowledge3);

        // Act
        var stats = await _sqlKnowledgeBase.GetKnowledgeStatsAsync();

        // Assert
        Assert.Equal(3, (int)stats["total_knowledge"]);

        var domainCounts = (Dictionary<string, int>)stats["domains"];
        Assert.Equal(2, domainCounts["domain1"]);
        Assert.Equal(1, domainCounts["domain2"]);

        var avgConfidence = (double)stats["average_confidence"];
        Assert.InRange(avgConfidence, 0.5, 1.0);

        var mostUsed = (List<object>)stats["most_used_knowledge"];
        Assert.Equal(3, mostUsed.Count); // Should return top 5, but we only have 3
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
            RelatedKnowledgeIds = new List<string>(),
            Metadata = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UsageCount = 0
        };
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}