using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Raid.Memory.Configuration;
using Raid.Memory.Data;
using Raid.Memory.Interfaces;
using Raid.Memory.Models;

namespace Raid.Memory.Services;

/// <summary>
/// SQL Server-based knowledge storage with Entity Framework Core
/// </summary>
public class SqlKnowledgeBase : IKnowledgeBase
{
    private readonly ILogger<SqlKnowledgeBase> _logger;
    private readonly MemoryConfiguration _config;
    private readonly MemoryDbContext _dbContext;
    private readonly IVectorSearchEngine _vectorSearch;

    public SqlKnowledgeBase(
        ILogger<SqlKnowledgeBase> logger,
        MemoryConfiguration config,
        MemoryDbContext dbContext,
        IVectorSearchEngine vectorSearch)
    {
        _logger = logger;
        _config = config;
        _dbContext = dbContext;
        _vectorSearch = vectorSearch;
    }

    public async Task<string> StoreKnowledgeAsync(Knowledge knowledge, CancellationToken cancellationToken = default)
    {
        if (knowledge == null)
            throw new ArgumentNullException(nameof(knowledge));

        try
        {
            // Generate embedding if not provided
            if (string.IsNullOrEmpty(knowledge.EmbeddingVector))
            {
                var combinedText = $"{knowledge.Domain} {knowledge.Concept} {knowledge.Rule}";
                var embedding = await _vectorSearch.GenerateEmbeddingAsync(combinedText, cancellationToken);
                knowledge.EmbeddingVector = System.Text.Json.JsonSerializer.Serialize(embedding);
            }

            // Check if knowledge already exists
            var existing = await _dbContext.Knowledge
                .FirstOrDefaultAsync(k => k.Domain == knowledge.Domain &&
                                        k.Concept == knowledge.Concept &&
                                        k.Rule == knowledge.Rule, cancellationToken);

            if (existing != null)
            {
                // Update existing knowledge
                existing.Confidence = Math.Max(existing.Confidence, knowledge.Confidence);
                existing.LastUsedAt = DateTimeOffset.UtcNow;
                existing.UsageCount++;
                existing.Tags = knowledge.Tags.Union(existing.Tags).ToList();

                foreach (var metadata in knowledge.Metadata)
                {
                    existing.Metadata[metadata.Key] = metadata.Value;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await _vectorSearch.UpdateKnowledgeIndexAsync(existing, cancellationToken);

                _logger.LogDebug("Updated existing knowledge: {Id}", existing.Id);
                return existing.Id;
            }
            else
            {
                // Add new knowledge
                _dbContext.Knowledge.Add(knowledge);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _vectorSearch.IndexKnowledgeAsync(knowledge, cancellationToken);

                _logger.LogDebug("Stored new knowledge: {Id} in domain: {Domain}", knowledge.Id, knowledge.Domain);
                return knowledge.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store knowledge in domain: {Domain}", knowledge.Domain);
            throw;
        }
    }

    public async Task<Knowledge?> GetKnowledgeAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Knowledge ID cannot be null or empty", nameof(id));

        try
        {
            var knowledge = await _dbContext.Knowledge
                .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

            if (knowledge != null)
            {
                // Update usage statistics
                knowledge.LastUsedAt = DateTimeOffset.UtcNow;
                knowledge.UsageCount++;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Retrieved knowledge: {Id}", id);
            }

            return knowledge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve knowledge: {Id}", id);
            throw;
        }
    }

    public async Task<List<SimilarityMatch>> QuerySimilarKnowledgeAsync(string query, float threshold = 0.7f, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SimilarityMatch>();

        try
        {
            // Use vector search to find similar knowledge
            var matches = await _vectorSearch.FindSimilarAsync(query, maxResults, threshold, cancellationToken);

            // Update usage statistics for accessed knowledge
            foreach (var match in matches)
            {
                var knowledge = await _dbContext.Knowledge
                    .FirstOrDefaultAsync(k => k.Id == match.Knowledge.Id, cancellationToken);

                if (knowledge != null)
                {
                    knowledge.LastUsedAt = DateTimeOffset.UtcNow;
                    knowledge.UsageCount++;
                }
            }

            if (matches.Any())
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogDebug("Found {Count} similar knowledge items for query", matches.Count);
            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query similar knowledge for: {Query}", query);
            throw;
        }
    }

    public async Task<List<Knowledge>> GetKnowledgeByDomainAsync(string domain, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(domain))
            throw new ArgumentException("Domain cannot be null or empty", nameof(domain));

        try
        {
            var knowledge = await _dbContext.Knowledge
                .Where(k => k.Domain == domain)
                .OrderByDescending(k => k.Confidence)
                .ThenByDescending(k => k.UsageCount)
                .Take(maxResults)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {Count} knowledge items for domain: {Domain}", knowledge.Count, domain);
            return knowledge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get knowledge for domain: {Domain}", domain);
            throw;
        }
    }

