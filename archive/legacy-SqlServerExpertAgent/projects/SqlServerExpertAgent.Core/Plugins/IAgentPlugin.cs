using Microsoft.SemanticKernel;
using SqlServerExpertAgent.Configuration;

namespace SqlServerExpertAgent.Plugins;

/// <summary>
/// Base interface for all agent plugins with lifecycle management and configuration
/// Enables assembly separation while maintaining type safety and performance
/// </summary>
public interface IAgentPlugin
{
    /// <summary>
    /// Plugin metadata for discovery and dependency resolution
    /// </summary>
    PluginMetadata Metadata { get; }
    
    /// <summary>
    /// Initialize plugin with agent configuration and dependencies
    /// Called once during plugin loading
    /// </summary>
    Task InitializeAsync(AgentConfiguration configuration, IServiceProvider serviceProvider);
    
    /// <summary>
    /// Register plugin's kernel functions with Semantic Kernel
    /// Called after successful initialization
    /// </summary>
    void RegisterKernelFunctions(Kernel kernel);
    
    /// <summary>
    /// Validate plugin health and dependencies
    /// Used for monitoring and diagnostics
    /// </summary>
    Task<PluginHealthStatus> GetHealthStatusAsync();
    
    /// <summary>
    /// Cleanup resources when plugin is unloaded
    /// Called during graceful shutdown or hot-reload
    /// </summary>
    Task DisposeAsync();
}

/// <summary>
/// Plugin metadata for discovery, versioning, and dependency management
/// </summary>
public record PluginMetadata(
    string Name,
    Version Version,
    string Description,
    string[] Dependencies,
    PluginCapabilities Capabilities,
    Dictionary<string, object> CustomProperties
);

/// <summary>
/// Plugin capabilities flags for runtime optimization and routing
/// </summary>
[Flags]
public enum PluginCapabilities
{
    None = 0,
    SqlQuery = 1 << 0,
    SqlSchema = 1 << 1,
    SqlOptimization = 1 << 2,
    DataVisualization = 1 << 3,
    FileOperations = 1 << 4,
    WebAccess = 1 << 5,
    CacheOperations = 1 << 6,
    SecurityValidation = 1 << 7,
    PerformanceMonitoring = 1 << 8
}

/// <summary>
/// Plugin health status for monitoring and diagnostics
/// </summary>
public record PluginHealthStatus(
    bool IsHealthy,
    string Status,
    Dictionary<string, object> Metrics,
    string[] Warnings,
    string[] Errors
);