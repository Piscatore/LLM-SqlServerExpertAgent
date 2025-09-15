using Microsoft.Extensions.Logging;
using Moq;
using Raid.Memory.Configuration;
using Raid.Memory.Interfaces;
using Raid.Memory.Models;
using Raid.Memory.Services;
using Xunit;

namespace Raid.Memory.Tests.Services;

public class MemoryAgentTests
{
    private readonly Mock<ILogger<MemoryAgent>> _loggerMock;
    private readonly Mock<IContextManager> _contextManagerMock;
    private readonly Mock<IKnowledgeBase> _knowledgeBaseMock;
    private readonly Mock<IVectorSearchEngine> _vectorSearchMock;
    private readonly MemoryConfiguration _config;
    private readonly MemoryAgent _memoryAgent;

    public MemoryAgentTests()
    {
        _loggerMock = new Mock<ILogger<MemoryAgent>>();
        _contextManagerMock = new Mock<IContextManager>();
        _knowledgeBaseMock = new Mock<IKnowledgeBase>();
        _vectorSearchMock = new Mock<IVectorSearchEngine>();

        _config = new MemoryConfiguration
        {
            Management = new MemoryManagementConfiguration
            {
                MinKnowledgeConfidence = 0.3f,
                DefaultSimilarityThreshold = 0.7f,
                MaxSimilarKnowledgeResults = 10
            }
        };

        _memoryAgent = new MemoryAgent(
            _loggerMock.Object,
            _config,
            _contextManagerMock.Object,
            _knowledgeBaseMock.Object,
            _vectorSearchMock.Object);
    }

