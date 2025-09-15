using System.ComponentModel.DataAnnotations;

namespace SqlServerExpertAgent.Configuration;

/// <summary>
/// Comprehensive configuration for SQL Server Expert Agent
/// Supports multiple configuration sources and hot-reload capability
/// </summary>
public class AgentConfiguration
{
    /// <summary>
    /// Core agent identity and behavior settings
    /// </summary>
    public AgentIdentity Identity { get; set; } = new();

    /// <summary>
    /// Performance and resource management settings
    /// </summary>
    public PerformanceSettings Performance { get; set; } = new();

    /// <summary>
    /// Knowledge base configuration and caching
    /// </summary>
    public KnowledgeConfiguration Knowledge { get; set; } = new();

    /// <summary>
    /// SQL Server connection and SMO settings
    /// </summary>
    public SqlServerConfiguration SqlServer { get; set; } = new();

    /// <summary>
    /// LLM and Semantic Kernel configuration
    /// </summary>
    public LlmConfiguration Llm { get; set; } = new();

    /// <summary>
    /// Quality control and validation settings
    /// </summary>
    public QualityControlConfiguration QualityControl { get; set; } = new();

    /// <summary>
    /// Project-specific context configurations
    /// </summary>
    public Dictionary<string, ProjectConfiguration> Projects { get; set; } = new();

    /// <summary>
    /// Plugin and extension configurations
    /// </summary>
    public PluginConfiguration Plugins { get; set; } = new();

    /// <summary>
    /// Logging and monitoring configuration
    /// </summary>
    public ObservabilityConfiguration Observability { get; set; } = new();
}

/// <summary>
/// Agent identity and personality configuration
/// </summary>
public class AgentIdentity
{
    [Required]
    public string Name { get; set; } = "SqlExpert";
    
    public string Version { get; set; } = "1.0.0";
    
    public string Description { get; set; } = "SQL Server Expert Agent with SMO integration";
    
    /// <summary>
    /// Agent personality traits (authoritative, helpful, proactive, etc.)
    /// </summary>
    public Dictionary<string, object> Personality { get; set; } = new()
    {
        ["authoritative"] = true,
        ["proactive_optimization"] = true,
        ["educational_explanations"] = true,
        ["confidence_level"] = 0.95,
        ["response_style"] = "expert_concise"
    };

    /// <summary>
    /// Specialized domain focus areas
    /// </summary>
    public List<string> Specializations { get; set; } = new()
    {
        "T-SQL", "Performance Optimization", "Schema Design", 
        "Error Prevention", "Best Practices", "SMO Integration"
    };

    /// <summary>
    /// Custom agent behavior modifiers
    /// </summary>
    public Dictionary<string, bool> BehaviorFlags { get; set; } = new()
    {
        ["always_validate_syntax"] = true,
        ["suggest_optimizations"] = true,
        ["explain_reasoning"] = true,
        ["cache_frequent_patterns"] = true,
        ["learn_from_errors"] = true
    };
}

/// <summary>
/// Performance tuning and resource management
/// </summary>
public class PerformanceSettings
{
    /// <summary>
    /// Memory cache configuration
    /// </summary>
    public CacheConfiguration Cache { get; set; } = new();
    
    /// <summary>
    /// Response time targets (milliseconds)
    /// </summary>
    public Dictionary<string, int> ResponseTargets { get; set; } = new()
    {
        ["syntax_validation"] = 100,
        ["schema_introspection"] = 500,
        ["query_optimization"] = 1000,
        ["complex_analysis"] = 3000
    };

    /// <summary>
    /// Concurrent operation limits
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = 10;
    
    /// <summary>
    /// Background task intervals (seconds)
    /// </summary>
    public Dictionary<string, int> BackgroundTasks { get; set; } = new()
    {
        ["schema_cache_refresh"] = 300,
        ["knowledge_base_update"] = 3600,
        ["performance_metrics_collect"] = 60
    };
}

