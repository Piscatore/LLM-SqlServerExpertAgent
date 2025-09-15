using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SqlServerExpertAgent.Configuration;
using SqlServerExpertAgent.Plugins;

namespace SqlServerExpertAgent.Console;

/// <summary>
/// Core service for SQL Server Expert Agent console operations
/// Manages agent lifecycle and provides unified interface for console commands
/// </summary>
public class AgentConsoleService
{
    private readonly ConfigurationManager _configManager;
    private PluginManager? _pluginManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentConsoleService> _logger;
    
    private AgentConfiguration? _configuration;
    private Kernel? _kernel;
    private bool _initialized = false;

    public AgentConsoleService(
        ConfigurationManager configManager,
        IServiceProvider serviceProvider,
        ILogger<AgentConsoleService> logger)
    {
        _configManager = configManager;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the agent with configuration and plugins
    /// </summary>
    public async Task<bool> InitializeAsync(string? configPath = null)
    {
        try
        {
            _logger.LogInformation("Initializing SQL Server Expert Agent...");

            // Load configuration
            _configuration = await LoadConfigurationAsync();
            if (_configuration == null)
            {
                _logger.LogError("Failed to load configuration");
                return false;
            }

            // Create PluginManager with loaded configuration
            _pluginManager = new PluginManager(_configuration, _serviceProvider, _serviceProvider.GetRequiredService<ILogger<PluginManager>>());

            // Create and configure Semantic Kernel
            var builder = Kernel.CreateBuilder();
            _kernel = builder.Build();

            // Load and initialize plugins
            var pluginResult = await _pluginManager.LoadPluginsAsync(_kernel);
            
            _logger.LogInformation("Plugin loading results: {SuccessCount} successful, {FailureCount} failed", 
                pluginResult.SuccessCount, pluginResult.FailureCount);

            if (pluginResult.FailureCount > 0)
            {
                foreach (var failure in pluginResult.Results.Where(r => !r.Success))
                {
                    _logger.LogWarning("Plugin loading failed: {Name} - {Message}", failure.Name, failure.Message);
                }
            }

            _initialized = true;
            _logger.LogInformation("SQL Server Expert Agent initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQL Server Expert Agent");
            return false;
        }
    }

    /// <summary>
    /// Validate SQL syntax using the agent
    /// </summary>
    public async Task<AgentResult> ValidateSqlAsync(string sql, bool checkSecurity = true)
    {
        if (!EnsureInitialized()) return AgentResult.CreateError("Agent not initialized");

        try
        {
            var function = _kernel!.Plugins["SqlServer"]["ValidateSqlSyntax"];
            var result = await _kernel.InvokeAsync(function, new KernelArguments
            {
                ["sql"] = sql,
                ["checkSecurity"] = checkSecurity
            });

            return AgentResult.CreateSuccess(result.ToString() ?? "Validation completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating SQL");
            return AgentResult.CreateError($"Validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Execute SQL query using the agent
    /// </summary>
    public async Task<AgentResult> ExecuteQueryAsync(string sql, int timeoutSeconds = 30)
    {
        if (!EnsureInitialized()) return AgentResult.CreateError("Agent not initialized");

        try
        {
            var function = _kernel!.Plugins["SqlServer"]["ExecuteSqlQuery"];
            var result = await _kernel.InvokeAsync(function, new KernelArguments
            {
                ["sql"] = sql,
                ["timeoutSeconds"] = timeoutSeconds
            });

            return AgentResult.CreateSuccess(result.ToString() ?? "Query executed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL query");
            return AgentResult.CreateError($"Query execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get database schema information
    /// </summary>
    public async Task<AgentResult> GetSchemaAsync(string databaseName, bool includeSystemObjects = false)
    {
        if (!EnsureInitialized()) return AgentResult.CreateError("Agent not initialized");

        try
        {
            var function = _kernel!.Plugins["SqlServer"]["GetDatabaseSchema"];
            var result = await _kernel.InvokeAsync(function, new KernelArguments
            {
                ["databaseName"] = databaseName,
                ["includeSystemObjects"] = includeSystemObjects
            });

            return AgentResult.CreateSuccess(result.ToString() ?? "Schema retrieved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database schema");
            return AgentResult.CreateError($"Schema retrieval failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyze query performance
    /// </summary>
    public async Task<AgentResult> AnalyzePerformanceAsync(string sql)
    {
        if (!EnsureInitialized()) return AgentResult.CreateError("Agent not initialized");

        try
        {
            var function = _kernel!.Plugins["SqlServer"]["AnalyzeQueryPerformance"];
            var result = await _kernel.InvokeAsync(function, new KernelArguments
            {
                ["sql"] = sql
            });

            return AgentResult.CreateSuccess(result.ToString() ?? "Performance analysis completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing query performance");
            return AgentResult.CreateError($"Performance analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get agent health status
    /// </summary>
    public async Task<AgentResult> GetHealthStatusAsync()
    {
        if (!EnsureInitialized()) return AgentResult.CreateError("Agent not initialized");

        try
        {
            var healthStatuses = await _pluginManager!.GetPluginHealthAsync();
            var overallHealthy = healthStatuses.Values.All(h => h.IsHealthy);
            
            var healthReport = new
            {
                OverallHealthy = overallHealthy,
                PluginCount = healthStatuses.Count,
                HealthyPlugins = healthStatuses.Values.Count(h => h.IsHealthy),
                UnhealthyPlugins = healthStatuses.Values.Count(h => !h.IsHealthy),
                Details = healthStatuses
            };

            return AgentResult.CreateSuccess(System.Text.Json.JsonSerializer.Serialize(healthReport, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status");
            return AgentResult.CreateError($"Health check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Run comprehensive agent tests
    /// </summary>
    public async Task<AgentResult> RunTestsAsync()
    {
        if (!EnsureInitialized()) return AgentResult.CreateError("Agent not initialized");

        var results = new List<string>();
        var allSuccess = true;

        try
        {
            // Test 1: Basic SQL validation
            results.Add("=== Test 1: SQL Validation ===");
            var validationResult = await ValidateSqlAsync("SELECT 1 as TestColumn");
            results.Add($"SQL Validation: {(validationResult.Success ? "✓ PASS" : "✗ FAIL")} - {validationResult.Message}");
            if (!validationResult.Success) allSuccess = false;

            // Test 2: Invalid SQL validation
            results.Add("\n=== Test 2: Invalid SQL Detection ===");
            var invalidSqlResult = await ValidateSqlAsync("SELECT FROM WHERE");
            results.Add($"Invalid SQL Detection: {(!invalidSqlResult.Success ? "✓ PASS" : "✗ FAIL")} - Should detect syntax error");
            
            // Test 3: Schema retrieval
            results.Add("\n=== Test 3: Schema Retrieval ===");
            var schemaResult = await GetSchemaAsync("master", false);
            results.Add($"Schema Retrieval: {(schemaResult.Success ? "✓ PASS" : "✗ FAIL")} - {schemaResult.Message}");
            if (!schemaResult.Success) allSuccess = false;

            // Test 4: Health check
            results.Add("\n=== Test 4: Health Check ===");
            var healthResult = await GetHealthStatusAsync();
            results.Add($"Health Check: {(healthResult.Success ? "✓ PASS" : "✗ FAIL")} - {healthResult.Message}");
            if (!healthResult.Success) allSuccess = false;

            results.Add($"\n=== Test Summary ===");
            results.Add($"Overall Result: {(allSuccess ? "✓ ALL TESTS PASSED" : "✗ SOME TESTS FAILED")}");

            return AgentResult.CreateSuccess(string.Join(Environment.NewLine, results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running tests");
            results.Add($"✗ TEST EXECUTION FAILED: {ex.Message}");
            return AgentResult.CreateError(string.Join(Environment.NewLine, results));
        }
    }

    private async Task<AgentConfiguration?> LoadConfigurationAsync()
    {
        try
        {
            return await _configManager.LoadConfigurationAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration");
            return null;
        }
    }

    private bool EnsureInitialized()
    {
        if (!_initialized)
        {
            _logger.LogWarning("Agent operation attempted before initialization");
            return false;
        }
        return true;
    }

    public void Dispose()
    {
        _pluginManager?.Dispose();
    }
}

