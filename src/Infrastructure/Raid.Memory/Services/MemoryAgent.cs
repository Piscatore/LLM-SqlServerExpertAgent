using Microsoft.Extensions.Logging;
using Raid.Memory.Configuration;
using Raid.Memory.Interfaces;
using Raid.Memory.Models;

namespace Raid.Memory.Services;

/// <summary>
/// Main Memory Agent implementation that coordinates all memory services
/// </summary>
public class MemoryAgent : IMemoryAgent
{
    private readonly ILogger<MemoryAgent> _logger;
    private readonly MemoryConfiguration _config;
    private readonly IContextManager _contextManager;
    private readonly IKnowledgeBase _knowledgeBase;
    private readonly IVectorSearchEngine _vectorSearchEngine;

    public MemoryAgent(
        ILogger<MemoryAgent> logger,
        MemoryConfiguration config,
        IContextManager contextManager,
        IKnowledgeBase knowledgeBase,
        IVectorSearchEngine vectorSearchEngine)
    {
        _logger = logger;
        _config = config;
        _contextManager = contextManager;
        _knowledgeBase = knowledgeBase;
        _vectorSearchEngine = vectorSearchEngine;
    }

    #region Context Management

    public async Task StoreContextAsync(string agentId, string sessionId, AgentContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _contextManager.StoreSessionContextAsync(agentId, sessionId, context, cancellationToken);
            _logger.LogInformation("Stored context for agent {AgentId}, session {SessionId}", agentId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store context for agent {AgentId}, session {SessionId}", agentId, sessionId);
            throw;
        }
    }