/// <summary>
/// Memory cache configuration with intelligent management
/// </summary>
public class CacheConfiguration
{
    public int SizeLimit { get; set; } = 1000; // Number of cached items
    public int ExpirationMinutes { get; set; } = 30;
    public int CompactionPercentage { get; set; } = 25;
    
    /// <summary>
    /// Cache priority weights for different content types
    /// </summary>
    public Dictionary<string, double> PriorityWeights { get; set; } = new()
    {
        ["syntax_rules"] = 1.0,
        ["error_patterns"] = 0.9,
        ["optimization_tips"] = 0.8,
        ["schema_metadata"] = 0.7,
        ["query_plans"] = 0.6
    };

    /// <summary>
    /// Preload patterns that should always be in cache
    /// </summary>
    public List<string> PreloadPatterns { get; set; } = new()
    {
        "bracket_escaping", "table_aliases", "cte_scope_rules",
        "common_syntax_errors", "performance_antipatterns"
    };
}

/// <summary>
/// Knowledge base and learning configuration
/// </summary>
public class KnowledgeConfiguration
{
    /// <summary>
    /// Knowledge base file paths and sources
    /// </summary>
    public KnowledgeSources Sources { get; set; } = new();
    
    /// <summary>
    /// Vector search and semantic matching settings
    /// </summary>
    public VectorSearchConfiguration VectorSearch { get; set; } = new();
    
    /// <summary>
    /// Learning and adaptation settings
    /// </summary>
    public LearningConfiguration Learning { get; set; } = new();
}

public class KnowledgeSources
{
    public string SqlSyntaxRulesPath { get; set; } = "knowledge/sql-syntax-rules.json";
    public string PerformanceGuidelinesPath { get; set; } = "knowledge/performance-guidelines.json";
    public string ErrorPatternsPath { get; set; } = "knowledge/error-patterns.json";
    public string BestPracticesPath { get; set; } = "knowledge/best-practices.json";
    
    /// <summary>
    /// External knowledge sources (URLs, databases, etc.)
    /// </summary>
    public Dictionary<string, string> ExternalSources { get; set; } = new();
}

public class VectorSearchConfiguration
{
    public bool Enabled { get; set; } = true;
    public int EmbeddingDimensions { get; set; } = 1536;
    public double SimilarityThreshold { get; set; } = 0.7;
    public int MaxResults { get; set; } = 10;
}

public class LearningConfiguration
{
    public bool EnableAdaptiveLearning { get; set; } = true;
    public bool SaveUserFeedback { get; set; } = true;
    public string FeedbackStoragePath { get; set; } = "feedback/user-corrections.json";
    
    /// <summary>
    /// Learning rate for pattern recognition (0.0 to 1.0)
    /// </summary>
    public double LearningRate { get; set; } = 0.1;
}

/// <summary>
/// SQL Server and SMO configuration
/// </summary>
public class SqlServerConfiguration
{
    /// <summary>
    /// Connection strings for different environments
    /// </summary>
    public Dictionary<string, string> ConnectionStrings { get; set; } = new()
    {
        ["default"] = "Server=.;Database=OrfPIM2;Trusted_Connection=true;",
        ["development"] = "Server=.;Database=OrfPIM2_Dev;Trusted_Connection=true;",
        ["testing"] = "Server=.;Database=OrfPIM2_Test;Trusted_Connection=true;"
    };

    /// <summary>
    /// Default database to use when none is specified
    /// </summary>
    public string? DefaultDatabase { get; set; } = "master";

    /// <summary>
    /// SMO-specific settings
    /// </summary>
    public SmoConfiguration Smo { get; set; } = new();
    
    /// <summary>
    /// Query execution settings
    /// </summary>
    public QueryExecutionConfiguration QueryExecution { get; set; } = new();
}

public class SmoConfiguration
{
    public int ConnectionTimeout { get; set; } = 30;
    public int CommandTimeout { get; set; } = 60;
    public bool EnableStatistics { get; set; } = true;
    public bool CacheSchemaMetadata { get; set; } = true;
    
    /// <summary>
    /// Schema objects to introspect by default
    /// </summary>
    public List<string> DefaultIntrospectionObjects { get; set; } = new()
    {
        "Tables", "Views", "StoredProcedures", "Functions", "Indexes"
    };
}

