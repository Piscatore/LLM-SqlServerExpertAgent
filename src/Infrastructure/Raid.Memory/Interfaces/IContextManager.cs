using Raid.Memory.Models;

namespace Raid.Memory.Interfaces;

/// <summary>
/// Manages agent conversation context and session memory
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// Store context for an agent session
    /// </summary>
    Task StoreSessionContextAsync(string agentId, string sessionId, AgentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve context for a specific agent session
    /// </summary>
    Task<AgentContext?> GetSessionContextAsync(string agentId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get recent context for an agent within a time window
    /// </summary>
    Task<List<AgentContext>> GetRecentContextAsync(string agentId, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all context for an agent, optionally filtered by topic
    /// </summary>
    Task<List<AgentContext>> GetAgentContextAsync(string agentId, string? topic = null, int maxResults = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing context
    /// </summary>
    Task UpdateSessionContextAsync(string agentId, string sessionId, AgentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete context for a session
    /// </summary>
    Task DeleteSessionContextAsync(string agentId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Summarize context for long-running sessions
    /// </summary>
    Task<AgentContext> SummarizeContextAsync(string agentId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search context by content
    /// </summary>
    Task<List<AgentContext>> SearchContextAsync(string query, string? agentId = null, CancellationToken cancellationToken = default);
}