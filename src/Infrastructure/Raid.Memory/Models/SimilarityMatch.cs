namespace Raid.Memory.Models;

/// <summary>
/// Represents a similarity match from vector search
/// </summary>
public class SimilarityMatch
{
    /// <summary>
    /// The knowledge item that matched
    /// </summary>
    public Knowledge Knowledge { get; set; } = new();

    /// <summary>
    /// Similarity score (0.0 to 1.0, higher is more similar)
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Distance metric used for comparison
    /// </summary>
    public string DistanceMetric { get; set; } = "cosine";

    /// <summary>
    /// Additional metadata about the match
    /// </summary>
    public Dictionary<string, object> MatchMetadata { get; set; } = new();
}