public class QueryExecutionConfiguration
{
    public bool AllowExecutionPlanAnalysis { get; set; } = true;
    public bool EnableQueryHints { get; set; } = true;
    public int MaxExecutionTimeSeconds { get; set; } = 30;
    
    /// <summary>
    /// Safety restrictions for query execution
    /// </summary>
    public QuerySafetyConfiguration Safety { get; set; } = new();
}

public class QuerySafetyConfiguration
{
    public bool AllowDataModification { get; set; } = false;
    public bool AllowSchemaChanges { get; set; } = false;
    public List<string> ProhibitedKeywords { get; set; } = new()
    {
        "DROP", "TRUNCATE", "DELETE", "UPDATE", "INSERT"
    };
    public int MaxRowsReturned { get; set; } = 1000;
    public int DefaultTimeout { get; set; } = 30;
}

/// <summary>
/// LLM and Semantic Kernel configuration
/// </summary>
public class LlmConfiguration
{
    public string Provider { get; set; } = "AzureOpenAI";
    public string Model { get; set; } = "gpt-4";
    public string ApiKey { get; set; } = "";
    public string Endpoint { get; set; } = "";
    
    /// <summary>
    /// Model-specific parameters
    /// </summary>
    public ModelParameters Parameters { get; set; } = new();
    
    /// <summary>
    /// Function calling configuration
    /// </summary>
    public FunctionCallingConfiguration FunctionCalling { get; set; } = new();
}

public class ModelParameters
{
    public double Temperature { get; set; } = 0.1; // Low for consistent, factual responses
    public int MaxTokens { get; set; } = 4000;
    public double TopP { get; set; } = 0.95;
    public double FrequencyPenalty { get; set; } = 0.0;
    public double PresencePenalty { get; set; } = 0.0;
    public List<string> StopSequences { get; set; } = new();
}

public class FunctionCallingConfiguration
{
    public bool AutoExecuteFunctions { get; set; } = true;
    public int MaxFunctionCalls { get; set; } = 10;
    public Dictionary<string, bool> EnabledFunctions { get; set; } = new()
    {
        ["validate_sql_syntax"] = true,
        ["analyze_query_performance"] = true,
        ["introspect_database_schema"] = true,
        ["suggest_optimizations"] = true,
        ["explain_execution_plan"] = true
    };
}

/// <summary>
/// Quality control and validation configuration
/// </summary>
public class QualityControlConfiguration
{
    /// <summary>
    /// Mandatory validation steps before response delivery
    /// </summary>
    public ValidationConfiguration Validation { get; set; } = new();
    
    /// <summary>
    /// Error tracking and learning configuration
    /// </summary>
    public ErrorTrackingConfiguration ErrorTracking { get; set; } = new();
    
    /// <summary>
    /// Success metrics and targets
    /// </summary>
    public SuccessMetricsConfiguration SuccessMetrics { get; set; } = new();
}

public class ValidationConfiguration
{
    public bool MandatorySyntaxCheck { get; set; } = true;
    public bool RequireExplanations { get; set; } = true;
    public bool ValidateAgainstSchema { get; set; } = true;
    public bool CheckPerformanceImpact { get; set; } = true;
    
    /// <summary>
    /// Custom validation rules
    /// </summary>
    public List<string> CustomValidationRules { get; set; } = new()
    {
        "no_select_star", "require_table_aliases", "check_index_usage"
    };
}

public class ErrorTrackingConfiguration
{
    public bool TrackSyntaxErrors { get; set; } = true;
    public bool TrackLogicErrors { get; set; } = true;
    public bool TrackPerformanceIssues { get; set; } = true;
    public string ErrorLogPath { get; set; } = "logs/errors.json";
    
    /// <summary>
    /// Auto-correction attempt configuration
    /// </summary>
    public bool EnableAutoCorrection { get; set; } = true;
    public int MaxCorrectionAttempts { get; set; } = 3;
}

