using Raid.Memory.Models;

namespace Raid.Memory.Interfaces;

/// <summary>
/// Manages persistent knowledge storage and retrieval
/// </summary>
public interface IKnowledgeBase
{
    /// <summary>
    /// Store new knowledge in the knowledge base
    /// </summary>
    Task<string> StoreKnowledgeAsync(Knowledge knowledge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve knowledge by ID
    /// </summary>
    Task<Knowledge?> GetKnowledgeAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query knowledge using semantic similarity
    /// </summary>
    Task<List<SimilarityMatch>> QuerySimilarKnowledgeAsync(string query, float threshold = 0.7f, int maxResults = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get knowledge filtered by domain
    /// </summary>
    Task<List<Knowledge>> GetKnowledgeByDomainAsync(string domain, int maxResults = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get knowledge filtered by tags
    /// </summary>
    Task<List<Knowledge>> GetKnowledgeByTagsAsync(List<string> tags, int maxResults = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing knowledge
    /// </summary>
    Task UpdateKnowledgeAsync(Knowledge knowledge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete knowledge by ID
    /// </summary>
    Task DeleteKnowledgeAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Link related knowledge items
    /// </summary>
    Task LinkKnowledgeAsync(string sourceId, string targetId, string relationshipType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get related knowledge for a given knowledge item
    /// </summary>
    Task<List<Knowledge>> GetRelatedKnowledgeAsync(string id, int maxResults = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search knowledge by text content
    /// </summary>
    Task<List<Knowledge>> SearchKnowledgeAsync(string query, string? domain = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get knowledge usage statistics
    /// </summary>
    Task<Dictionary<string, object>> GetKnowledgeStatsAsync(CancellationToken cancellationToken = default);
}