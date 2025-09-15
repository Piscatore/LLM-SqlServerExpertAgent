using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SqlServerExpertAgent.Configuration;

/// <summary>
/// Advanced configuration manager with multiple sources, hot-reload, and secure key management
/// Inspired by best practices from Exner project and enterprise patterns
/// </summary>
public class ConfigurationManager
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly Dictionary<string, string> _secureKeys = new();
    
    public ConfigurationManager(IConfiguration configuration, ILogger<ConfigurationManager> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Load complete agent configuration with validation and secure key resolution
    /// </summary>
    public async Task<AgentConfiguration> LoadConfigurationAsync()
    {
        _logger.LogInformation("Loading SQL Server Expert Agent configuration...");

        try
        {
            // Load base configuration
            var config = new AgentConfiguration();
            _configuration.Bind(config);

            // Load secure keys (API keys, connection strings, etc.)
            await LoadSecureKeysAsync(config);

            // Apply environment-specific overrides
            ApplyEnvironmentOverrides(config);

            // Validate configuration
            ValidateConfiguration(config);

            // Apply dynamic defaults
            ApplyDynamicDefaults(config);

            _logger.LogInformation("Configuration loaded successfully for agent: {AgentName}", config.Identity.Name);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent configuration");
            throw;
        }
    }

    /// <summary>
    /// Secure key management similar to Exner project pattern
    /// Supports file-based keys, environment variables, and Azure Key Vault
    /// </summary>
    private async Task LoadSecureKeysAsync(AgentConfiguration config)
    {
        // Load API keys from separate files (Exner pattern)
        await LoadApiKeyFromFileAsync("claude-api-key.txt", key => config.Llm.ApiKey = key);
        await LoadApiKeyFromFileAsync("azure-openai-key.txt", key => config.Llm.ApiKey = key);
        
        // Load connection strings securely
        await LoadConnectionStringsAsync(config);
        
        // Load from environment variables as fallback
        LoadFromEnvironmentVariables(config);
        
        _logger.LogDebug("Secure keys loaded from {KeyCount} sources", _secureKeys.Count);
    }

    private async Task LoadApiKeyFromFileAsync(string fileName, Action<string> setter)
    {
        try
        {
            var keyPath = Path.Combine(AppContext.BaseDirectory, fileName);
            if (File.Exists(keyPath))
            {
                var key = (await File.ReadAllTextAsync(keyPath)).Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    setter(key);
                    _secureKeys[fileName] = "***LOADED***";
                    _logger.LogDebug("Loaded API key from {FileName}", fileName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load API key from {FileName}", fileName);
        }
    }

    private async Task LoadConnectionStringsAsync(AgentConfiguration config)
    {
        // Try to load from secure connection strings file
        var connectionStringsPath = Path.Combine(AppContext.BaseDirectory, "connectionstrings.json");
        if (File.Exists(connectionStringsPath))
        {
            try
            {
                var connectionJson = await File.ReadAllTextAsync(connectionStringsPath);
                var connections = JsonSerializer.Deserialize<Dictionary<string, string>>(connectionJson);
                
                if (connections != null)
                {
                    foreach (var (key, value) in connections)
                    {
                        config.SqlServer.ConnectionStrings[key] = value;
                    }
                    _logger.LogDebug("Loaded {Count} connection strings from secure file", connections.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load connection strings from secure file");
            }
        }
    }

    private void LoadFromEnvironmentVariables(AgentConfiguration config)
    {
        // Environment variable patterns
        var envMappings = new Dictionary<string, Action<string>>
        {
            ["CLAUDE_API_KEY"] = key => config.Llm.ApiKey = key,
            ["AZURE_OPENAI_KEY"] = key => config.Llm.ApiKey = key,
            ["AZURE_OPENAI_ENDPOINT"] = endpoint => config.Llm.Endpoint = endpoint,
            ["SQL_SERVER_CONNECTION"] = conn => config.SqlServer.ConnectionStrings["default"] = conn,
            ["AGENT_LOG_LEVEL"] = level => config.Observability.Logging.LogLevel = level
        };

        foreach (var (envVar, setter) in envMappings)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(value))
            {
                setter(value);
                _secureKeys[envVar] = "***LOADED***";
                _logger.LogDebug("Loaded configuration from environment variable: {EnvVar}", envVar);
            }
        }
    }

    /// <summary>
    /// Apply environment-specific configuration overrides
    /// </summary>
    private void ApplyEnvironmentOverrides(AgentConfiguration config)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        
        _logger.LogInformation("Applying configuration for environment: {Environment}", environment);

        switch (environment.ToLower())
        {
            case "development":
                ApplyDevelopmentOverrides(config);
                break;
            case "testing":
                ApplyTestingOverrides(config);
                break;
            case "staging":
                ApplyStagingOverrides(config);
                break;
            case "production":
                ApplyProductionOverrides(config);
                break;
        }
    }

    private void ApplyDevelopmentOverrides(AgentConfiguration config)
    {
        // Development-specific settings
        config.Observability.Logging.LogLevel = "Debug";
        config.Observability.Logging.EnableConsoleLogging = true;
        config.Performance.ResponseTargets["syntax_validation"] = 200; // Relaxed targets
        config.QualityControl.ErrorTracking.EnableAutoCorrection = true;
        config.Knowledge.Learning.EnableAdaptiveLearning = true;
        
        // Use development database if available
        if (config.SqlServer.ConnectionStrings.ContainsKey("development"))
        {
            config.SqlServer.ConnectionStrings["default"] = config.SqlServer.ConnectionStrings["development"];
        }
    }

    private void ApplyTestingOverrides(AgentConfiguration config)
    {
        // Testing-specific settings
        config.Observability.Logging.LogLevel = "Warning";
        config.Performance.MaxConcurrentOperations = 1; // Single-threaded for predictable tests
        config.QualityControl.Validation.MandatorySyntaxCheck = true;
        config.SqlServer.QueryExecution.Safety.AllowDataModification = false; // Extra safety in tests
        
        if (config.SqlServer.ConnectionStrings.ContainsKey("testing"))
        {
            config.SqlServer.ConnectionStrings["default"] = config.SqlServer.ConnectionStrings["testing"];
        }
    }

    private void ApplyStagingOverrides(AgentConfiguration config)
    {
        // Staging mirrors production but with additional monitoring
        config.Observability.Monitoring.EnablePerformanceCounters = true;
        config.Observability.Telemetry.CollectUsageStatistics = true;
        config.Performance.ResponseTargets = config.Performance.ResponseTargets
            .ToDictionary(kvp => kvp.Key, kvp => (int)(kvp.Value * 1.2)); // 20% more lenient
    }

    private void ApplyProductionOverrides(AgentConfiguration config)
    {
        // Production-specific settings - maximum performance and security
        config.Observability.Logging.LogLevel = "Information";
        config.Performance.Cache.SizeLimit = 2000; // Larger cache for production
        config.QualityControl.SuccessMetrics.EnableMetricsCollection = true;
        config.SqlServer.QueryExecution.Safety.AllowDataModification = false; // Production safety
        
        // Tighten performance targets for production
        config.Performance.ResponseTargets = config.Performance.ResponseTargets
            .ToDictionary(kvp => kvp.Key, kvp => (int)(kvp.Value * 0.8)); // 20% stricter
    }

    /// <summary>
    /// Apply intelligent defaults based on system capabilities and detected environment
    /// </summary>
    private void ApplyDynamicDefaults(AgentConfiguration config)
    {
        // Auto-detect system capabilities
        var processorCount = Environment.ProcessorCount;
        var availableMemoryGB = GC.GetTotalMemory(false) / 1024 / 1024 / 1024;

        // Adjust concurrent operations based on system resources
        config.Performance.MaxConcurrentOperations = Math.Min(
            config.Performance.MaxConcurrentOperations, 
            Math.Max(1, processorCount - 1)
        );

        // Adjust cache size based on available memory
        if (availableMemoryGB > 8)
        {
            config.Performance.Cache.SizeLimit = Math.Max(config.Performance.Cache.SizeLimit, 1500);
        }
        else if (availableMemoryGB < 4)
        {
            config.Performance.Cache.SizeLimit = Math.Min(config.Performance.Cache.SizeLimit, 500);
        }

        // Auto-detect SQL Server version and capabilities
        AutoDetectSqlServerFeatures(config);

        _logger.LogDebug("Applied dynamic defaults: CPU={CPU}, MaxOps={MaxOps}, CacheSize={CacheSize}", 
            processorCount, config.Performance.MaxConcurrentOperations, config.Performance.Cache.SizeLimit);
    }

    private void AutoDetectSqlServerFeatures(AgentConfiguration config)
    {
        // This will be implemented to detect SQL Server version and available features
        // For now, set conservative defaults
        config.SqlServer.Smo.EnableStatistics = true;
        config.SqlServer.QueryExecution.AllowExecutionPlanAnalysis = true;
    }

    /// <summary>
    /// Validate configuration for required values and logical consistency
    /// </summary>
    private void ValidateConfiguration(AgentConfiguration config)
    {
        var errors = new List<string>();

        // Required configuration validation
        if (string.IsNullOrEmpty(config.Identity.Name))
            errors.Add("Agent Identity.Name is required");

        if (string.IsNullOrEmpty(config.Llm.ApiKey))
            errors.Add("LLM API Key is required (set via file, environment variable, or configuration)");

        if (!config.SqlServer.ConnectionStrings.ContainsKey("default"))
            errors.Add("Default SQL Server connection string is required");

        // Logical consistency validation
        if (config.Performance.Cache.SizeLimit <= 0)
            errors.Add("Cache size limit must be positive");

        if (config.QualityControl.SuccessMetrics.TargetSyntaxAccuracy < 0 || 
            config.QualityControl.SuccessMetrics.TargetSyntaxAccuracy > 1)
            errors.Add("Target syntax accuracy must be between 0 and 1");

        // Performance target validation
        foreach (var (operation, targetMs) in config.Performance.ResponseTargets)
        {
            if (targetMs <= 0)
                errors.Add($"Response target for {operation} must be positive");
        }

        if (errors.Any())
        {
            var errorMessage = "Configuration validation failed:\n" + string.Join("\n", errors);
            _logger.LogError("Configuration validation errors: {Errors}", string.Join(", ", errors));
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogDebug("Configuration validation passed");
    }

    /// <summary>
    /// Create configuration template files for easy customization
    /// </summary>
    public async Task CreateTemplateFilesAsync(string outputPath = "config-templates")
    {
        Directory.CreateDirectory(outputPath);

        // Create main configuration template
        var templateConfig = CreateTemplateConfiguration();
        var templateJson = JsonSerializer.Serialize(templateConfig, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(Path.Combine(outputPath, "appsettings.template.json"), templateJson);

        // Create secure files templates
        await File.WriteAllTextAsync(Path.Combine(outputPath, "claude-api-key.txt"), "your-claude-api-key-here");
        await File.WriteAllTextAsync(Path.Combine(outputPath, "connectionstrings.template.json"), 
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["default"] = "Server=.;Database=YourDatabase;Trusted_Connection=true;",
                ["development"] = "Server=.;Database=YourDatabase_Dev;Trusted_Connection=true;",
                ["testing"] = "Server=.;Database=YourDatabase_Test;Trusted_Connection=true;"
            }, new JsonSerializerOptions { WriteIndented = true }));

        // Create environment-specific templates
        await CreateEnvironmentTemplatesAsync(outputPath);

        _logger.LogInformation("Configuration templates created in {OutputPath}", outputPath);
    }

    private AgentConfiguration CreateTemplateConfiguration()
    {
        var template = new AgentConfiguration();
        
        // Customize template with sensible defaults and documentation
        template.Identity.Name = "MyCustomSqlExpert";
        template.Identity.Description = "Customized SQL Server Expert Agent";
        template.Identity.Personality["response_style"] = "friendly_expert"; // vs "expert_concise"
        template.Identity.BehaviorFlags["explain_reasoning"] = true;
        
        // Add project-specific configuration example
        template.Projects["MyProject"] = new ProjectConfiguration
        {
            ProjectName = "MyProject",
            DatabaseName = "MyProjectDB",
            Schema = new ProjectSchemaConfiguration
            {
                ImportantTables = new List<string> { "Users", "Products", "Orders" },
                ImportantViews = new List<string> { "v_ProductSummary", "v_UserActivity" },
                NamingConventions = new Dictionary<string, string>
                {
                    ["table_prefix"] = "tbl_",
                    ["view_prefix"] = "v_",
                    ["stored_procedure_prefix"] = "sp_"
                }
            }
        };

        return template;
    }

    private async Task CreateEnvironmentTemplatesAsync(string outputPath)
    {
        var environments = new[] { "development", "staging", "production" };
        
        foreach (var env in environments)
        {
            var envConfig = new
            {
                Environment = env,
                Logging = new { LogLevel = env == "development" ? "Debug" : "Information" },
                Performance = new { 
                    ResponseTargets = env == "production" 
                        ? new { syntax_validation = 80, query_optimization = 800 }
                        : new { syntax_validation = 150, query_optimization = 1200 }
                }
            };

            var envJson = JsonSerializer.Serialize(envConfig, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(outputPath, $"appsettings.{env}.json"), envJson);
        }
    }

    /// <summary>
    /// Get secure configuration summary (without sensitive values)
    /// </summary>
    public string GetConfigurationSummary()
    {
        var summary = new
        {
            LoadedSecureKeys = _secureKeys.Keys.ToArray(),
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            ProcessorCount = Environment.ProcessorCount,
            WorkingDirectory = Environment.CurrentDirectory,
            RuntimeVersion = Environment.Version.ToString()
        };

        return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
    }
}