    public async Task<AgentContext?> GetContextAsync(string agentId, string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await _contextManager.GetSessionContextAsync(agentId, sessionId, cancellationToken);
            if (context != null)
            {
                _logger.LogDebug("Retrieved context for agent {AgentId}, session {SessionId}", agentId, sessionId);
            }
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve context for agent {AgentId}, session {SessionId}", agentId, sessionId);
            throw;
        }
    }

    public async Task<List<AgentContext>> GetRecentContextAsync(string agentId, TimeSpan window, CancellationToken cancellationToken = default)
    {
        try
        {
            var contexts = await _contextManager.GetRecentContextAsync(agentId, window, cancellationToken);
            _logger.LogDebug("Retrieved {Count} recent context items for agent {AgentId}", contexts.Count, agentId);
            return contexts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recent context for agent {AgentId}", agentId);
            throw;
        }
    }

    #endregion

    #region Knowledge Management

    public async Task<string> StoreKnowledgeAsync(Knowledge knowledge, CancellationToken cancellationToken = default)
    {
        try
        {
            if (knowledge.Confidence < _config.Management.MinKnowledgeConfidence)
            {
                _logger.LogWarning("Skipping knowledge storage due to low confidence: {Confidence}", knowledge.Confidence);
                return knowledge.Id;
            }

            var id = await _knowledgeBase.StoreKnowledgeAsync(knowledge, cancellationToken);
            _logger.LogInformation("Stored knowledge {Id} in domain {Domain}", id, knowledge.Domain);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store knowledge in domain {Domain}", knowledge.Domain);
            throw;
        }
    }

    public async Task<List<Knowledge>> QueryKnowledgeAsync(string query, string? domain = null, float threshold = 0.7f, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the configured threshold if none provided
            var actualThreshold = threshold > 0 ? threshold : _config.Management.DefaultSimilarityThreshold;
            var actualMaxResults = Math.Min(maxResults, _config.Management.MaxSimilarKnowledgeResults);

            var matches = await _knowledgeBase.QuerySimilarKnowledgeAsync(query, actualThreshold, actualMaxResults, cancellationToken);

            var knowledge = matches.Select(m => m.Knowledge).ToList();

            // Filter by domain if specified
            if (!string.IsNullOrEmpty(domain))
            {
                knowledge = knowledge.Where(k => k.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            _logger.LogDebug("Queried knowledge with query '{Query}' and found {Count} results", query, knowledge.Count);
            return knowledge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query knowledge with query: {Query}", query);
            throw;
        }
    }

    public async Task<List<Knowledge>> GetKnowledgeByDomainAsync(string domain, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var knowledge = await _knowledgeBase.GetKnowledgeByDomainAsync(domain, maxResults, cancellationToken);
            _logger.LogDebug("Retrieved {Count} knowledge items for domain {Domain}", knowledge.Count, domain);
            return knowledge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get knowledge for domain: {Domain}", domain);
            throw;
        }
    }

    #endregion

    #region Cross-Agent Coordination

    public async Task ShareContextAsync(string fromAgentId, string toAgentId, string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await _contextManager.GetSessionContextAsync(fromAgentId, sessionId, cancellationToken);
            if (context != null)
            {
                // Create a copy for the target agent with shared context marker
                var sharedContext = new AgentContext
                {
                    AgentId = toAgentId,
                    SessionId = $"shared_{sessionId}",
                    Topic = context.Topic,
                    Entities = context.Entities,
                    Decisions = context.Decisions,
                    Outcome = context.Outcome,
                    Timestamp = DateTimeOffset.UtcNow,
                    Confidence = context.Confidence * 0.9f, // Reduce confidence slightly for shared context
                    Tags = context.Tags.Concat(new[] { "shared", $"from_{fromAgentId}" }).ToList(),
                    Metadata = new Dictionary<string, object>(context.Metadata)
                    {
                        ["shared_from"] = fromAgentId,
                        ["shared_at"] = DateTimeOffset.UtcNow,
                        ["original_session"] = sessionId
                    }
                };

                await _contextManager.StoreSessionContextAsync(toAgentId, sharedContext.SessionId, sharedContext, cancellationToken);
                _logger.LogInformation("Shared context from {FromAgent} to {ToAgent} for session {SessionId}", fromAgentId, toAgentId, sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to share context from {FromAgent} to {ToAgent}", fromAgentId, toAgentId);
            throw;
        }
    }

    public async Task<List<Knowledge>> GetSharedKnowledgeAsync(List<string> agentIds, string? topic = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var allKnowledge = new List<Knowledge>();

            foreach (var agentId in agentIds)
            {
                // Get knowledge that would be relevant to this agent
                var agentKnowledge = await _knowledgeBase.SearchKnowledgeAsync(agentId, cancellationToken: cancellationToken);
                allKnowledge.AddRange(agentKnowledge);
            }

            // Filter by topic if specified
            if (!string.IsNullOrEmpty(topic))
            {
                allKnowledge = allKnowledge.Where(k =>
                    k.Concept.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
                    k.Tags.Any(tag => tag.Contains(topic, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            // Remove duplicates and sort by relevance
            var uniqueKnowledge = allKnowledge
                .GroupBy(k => k.Id)
                .Select(g => g.OrderByDescending(k => k.Confidence).First())
                .OrderByDescending(k => k.Confidence)
                .ThenByDescending(k => k.UsageCount)
                .ToList();

            _logger.LogDebug("Retrieved {Count} shared knowledge items for {AgentCount} agents", uniqueKnowledge.Count, agentIds.Count);
            return uniqueKnowledge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get shared knowledge for agents: {Agents}", string.Join(", ", agentIds));
            throw;
        }
    }

    #endregion

    #region Learning and Adaptation

    public async Task LearnFromInteractionAsync(string agentId, AgentContext context, string outcome, float confidence, CancellationToken cancellationToken = default)
    {
        try
        {
            // Create knowledge from the successful interaction
            var knowledge = new Knowledge
            {
                Domain = agentId,
                Concept = context.Topic,
                Rule = $"When dealing with {string.Join(", ", context.Entities)}, apply decisions: {string.Join("; ", context.Decisions)} to achieve: {outcome}",
                Confidence = confidence,
                Source = agentId,
                Tags = context.Tags.Concat(new[] { "learned", "interaction" }).ToList(),
                Metadata = new Dictionary<string, object>
                {
                    ["learned_from_session"] = context.SessionId,
                    ["interaction_timestamp"] = context.Timestamp,
                    ["agent_id"] = agentId,
                    ["entities"] = context.Entities,
                    ["decisions"] = context.Decisions
                }
            };

            await StoreKnowledgeAsync(knowledge, cancellationToken);

            // Update the context with the outcome
            context.Outcome = outcome;
            context.Confidence = confidence;
            context.Tags = context.Tags.Concat(new[] { "learned" }).ToList();

            await _contextManager.UpdateSessionContextAsync(agentId, context.SessionId, context, cancellationToken);

            _logger.LogInformation("Learned from interaction for agent {AgentId} with confidence {Confidence}", agentId, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to learn from interaction for agent {AgentId}", agentId);
            throw;
        }
    }

    public async Task<List<Knowledge>> GetRecommendationsAsync(string agentId, AgentContext currentContext, int maxResults = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var recommendations = new List<Knowledge>();

            // Query based on current topic
            if (!string.IsNullOrEmpty(currentContext.Topic))
            {
                var topicKnowledge = await QueryKnowledgeAsync(currentContext.Topic, agentId, cancellationToken: cancellationToken);
                recommendations.AddRange(topicKnowledge.Take(maxResults / 2));
            }

            // Query based on entities
            foreach (var entity in currentContext.Entities.Take(3))
            {
                var entityKnowledge = await QueryKnowledgeAsync(entity, agentId, cancellationToken: cancellationToken);
                recommendations.AddRange(entityKnowledge.Take(2));
            }

            // Remove duplicates and limit results
            var uniqueRecommendations = recommendations
                .GroupBy(k => k.Id)
                .Select(g => g.First())
                .OrderByDescending(k => k.Confidence)
                .ThenByDescending(k => k.UsageCount)
                .Take(maxResults)
                .ToList();

            _logger.LogDebug("Generated {Count} recommendations for agent {AgentId}", uniqueRecommendations.Count, agentId);
            return uniqueRecommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommendations for agent {AgentId}", agentId);
            throw;
        }
    }

    #endregion

    #region Memory Management

    public Task SummarizeOldContextAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        try
        {
            // This is a complex operation that would need agent-specific logic
            // For now, we'll implement a basic cleanup
            _logger.LogInformation("Starting context summarization for items older than {TimeSpan}", olderThan);

            // TODO: Implement context summarization using AI
            // This would involve:
            // 1. Finding old context items
            // 2. Summarizing them using an LLM
            // 3. Replacing detailed context with summaries

            _logger.LogInformation("Context summarization completed");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize old context");
            throw;
        }
    }

    public async Task CleanupMemoryAsync(float minConfidence = 0.3f, TimeSpan olderThan = default, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting memory cleanup with min confidence {MinConfidence}", minConfidence);

            var stats = await _knowledgeBase.GetKnowledgeStatsAsync(cancellationToken);
            var beforeCount = (int)stats["total_knowledge"];

            // TODO: Implement knowledge cleanup
            // This would involve:
            // 1. Finding low-confidence knowledge
            // 2. Finding unused knowledge older than threshold
            // 3. Removing or archiving them

            var afterStats = await _knowledgeBase.GetKnowledgeStatsAsync(cancellationToken);
            var afterCount = (int)afterStats["total_knowledge"];

            _logger.LogInformation("Memory cleanup completed. Removed {Count} knowledge items", beforeCount - afterCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup memory");
            throw;
        }
    }

    #endregion

    #region Statistics and Health

    public async Task<Dictionary<string, object>> GetMemoryStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var knowledgeStats = await _knowledgeBase.GetKnowledgeStatsAsync(cancellationToken);
            var vectorStats = await _vectorSearchEngine.GetIndexStatsAsync(cancellationToken);

            var stats = new Dictionary<string, object>
            {
                ["knowledge"] = knowledgeStats,
                ["vector_index"] = vectorStats,
                ["configuration"] = new
                {
                    _config.Management.SessionContextTtl,
                    _config.Management.MaxContextItemsPerSession,
                    _config.Management.MinKnowledgeConfidence,
                    _config.Management.DefaultSimilarityThreshold
                },
                ["timestamp"] = DateTimeOffset.UtcNow
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory statistics");
            throw;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if all components are working
            var stats = await GetMemoryStatsAsync(cancellationToken);

            // Basic health checks
            var isHealthy = stats.ContainsKey("knowledge") &&
                           stats.ContainsKey("vector_index");

            _logger.LogDebug("Memory agent health check: {IsHealthy}", isHealthy);
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return false;
        }
    }

    #endregion
}