    public async Task<List<Knowledge>> GetKnowledgeByTagsAsync(List<string> tags, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        if (tags == null || !tags.Any())
            return new List<Knowledge>();

        try
        {
            var knowledge = await _dbContext.Knowledge
                .Where(k => k.Tags.Any(tag => tags.Contains(tag)))
                .OrderByDescending(k => k.Confidence)
                .ThenByDescending(k => k.UsageCount)
                .Take(maxResults)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {Count} knowledge items for tags: {Tags}", knowledge.Count, string.Join(", ", tags));
            return knowledge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get knowledge for tags: {Tags}", string.Join(", ", tags));
            throw;
        }
    }

    public async Task UpdateKnowledgeAsync(Knowledge knowledge, CancellationToken cancellationToken = default)
    {
        if (knowledge == null)
            throw new ArgumentNullException(nameof(knowledge));

        try
        {
            _dbContext.Knowledge.Update(knowledge);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _vectorSearch.UpdateKnowledgeIndexAsync(knowledge, cancellationToken);

            _logger.LogDebug("Updated knowledge: {Id}", knowledge.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update knowledge: {Id}", knowledge.Id);
            throw;
        }
    }

    public async Task DeleteKnowledgeAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Knowledge ID cannot be null or empty", nameof(id));

        try
        {
            var knowledge = await _dbContext.Knowledge
                .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

            if (knowledge != null)
            {
                _dbContext.Knowledge.Remove(knowledge);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _vectorSearch.RemoveFromIndexAsync(id, cancellationToken);

                _logger.LogDebug("Deleted knowledge: {Id}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete knowledge: {Id}", id);
            throw;
        }
    }

    public async Task LinkKnowledgeAsync(string sourceId, string targetId, string relationshipType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sourceId))
            throw new ArgumentException("Source ID cannot be null or empty", nameof(sourceId));
        if (string.IsNullOrEmpty(targetId))
            throw new ArgumentException("Target ID cannot be null or empty", nameof(targetId));

        try
        {
            var sourceKnowledge = await _dbContext.Knowledge
                .FirstOrDefaultAsync(k => k.Id == sourceId, cancellationToken);

            if (sourceKnowledge != null && !sourceKnowledge.RelatedKnowledgeIds.Contains(targetId))
            {
                sourceKnowledge.RelatedKnowledgeIds.Add(targetId);
                sourceKnowledge.Metadata[$"relationship_to_{targetId}"] = relationshipType;

                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Linked knowledge {SourceId} to {TargetId} with relationship: {Type}", sourceId, targetId, relationshipType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to link knowledge {SourceId} to {TargetId}", sourceId, targetId);
            throw;
        }
    }

    public async Task<List<Knowledge>> GetRelatedKnowledgeAsync(string id, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Knowledge ID cannot be null or empty", nameof(id));

        try
        {
            var knowledge = await _dbContext.Knowledge
                .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

            if (knowledge == null || !knowledge.RelatedKnowledgeIds.Any())
                return new List<Knowledge>();

            var relatedKnowledge = await _dbContext.Knowledge
                .Where(k => knowledge.RelatedKnowledgeIds.Contains(k.Id))
                .Take(maxResults)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {Count} related knowledge items for: {Id}", relatedKnowledge.Count, id);
            return relatedKnowledge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get related knowledge for: {Id}", id);
            throw;
        }
    }

    public async Task<List<Knowledge>> SearchKnowledgeAsync(string query, string? domain = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<Knowledge>();

        try
        {
            var queryable = _dbContext.Knowledge.AsQueryable();

            if (!string.IsNullOrEmpty(domain))
            {
                queryable = queryable.Where(k => k.Domain == domain);
            }

            var knowledge = await queryable
                .Where(k => k.Concept.Contains(query) ||
                          k.Rule.Contains(query) ||
                          k.Tags.Any(tag => tag.Contains(query)))
                .OrderByDescending(k => k.Confidence)
                .ThenByDescending(k => k.UsageCount)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Found {Count} knowledge items for search query: {Query}", knowledge.Count, query);
            return knowledge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search knowledge for: {Query}", query);
            throw;
        }
    }

    public async Task<Dictionary<string, object>> GetKnowledgeStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var totalKnowledge = await _dbContext.Knowledge.CountAsync(cancellationToken);
            var domainCounts = await _dbContext.Knowledge
                .GroupBy(k => k.Domain)
                .Select(g => new { Domain = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var avgConfidence = await _dbContext.Knowledge
                .AverageAsync(k => k.Confidence, cancellationToken);

            var mostUsed = await _dbContext.Knowledge
                .OrderByDescending(k => k.UsageCount)
                .Take(5)
                .Select(k => new { k.Id, k.Concept, k.UsageCount })
                .ToListAsync(cancellationToken);

            var stats = new Dictionary<string, object>
            {
                ["total_knowledge"] = totalKnowledge,
                ["domains"] = domainCounts.ToDictionary(d => d.Domain, d => d.Count),
                ["average_confidence"] = avgConfidence,
                ["most_used_knowledge"] = mostUsed
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get knowledge statistics");
            throw;
        }
    }
}