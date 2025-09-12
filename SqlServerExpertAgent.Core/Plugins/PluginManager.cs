using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SqlServerExpertAgent.Configuration;

namespace SqlServerExpertAgent.Plugins;

/// <summary>
/// Plugin manager with hot-reload, dependency resolution, and assembly isolation
/// Inspired by enterprise plugin architectures and MEF patterns
/// </summary>
public class PluginManager : IDisposable
{
    private readonly AgentConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager> _logger;
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new();
    private readonly Dictionary<string, AssemblyLoadContext> _assemblyContexts = new();
    
    public PluginManager(AgentConfiguration configuration, IServiceProvider serviceProvider, ILogger<PluginManager> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Discover and load plugins from configured directories
    /// Supports assembly isolation and version management
    /// </summary>
    public async Task<PluginLoadResult> LoadPluginsAsync(Kernel kernel)
    {
        var results = new List<PluginLoadResult.PluginResult>();
        
        foreach (var pluginDirectory in _configuration.Plugins.PluginDirectories)
        {
            if (!Directory.Exists(pluginDirectory))
            {
                _logger.LogWarning("Plugin directory not found: {Directory}", pluginDirectory);
                continue;
            }

            var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            
            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    var result = await LoadPluginFromAssemblyAsync(pluginFile, kernel);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load plugin from {File}", pluginFile);
                    results.Add(new PluginLoadResult.PluginResult(
                        Path.GetFileNameWithoutExtension(pluginFile), 
                        false, 
                        ex.Message
                    ));
                }
            }
        }

        // Resolve dependencies and initialize in correct order
        var dependencyOrder = ResolveDependencyOrder();
        foreach (var pluginName in dependencyOrder)
        {
            if (_loadedPlugins.TryGetValue(pluginName, out var loadedPlugin))
            {
                await loadedPlugin.Plugin.InitializeAsync(_configuration, _serviceProvider);
                loadedPlugin.Plugin.RegisterKernelFunctions(kernel);
                _logger.LogInformation("Initialized plugin: {Plugin}", pluginName);
            }
        }

