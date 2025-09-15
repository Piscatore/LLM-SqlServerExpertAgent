using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Raid.Memory.Configuration;
using Raid.Memory.Data;
using Raid.Memory.Extensions;
using Raid.Memory.Interfaces;
using Raid.Memory.Models;
using Xunit;

namespace Raid.Memory.Tests.Integration;

public class MemoryAgentIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMemoryAgent _memoryAgent;

    public MemoryAgentIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Configure Memory Agent with test settings
        services.AddMemoryAgent(config =>
        {
            config.RedisConnectionString = "localhost:6379";
            config.SqlConnectionString = $"Server=(localdb)\\mssqllocaldb;Database=TestMemory_{Guid.NewGuid():N};Trusted_Connection=true;";
            config.Management = new MemoryManagementConfiguration
            {
                MinKnowledgeConfidence = 0.3f,
                DefaultSimilarityThreshold = 0.5f,
                MaxSimilarKnowledgeResults = 20
            };
        });

        _serviceProvider = services.BuildServiceProvider();

        // Initialize database
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
            dbContext.Database.EnsureCreated();
        }

        _memoryAgent = _serviceProvider.GetRequiredService<IMemoryAgent>();
    }

    [Fact]
    public async Task CompleteWorkflow_ShouldWorkEndToEnd()
    {
        var agentId = "integration-test-agent";
        var sessionId = "integration-session-1";

        // Step 1: Store initial context
        var context = new AgentContext
        {
            AgentId = agentId,
            SessionId = sessionId,
            Topic = "Database Performance Optimization",
            Entities = new List<string> { "SQL Server", "Query Plan", "Index Tuning" },
            Decisions = new List<string> { "Analyze slow queries", "Create covering indexes" },
            Timestamp = DateTimeOffset.UtcNow,
            Confidence = 0.8f,
            Tags = new List<string> { "performance", "database", "sql-server" },
            Metadata = new Dictionary<string, object>
            {
                ["database_type"] = "SQL Server",
                ["session_type"] = "optimization"
            }
        };

        await _memoryAgent.StoreContextAsync(agentId, sessionId, context);

        // Step 2: Store related knowledge
        var knowledge1 = new Knowledge
        {
            Domain = agentId,
            Concept = "Index Optimization",
            Rule = "Create covering indexes for frequently queried columns to improve SELECT performance",
            Confidence = 0.9f,
            Source = "performance-expert",
            Tags = new List<string> { "indexing", "performance", "sql-server" },
            Metadata = new Dictionary<string, object>
            {
                ["impact_level"] = "high",
                ["complexity"] = "medium"
            }
        };

        var knowledge2 = new Knowledge
        {
            Domain = agentId,
            Concept = "Query Plan Analysis",
            Rule = "Use execution plans to identify costly operations and optimize query structure",
            Confidence = 0.85f,
            Source = "performance-expert",
            Tags = new List<string> { "query-analysis", "execution-plans", "sql-server" },
            Metadata = new Dictionary<string, object>
            {
                ["tool"] = "SQL Server Management Studio",
                ["skill_level"] = "intermediate"
            }
        };

        var storedId1 = await _memoryAgent.StoreKnowledgeAsync(knowledge1);
        var storedId2 = await _memoryAgent.StoreKnowledgeAsync(knowledge2);

        Assert.NotNull(storedId1);
        Assert.NotNull(storedId2);

        // Step 3: Query related knowledge
        var similarKnowledge = await _memoryAgent.QueryKnowledgeAsync(
            "database index performance optimization",
            agentId,
            0.5f,
            10);

        Assert.NotEmpty(similarKnowledge);
        Assert.Contains(similarKnowledge, k => k.Concept.Contains("Index"));

        // Step 4: Learn from successful interaction
        var outcome = "Successfully optimized database performance by creating covering indexes, reducing query time by 60%";
        await _memoryAgent.LearnFromInteractionAsync(agentId, context, outcome, 0.95f);

        // Step 5: Get recommendations for similar context
        var newContext = new AgentContext
        {
            AgentId = agentId,
            SessionId = "new-session",
            Topic = "SQL Performance Issues",
            Entities = new List<string> { "Slow Queries", "Database Tuning" },
            Decisions = new List<string>(),
            Timestamp = DateTimeOffset.UtcNow,
            Confidence = 0.7f,
            Tags = new List<string> { "performance", "sql" }
        };

        var recommendations = await _memoryAgent.GetRecommendationsAsync(agentId, newContext, 5);
        Assert.NotEmpty(recommendations);

        // Step 6: Share context with another agent
        var otherAgentId = "secondary-agent";
        await _memoryAgent.ShareContextAsync(agentId, otherAgentId, sessionId);

        var sharedContext = await _memoryAgent.GetContextAsync(otherAgentId, $"shared_{sessionId}");
        Assert.NotNull(sharedContext);
        Assert.Equal(otherAgentId, sharedContext.AgentId);
        Assert.Contains("shared", sharedContext.Tags);
        Assert.Contains($"from_{agentId}", sharedContext.Tags);

        // Step 7: Get knowledge by domain
        var domainKnowledge = await _memoryAgent.GetKnowledgeByDomainAsync(agentId, 10);
        Assert.NotEmpty(domainKnowledge);
        Assert.All(domainKnowledge, k => Assert.Equal(agentId, k.Domain));

        // Step 8: Verify health status
        var isHealthy = await _memoryAgent.IsHealthyAsync();
        Assert.True(isHealthy);

        // Step 9: Get memory statistics
        var stats = await _memoryAgent.GetMemoryStatsAsync();
        Assert.Contains("knowledge", stats.Keys);
        Assert.Contains("vector_index", stats.Keys);
        Assert.Contains("configuration", stats.Keys);
    }

    [Fact]
    public async Task CrossAgentCollaboration_ShouldShareKnowledgeEffectively()
    {
        var agent1Id = "database-expert";
        var agent2Id = "performance-analyst";
        var agent3Id = "security-specialist";

        // Agent 1 stores database knowledge
        var dbKnowledge = new Knowledge
        {
            Domain = agent1Id,
            Concept = "Database Security",
            Rule = "Always use parameterized queries to prevent SQL injection attacks",
            Confidence = 0.9f,
            Source = agent1Id,
            Tags = new List<string> { "security", "sql-injection", "best-practices" }
        };

        await _memoryAgent.StoreKnowledgeAsync(dbKnowledge);

        // Agent 2 stores performance knowledge
        var perfKnowledge = new Knowledge
        {
            Domain = agent2Id,
            Concept = "Query Performance",
            Rule = "Parameterized queries also improve performance through execution plan reuse",
            Confidence = 0.85f,
            Source = agent2Id,
            Tags = new List<string> { "performance", "query-plans", "best-practices" }
        };

        await _memoryAgent.StoreKnowledgeAsync(perfKnowledge);

        // Agent 3 queries shared knowledge
        var agentIds = new List<string> { agent1Id, agent2Id };
        var sharedKnowledge = await _memoryAgent.GetSharedKnowledgeAsync(agentIds, "best-practices");

        Assert.NotEmpty(sharedKnowledge);
        Assert.Contains(sharedKnowledge, k => k.Domain == agent1Id);
        Assert.Contains(sharedKnowledge, k => k.Domain == agent2Id);
        Assert.All(sharedKnowledge, k => Assert.Contains("best-practices", k.Tags));
    }

    [Fact]
    public async Task LearningLoop_ShouldImproveOverTime()
    {
        var agentId = "learning-agent";
        var sessionId = "learning-session";

        // Initial interaction with low confidence
        var initialContext = new AgentContext
        {
            AgentId = agentId,
            SessionId = sessionId,
            Topic = "New Technology Implementation",
            Entities = new List<string> { "React", "TypeScript", "Frontend" },
            Decisions = new List<string> { "Try basic implementation" },
            Confidence = 0.4f,
            Tags = new List<string> { "learning", "frontend" }
        };

        await _memoryAgent.StoreContextAsync(agentId, sessionId, initialContext);

        // Learn from initial experience
        await _memoryAgent.LearnFromInteractionAsync(
            agentId,
            initialContext,
            "Basic React component created successfully",
            0.6f);

        // Second interaction with improved confidence
        var improvedContext = new AgentContext
        {
            AgentId = agentId,
            SessionId = $"{sessionId}-2",
            Topic = "Advanced React Implementation",
            Entities = new List<string> { "React Hooks", "State Management", "TypeScript" },
            Decisions = new List<string> { "Use hooks for state", "Implement TypeScript interfaces" },
            Confidence = 0.7f,
            Tags = new List<string> { "learning", "frontend", "advanced" }
        };

        await _memoryAgent.StoreContextAsync(agentId, $"{sessionId}-2", improvedContext);

        // Learn from advanced experience
        await _memoryAgent.LearnFromInteractionAsync(
            agentId,
            improvedContext,
            "Advanced React application with TypeScript completed successfully",
            0.85f);

        // Verify learning progression
        var recommendations = await _memoryAgent.GetRecommendationsAsync(agentId, new AgentContext
        {
            AgentId = agentId,
            SessionId = "new-react-project",
            Topic = "React Development",
            Entities = new List<string> { "Component Development" },
            Decisions = new List<string>(),
            Confidence = 0.5f,
            Tags = new List<string> { "frontend", "react" }
        }, 10);

        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations, k => k.Tags.Contains("learned"));

        // Check that we have both basic and advanced knowledge
        var domainKnowledge = await _memoryAgent.GetKnowledgeByDomainAsync(agentId, 20);
        var learnedKnowledge = domainKnowledge.Where(k => k.Tags.Contains("learned")).ToList();

        Assert.True(learnedKnowledge.Count >= 2);
        Assert.True(learnedKnowledge.Any(k => k.Confidence >= 0.8f)); // Advanced knowledge should have high confidence
    }

    [Fact]
    public async Task MemoryManagement_ShouldMaintainPerformance()
    {
        var agentId = "performance-test-agent";

        // Store a large number of knowledge items
        var knowledge_items = new List<Knowledge>();
        for (int i = 0; i < 50; i++)
        {
            var knowledge = new Knowledge
            {
                Domain = agentId,
                Concept = $"Concept {i}",
                Rule = $"This is rule number {i} for testing performance",
                Confidence = 0.5f + (i % 5) * 0.1f, // Varying confidence levels
                Source = agentId,
                Tags = new List<string> { "performance-test", $"batch-{i / 10}" }
            };
            knowledge_items.Add(knowledge);
        }

        // Store all knowledge items
        var startTime = DateTimeOffset.UtcNow;
        foreach (var knowledge in knowledge_items)
        {
            await _memoryAgent.StoreKnowledgeAsync(knowledge);
        }
        var storageTime = DateTimeOffset.UtcNow - startTime;

        // Query for similar items
        startTime = DateTimeOffset.UtcNow;
        var queryResults = await _memoryAgent.QueryKnowledgeAsync("Concept performance test", agentId, 0.3f, 20);
        var queryTime = DateTimeOffset.UtcNow - startTime;

        // Verify results
        Assert.NotEmpty(queryResults);
        Assert.True(storageTime.TotalSeconds < 30, $"Storage took too long: {storageTime.TotalSeconds} seconds");
        Assert.True(queryTime.TotalSeconds < 5, $"Query took too long: {queryTime.TotalSeconds} seconds");

        // Test memory statistics
        var stats = await _memoryAgent.GetMemoryStatsAsync();
        Assert.Contains("knowledge", stats.Keys);

        var knowledgeStats = (Dictionary<string, object>)stats["knowledge"];
        var totalKnowledge = (int)knowledgeStats["total_knowledge"];
        Assert.True(totalKnowledge >= 50, $"Expected at least 50 knowledge items, found {totalKnowledge}");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}