    [Fact]
    public async Task StoreContextAsync_ShouldCallContextManager()
    {
        // Arrange
        var agentId = "test-agent";
        var sessionId = "test-session";
        var context = CreateTestContext(agentId, sessionId);

        // Act
        await _memoryAgent.StoreContextAsync(agentId, sessionId, context);

        // Assert
        _contextManagerMock.Verify(
            cm => cm.StoreSessionContextAsync(agentId, sessionId, context, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetContextAsync_ShouldReturnContextFromManager()
    {
        // Arrange
        var agentId = "test-agent";
        var sessionId = "test-session";
        var expectedContext = CreateTestContext(agentId, sessionId);

        _contextManagerMock
            .Setup(cm => cm.GetSessionContextAsync(agentId, sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedContext);

        // Act
        var result = await _memoryAgent.GetContextAsync(agentId, sessionId);

        // Assert
        Assert.Equal(expectedContext, result);
        _contextManagerMock.Verify(
            cm => cm.GetSessionContextAsync(agentId, sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreKnowledgeAsync_ShouldSkipLowConfidenceKnowledge()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();
        knowledge.Confidence = 0.2f; // Below minimum threshold

        // Act
        var result = await _memoryAgent.StoreKnowledgeAsync(knowledge);

        // Assert
        Assert.Equal(knowledge.Id, result);
        _knowledgeBaseMock.Verify(
            kb => kb.StoreKnowledgeAsync(It.IsAny<Knowledge>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StoreKnowledgeAsync_ShouldStoreHighConfidenceKnowledge()
    {
        // Arrange
        var knowledge = CreateTestKnowledge();
        knowledge.Confidence = 0.8f; // Above minimum threshold
        var expectedId = "stored-knowledge-id";

        _knowledgeBaseMock
            .Setup(kb => kb.StoreKnowledgeAsync(knowledge, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);

        // Act
        var result = await _memoryAgent.StoreKnowledgeAsync(knowledge);

        // Assert
        Assert.Equal(expectedId, result);
        _knowledgeBaseMock.Verify(
            kb => kb.StoreKnowledgeAsync(knowledge, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryKnowledgeAsync_ShouldApplyConfiguredLimits()
    {
        // Arrange
        var query = "test query";
        var domain = "test-domain";
        var threshold = 0.5f;
        var maxResults = 50;

        var mockKnowledge = new List<Knowledge> { CreateTestKnowledge() };
        var mockMatches = mockKnowledge.Select(k => new SimilarityMatch
        {
            Knowledge = k,
            Score = 0.8f
        }).ToList();

        _knowledgeBaseMock
            .Setup(kb => kb.QuerySimilarKnowledgeAsync(query, It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockMatches);

        // Act
        var result = await _memoryAgent.QueryKnowledgeAsync(query, domain, threshold, maxResults);

        // Assert
        Assert.Single(result);
        _knowledgeBaseMock.Verify(
            kb => kb.QuerySimilarKnowledgeAsync(
                query,
                It.Is<float>(t => Math.Abs(t - threshold) < 0.001f),
                It.Is<int>(mr => mr <= _config.Management.MaxSimilarKnowledgeResults),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ShareContextAsync_ShouldCreateSharedContextWithReducedConfidence()
    {
        // Arrange
        var fromAgentId = "source-agent";
        var toAgentId = "target-agent";
        var sessionId = "test-session";
        var originalContext = CreateTestContext(fromAgentId, sessionId);
        originalContext.Confidence = 0.9f;

        _contextManagerMock
            .Setup(cm => cm.GetSessionContextAsync(fromAgentId, sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalContext);

        // Act
        await _memoryAgent.ShareContextAsync(fromAgentId, toAgentId, sessionId);

        // Assert
        _contextManagerMock.Verify(
            cm => cm.StoreSessionContextAsync(
                toAgentId,
                $"shared_{sessionId}",
                It.Is<AgentContext>(ctx =>
                    ctx.AgentId == toAgentId &&
                    ctx.Confidence == 0.81f && // 0.9 * 0.9
                    ctx.Tags.Contains("shared") &&
                    ctx.Tags.Contains($"from_{fromAgentId}")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LearnFromInteractionAsync_ShouldCreateKnowledgeFromContext()
    {
        // Arrange
        var agentId = "test-agent";
        var context = CreateTestContext(agentId, "session-1");
        var outcome = "Successfully completed task";
        var confidence = 0.85f;
        var expectedKnowledgeId = "learned-knowledge-id";

        _knowledgeBaseMock
            .Setup(kb => kb.StoreKnowledgeAsync(It.IsAny<Knowledge>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedKnowledgeId);

        _contextManagerMock
            .Setup(cm => cm.UpdateSessionContextAsync(agentId, context.SessionId, It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _memoryAgent.LearnFromInteractionAsync(agentId, context, outcome, confidence);

        // Assert
        _knowledgeBaseMock.Verify(
            kb => kb.StoreKnowledgeAsync(
                It.Is<Knowledge>(k =>
                    k.Domain == agentId &&
                    k.Concept == context.Topic &&
                    k.Confidence == confidence &&
                    k.Tags.Contains("learned") &&
                    k.Tags.Contains("interaction")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _contextManagerMock.Verify(
            cm => cm.UpdateSessionContextAsync(
                agentId,
                context.SessionId,
                It.Is<AgentContext>(ctx =>
                    ctx.Outcome == outcome &&
                    ctx.Confidence == confidence &&
                    ctx.Tags.Contains("learned")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRecommendationsAsync_ShouldReturnRelevantKnowledge()
    {
        // Arrange
        var agentId = "test-agent";
        var context = CreateTestContext(agentId, "session-1");
        var maxResults = 5;

        var mockKnowledge = new List<Knowledge>
        {
            CreateTestKnowledge(),
            CreateTestKnowledge()
        };

        _knowledgeBaseMock
            .Setup(kb => kb.QuerySimilarKnowledgeAsync(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockKnowledge.Select(k => new SimilarityMatch { Knowledge = k, Score = 0.8f }).ToList());

        // Act
        var result = await _memoryAgent.GetRecommendationsAsync(agentId, context, maxResults);

        // Assert
        Assert.True(result.Count <= maxResults);
        _knowledgeBaseMock.Verify(
            kb => kb.QuerySimilarKnowledgeAsync(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task IsHealthyAsync_ShouldReturnTrueWhenStatsAvailable()
    {
        // Arrange
        var mockStats = new Dictionary<string, object>
        {
            ["knowledge"] = new Dictionary<string, object>(),
            ["vector_index"] = new Dictionary<string, object>()
        };

        _knowledgeBaseMock
            .Setup(kb => kb.GetKnowledgeStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object>());

        _vectorSearchMock
            .Setup(vs => vs.GetIndexStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object>());

        // Act
        var result = await _memoryAgent.IsHealthyAsync();

        // Assert
        Assert.True(result);
    }

    private static AgentContext CreateTestContext(string agentId, string sessionId)
    {
        return new AgentContext
        {
            AgentId = agentId,
            SessionId = sessionId,
            Topic = "Test Topic",
            Entities = new List<string> { "entity1", "entity2" },
            Decisions = new List<string> { "decision1", "decision2" },
            Timestamp = DateTimeOffset.UtcNow,
            Confidence = 0.7f,
            Tags = new List<string> { "test", "context" },
            Metadata = new Dictionary<string, object>()
        };
    }

    private static Knowledge CreateTestKnowledge()
    {
        return new Knowledge
        {
            Id = Guid.NewGuid().ToString(),
            Domain = "test-domain",
            Concept = "test-concept",
            Rule = "test rule for knowledge",
            Confidence = 0.8f,
            Source = "test-source",
            Tags = new List<string> { "test", "knowledge" },
            Metadata = new Dictionary<string, object>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UsageCount = 0
        };
    }
}