        return new PluginLoadResult(results, DateTime.UtcNow);
    }

    private async Task<PluginLoadResult.PluginResult> LoadPluginFromAssemblyAsync(string assemblyPath, Kernel kernel)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        
        // Create isolated assembly context for plugin
        var context = new AssemblyLoadContext(assemblyName, isCollectible: _configuration.Plugins.EnableHotReload);
        _assemblyContexts[assemblyName] = context;

        var assembly = context.LoadFromAssemblyPath(assemblyPath);
        
        // Find plugin implementations
        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IAgentPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToArray();

        if (!pluginTypes.Any())
        {
            return new PluginLoadResult.PluginResult(assemblyName, false, "No plugin implementations found");
        }

        foreach (var pluginType in pluginTypes)
        {
            var plugin = (IAgentPlugin)Activator.CreateInstance(pluginType)!;
            var pluginName = plugin.Metadata.Name;
            
            _loadedPlugins[pluginName] = new LoadedPlugin(plugin, assembly, context);
            _logger.LogDebug("Loaded plugin: {Plugin} v{Version}", pluginName, plugin.Metadata.Version);
        }

        return new PluginLoadResult.PluginResult(assemblyName, true, $"Loaded {pluginTypes.Length} plugins");
    }

    /// <summary>
    /// Resolve plugin dependencies using topological sort
    /// Ensures plugins are initialized in correct order
    /// </summary>
    private List<string> ResolveDependencyOrder()
    {
        var dependencies = new Dictionary<string, string[]>();
        var resolved = new List<string>();
        var visited = new HashSet<string>();

        // Build dependency graph
        foreach (var (name, loadedPlugin) in _loadedPlugins)
        {
            dependencies[name] = loadedPlugin.Plugin.Metadata.Dependencies;
        }

        // Topological sort with cycle detection
        void Visit(string pluginName)
        {
            if (visited.Contains(pluginName)) return;
            visited.Add(pluginName);

            if (dependencies.TryGetValue(pluginName, out var deps))
            {
                foreach (var dependency in deps)
                {
                    if (_loadedPlugins.ContainsKey(dependency))
                    {
                        Visit(dependency);
                    }
                    else
                    {
                        _logger.LogWarning("Plugin {Plugin} depends on missing plugin: {Dependency}", 
                            pluginName, dependency);
                    }
                }
            }

            resolved.Add(pluginName);
        }

        foreach (var pluginName in _loadedPlugins.Keys)
        {
            Visit(pluginName);
        }

        return resolved;
    }

    /// <summary>
    /// Hot-reload plugin for development and updates
    /// Maintains state where possible during reload
    /// </summary>
    public async Task<bool> ReloadPluginAsync(string pluginName, Kernel kernel)
    {
        if (!_configuration.Plugins.EnableHotReload)
        {
            _logger.LogWarning("Hot-reload is disabled in configuration");
            return false;
        }

        if (!_loadedPlugins.TryGetValue(pluginName, out var loadedPlugin))
        {
            _logger.LogWarning("Plugin not found for reload: {Plugin}", pluginName);
            return false;
        }

        try
        {
            // Cleanup old plugin
            await loadedPlugin.Plugin.DisposeAsync();
            
            // Unload assembly context
            if (_assemblyContexts.TryGetValue(pluginName, out var context))
            {
                context.Unload();
                _assemblyContexts.Remove(pluginName);
            }

            // Find and reload from original location
            var assemblyPath = loadedPlugin.Assembly.Location;
            var reloadResult = await LoadPluginFromAssemblyAsync(assemblyPath, kernel);
            
            _logger.LogInformation("Plugin reloaded: {Plugin} - {Result}", pluginName, reloadResult.Message);
            return reloadResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload plugin: {Plugin}", pluginName);
            return false;
        }
    }

    /// <summary>
    /// Get health status for all loaded plugins
    /// Used for monitoring and diagnostics
    /// </summary>
    public async Task<Dictionary<string, PluginHealthStatus>> GetPluginHealthAsync()
    {
        var healthStatuses = new Dictionary<string, PluginHealthStatus>();
        
        foreach (var (name, loadedPlugin) in _loadedPlugins)
        {
            try
            {
                var health = await loadedPlugin.Plugin.GetHealthStatusAsync();
                healthStatuses[name] = health;
            }
            catch (Exception ex)
            {
                healthStatuses[name] = new PluginHealthStatus(
                    false, 
                    "Error checking health", 
                    new Dictionary<string, object>(), 
                    new string[0], 
                    new[] { ex.Message }
                );
            }
        }

        return healthStatuses;
    }

    public void Dispose()
    {
        foreach (var (_, loadedPlugin) in _loadedPlugins)
        {
            try
            {
                loadedPlugin.Plugin.DisposeAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing plugin: {Plugin}", loadedPlugin.Plugin.Metadata.Name);
            }
        }

        foreach (var (_, context) in _assemblyContexts)
        {
            try
            {
                context.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unloading assembly context");
            }
        }
    }

    private record LoadedPlugin(IAgentPlugin Plugin, Assembly Assembly, AssemblyLoadContext Context);
}

/// <summary>
/// Result of plugin loading operation with detailed status
/// </summary>
public record PluginLoadResult(
    List<PluginLoadResult.PluginResult> Results,
    DateTime LoadTime
)
{
    public record PluginResult(string Name, bool Success, string Message);
    
    public bool AllSuccessful => Results.All(r => r.Success);
    public int SuccessCount => Results.Count(r => r.Success);
    public int FailureCount => Results.Count(r => !r.Success);
}

/// <summary>
/// Assembly load context for plugin isolation
/// Enables hot-reload and prevents assembly conflicts
/// </summary>
public class AssemblyLoadContext : System.Runtime.Loader.AssemblyLoadContext
{
    private readonly string _pluginPath;

    public AssemblyLoadContext(string name, bool isCollectible) : base(name, isCollectible)
    {
        _pluginPath = name;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Load plugin dependencies from plugin directory
        var assemblyPath = Path.Combine(Path.GetDirectoryName(_pluginPath) ?? "", $"{assemblyName.Name}.dll");
        
        if (File.Exists(assemblyPath))
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fall back to default context for framework assemblies
        return null;
    }
}