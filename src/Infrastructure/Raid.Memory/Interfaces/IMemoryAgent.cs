using Raid.Memory.Models;

namespace Raid.Memory.Interfaces;

/// <summary>
/// Main interface for the RAID Memory Agent
/// Combines all memory capabilities into a unified service
/// </summary>
public interface IMemoryAgent
{
    // Context Management
    /// <summary>
    /// Store conversation context for an agent
    /// </summary>
    Task StoreContextAsync(string agentId, string sessionId, AgentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve conversation context
    /// </summary>
    Task<AgentContext?> GetContextAsync(string agentId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent context for an agent
    /// </summary>
    Task<List<AgentContext>> GetRecentContextAsync(string agentId, TimeSpan window, CancellationToken cancellationToken = default);

    // Knowledge Management
    /// <summary>
    /// Store learned knowledge
    /// </summary>
    Task<string> StoreKnowledgeAsync(Knowledge knowledge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query knowledge using semantic search
    /// </summary>
    Task<List<Knowledge>> QueryKnowledgeAsync(string query, string? domain = null, float threshold = 0.7f, int maxResults = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get knowledge by domain
    /// </summary>
    Task<List<Knowledge>> GetKnowledgeByDomainAsync(string domain, int maxResults = 100, CancellationToken cancellationToken = default);

    // Cross-Agent Coordination
    /// <summary>
    /// Share context between agents
    /// </summary>
    Task ShareContextAsync(string fromAgentId, string toAgentId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get shared knowledge relevant to multiple agents
    /// </summary>
    Task<List<Knowledge>> GetSharedKnowledgeAsync(List<string> agentIds, string? topic = null, CancellationToken cancellationToken = default);

    // Learning and Adaptation
    /// <summary>
    /// Learn from successful agent interactions
    /// </summary>
    Task LearnFromInteractionAsync(string agentId, AgentContext context, string outcome, float confidence, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recommendations based on current context
    /// </summary>
    Task<List<Knowledge>> GetRecommendationsAsync(string agentId, AgentContext currentContext, int maxResults = 5, CancellationToken cancellationToken = default);

    // Memory Management
    /// <summary>
    /// Summarize old context to save space
    /// </summary>
    Task SummarizeOldContextAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up expired or low-confidence knowledge
    /// </summary>
    Task CleanupMemoryAsync(float minConfidence = 0.3f, TimeSpan olderThan = default, CancellationToken cancellationToken = default);

    // Statistics and Health
    /// <summary>
    /// Get memory usage statistics
    /// </summary>
    Task<Dictionary<string, object>> GetMemoryStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check memory agent health
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}