public class SuccessMetricsConfiguration
{
    public double TargetSyntaxAccuracy { get; set; } = 0.99; // 99% accuracy
    public double TargetFirstTrySuccess { get; set; } = 0.95; // 95% first-try success
    public int TargetResponseTimeMs { get; set; } = 500; // 500ms average response
    
    /// <summary>
    /// Metrics collection and reporting
    /// </summary>
    public bool EnableMetricsCollection { get; set; } = true;
    public string MetricsStoragePath { get; set; } = "metrics/performance.json";
    public int MetricsRetentionDays { get; set; } = 30;
}

/// <summary>
/// Project-specific configuration
/// </summary>
public class ProjectConfiguration
{
    public string ProjectName { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    
    /// <summary>
    /// Project-specific schemas and patterns
    /// </summary>
    public ProjectSchemaConfiguration Schema { get; set; } = new();
    
    /// <summary>
    /// Business logic and domain-specific rules
    /// </summary>
    public Dictionary<string, object> BusinessRules { get; set; } = new();
}

public class ProjectSchemaConfiguration
{
    public List<string> ImportantTables { get; set; } = new();
    public List<string> ImportantViews { get; set; } = new();
    public List<string> CriticalStoredProcedures { get; set; } = new();
    
    /// <summary>
    /// Naming conventions specific to this project
    /// </summary>
    public Dictionary<string, string> NamingConventions { get; set; } = new();
}

/// <summary>
/// Plugin and extension configuration
/// </summary>
public class PluginConfiguration
{
    /// <summary>
    /// Enabled plugins and their settings
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> EnabledPlugins { get; set; } = new();
    
    /// <summary>
    /// Plugin discovery and loading paths
    /// </summary>
    public List<string> PluginPaths { get; set; } = new() { "plugins/" };

    /// <summary>
    /// Plugin directories for discovery
    /// </summary>
    public List<string> PluginDirectories { get; set; } = new() { "plugins/", "extensions/" };
    
    /// <summary>
    /// Hot-reload configuration for plugins
    /// </summary>
    public bool EnableHotReload { get; set; } = true;
}

/// <summary>
/// Observability: logging, monitoring, and telemetry
/// </summary>
public class ObservabilityConfiguration
{
    /// <summary>
    /// Logging configuration
    /// </summary>
    public LoggingConfiguration Logging { get; set; } = new();
    
    /// <summary>
    /// Performance monitoring
    /// </summary>
    public MonitoringConfiguration Monitoring { get; set; } = new();
    
    /// <summary>
    /// Telemetry and analytics
    /// </summary>
    public TelemetryConfiguration Telemetry { get; set; } = new();
}

public class LoggingConfiguration
{
    public string LogLevel { get; set; } = "Information";
    public string LogPath { get; set; } = "logs/";
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;
    public bool EnableStructuredLogging { get; set; } = true;
    
    /// <summary>
    /// Log retention and rotation
    /// </summary>
    public int RetentionDays { get; set; } = 30;
    public long MaxFileSizeMB { get; set; } = 100;
}

public class MonitoringConfiguration
{
    public bool EnablePerformanceCounters { get; set; } = true;
    public bool EnableHealthChecks { get; set; } = true;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// Alert thresholds
    /// </summary>
    public Dictionary<string, double> AlertThresholds { get; set; } = new()
    {
        ["response_time_ms"] = 1000,
        ["error_rate_percent"] = 5,
        ["memory_usage_percent"] = 80,
        ["cpu_usage_percent"] = 70
    };
}

public class TelemetryConfiguration
{
    public bool EnableTelemetry { get; set; } = true;
    public bool CollectUsageStatistics { get; set; } = true;
    public bool CollectPerformanceMetrics { get; set; } = true;
    public string TelemetryEndpoint { get; set; } = "";
    
    /// <summary>
    /// Privacy and data protection settings
    /// </summary>
    public bool AnonymizeData { get; set; } = true;
    public List<string> ExcludedDataTypes { get; set; } = new()
    {
        "connection_strings", "api_keys", "user_credentials"
    };
}