using Microsoft.Extensions.Logging;
using Raid.Memory.Configuration;
using Raid.Memory.Interfaces;
using Raid.Memory.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace Raid.Memory.Services;

/// <summary>
/// Redis-based implementation of context manager for fast session storage
/// </summary>
public class RedisContextManager : IContextManager
{
    private readonly ILogger<RedisContextManager> _logger;
    private readonly MemoryConfiguration _config;
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _redis;

    public RedisContextManager(
        ILogger<RedisContextManager> logger,
        MemoryConfiguration config,
        IConnectionMultiplexer redis)
    {
        _logger = logger;
        _config = config;
        _redis = redis;
        _database = redis.GetDatabase();
    }

    public async Task StoreSessionContextAsync(string agentId, string sessionId, AgentContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentId))
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(agentId));
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        try
        {
            var key = GetContextKey(agentId, sessionId);
            var json = JsonSerializer.Serialize(context, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            await _database.StringSetAsync(key, json, _config.Management.SessionContextTtl);

            // Also add to agent's context list for querying
            var listKey = GetAgentContextListKey(agentId);
            await _database.ListLeftPushAsync(listKey, sessionId);
            await _database.KeyExpireAsync(listKey, _config.Management.SessionContextTtl);

            // Trim the list to prevent unlimited growth
            await _database.ListTrimAsync(listKey, 0, _config.Management.MaxContextItemsPerSession - 1);

            _logger.LogDebug("Stored context for agent {AgentId}, session {SessionId}", agentId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store context for agent {AgentId}, session {SessionId}", agentId, sessionId);
            throw;
        }
    }

    public async Task<AgentContext?> GetSessionContextAsync(string agentId, string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentId))
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(agentId));
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        try
        {
            var key = GetContextKey(agentId, sessionId);
            var json = await _database.StringGetAsync(key);

            if (!json.HasValue)
            {
                _logger.LogDebug("No context found for agent {AgentId}, session {SessionId}", agentId, sessionId);
                return null;
            }

            var context = JsonSerializer.Deserialize<AgentContext>(json!, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogDebug("Retrieved context for agent {AgentId}, session {SessionId}", agentId, sessionId);
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
        if (string.IsNullOrEmpty(agentId))
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(agentId));

        try
        {
            var listKey = GetAgentContextListKey(agentId);
            var sessionIds = await _database.ListRangeAsync(listKey);

            var contexts = new List<AgentContext>();
            var cutoffTime = DateTimeOffset.UtcNow - window;

            foreach (var sessionId in sessionIds)
            {
                if (!sessionId.HasValue) continue;

                var context = await GetSessionContextAsync(agentId, sessionId!, cancellationToken);
                if (context != null && context.Timestamp >= cutoffTime)
                {
                    contexts.Add(context);
                }
            }

            return contexts.OrderByDescending(c => c.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent context for agent {AgentId}", agentId);
            throw;
        }
    }

    public async Task<List<AgentContext>> GetAgentContextAsync(string agentId, string? topic = null, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentId))
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(agentId));

        try
        {
            var listKey = GetAgentContextListKey(agentId);
            var sessionIds = await _database.ListRangeAsync(listKey, 0, maxResults - 1);

            var contexts = new List<AgentContext>();

            foreach (var sessionId in sessionIds)
            {
                if (!sessionId.HasValue) continue;

                var context = await GetSessionContextAsync(agentId, sessionId!, cancellationToken);
                if (context != null)
                {
                    if (string.IsNullOrEmpty(topic) ||
                        context.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
                        context.Tags.Any(tag => tag.Contains(topic, StringComparison.OrdinalIgnoreCase)))
                    {
                        contexts.Add(context);
                    }
                }
            }

            return contexts.OrderByDescending(c => c.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get context for agent {AgentId}", agentId);
            throw;
        }
    }

    public async Task UpdateSessionContextAsync(string agentId, string sessionId, AgentContext context, CancellationToken cancellationToken = default)
    {
        // For Redis, update is the same as store
        await StoreSessionContextAsync(agentId, sessionId, context, cancellationToken);
    }

    public async Task DeleteSessionContextAsync(string agentId, string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentId))
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(agentId));
        if (string.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        try
        {
            var key = GetContextKey(agentId, sessionId);
            await _database.KeyDeleteAsync(key);

            // Remove from agent's context list
            var listKey = GetAgentContextListKey(agentId);
            await _database.ListRemoveAsync(listKey, sessionId);

            _logger.LogDebug("Deleted context for agent {AgentId}, session {SessionId}", agentId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete context for agent {AgentId}, session {SessionId}", agentId, sessionId);
            throw;
        }
    }

    public async Task<AgentContext> SummarizeContextAsync(string agentId, string sessionId, CancellationToken cancellationToken = default)
    {
        var context = await GetSessionContextAsync(agentId, sessionId, cancellationToken);
        if (context == null)
            throw new InvalidOperationException($"No context found for agent {agentId}, session {sessionId}");

        // Simple summarization - in production this would use AI summarization
        var summarized = new AgentContext
        {
            AgentId = context.AgentId,
            SessionId = context.SessionId,
            Topic = context.Topic,
            Entities = context.Entities.Take(10).ToList(), // Keep top entities
            Decisions = context.Decisions.TakeLast(5).ToList(), // Keep recent decisions
            Outcome = context.Outcome,
            Timestamp = context.Timestamp,
            Confidence = Math.Max(context.Confidence - 0.1f, 0.1f), // Reduce confidence slightly
            Tags = context.Tags.Concat(new[] { "summarized" }).ToList(),
            Metadata = new Dictionary<string, object>(context.Metadata)
            {
                ["summarized_at"] = DateTimeOffset.UtcNow,
                ["original_entities_count"] = context.Entities.Count,
                ["original_decisions_count"] = context.Decisions.Count
            }
        };

        await UpdateSessionContextAsync(agentId, sessionId, summarized, cancellationToken);
        return summarized;
    }

    public async Task<List<AgentContext>> SearchContextAsync(string query, string? agentId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<AgentContext>();

        try
        {
            var results = new List<AgentContext>();

            if (!string.IsNullOrEmpty(agentId))
            {
                // Search for specific agent
                var contexts = await GetAgentContextAsync(agentId, cancellationToken: cancellationToken);
                results.AddRange(FilterContextsByQuery(contexts, query));
            }
            else
            {
                // Search across all agents (this is expensive in Redis)
                // In production, consider using a search index like Redis Search
                _logger.LogWarning("Cross-agent context search is not optimized in Redis implementation");

                // For now, return empty list for cross-agent search
                // TODO: Implement Redis Search or use a different approach
            }

            return results.OrderByDescending(c => c.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search context with query: {Query}", query);
            throw;
        }
    }

    #region Private Helper Methods

    private static string GetContextKey(string agentId, string sessionId)
    {
        return $"raid:context:{agentId}:{sessionId}";
    }

    private static string GetAgentContextListKey(string agentId)
    {
        return $"raid:contexts:{agentId}";
    }

    private static List<AgentContext> FilterContextsByQuery(List<AgentContext> contexts, string query)
    {
        var queryLower = query.ToLowerInvariant();

        return contexts.Where(context =>
            context.Topic.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
            context.Entities.Any(e => e.Contains(queryLower, StringComparison.OrdinalIgnoreCase)) ||
            context.Decisions.Any(d => d.Contains(queryLower, StringComparison.OrdinalIgnoreCase)) ||
            context.Tags.Any(t => t.Contains(queryLower, StringComparison.OrdinalIgnoreCase)) ||
            (context.Outcome?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) == true)
        ).ToList();
    }

    #endregion
}