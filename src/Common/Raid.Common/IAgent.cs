namespace Raid.Common;

/// <summary>
/// Base interface for all RAID Platform agents
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Unique identifier for the agent
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name of the agent
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Version of the agent
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Initialize the agent with configuration
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start the agent
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the agent
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}