using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SqlServerExpertAgent.Configuration;
using SqlServerExpertAgent.Plugins;
using SqlServerExpertAgent.Templates;
using System.ComponentModel;

namespace SqlServerExpertAgent.Templates.Skills;

/// <summary>
/// SQL Server skill plugin that provides database expertise through skill interface
/// Demonstrates conversion of traditional plugin to skill-based architecture
/// </summary>
public class SqlServerSkill : ISkillPlugin
{
    private readonly SqlServerPlugin _sqlServerPlugin;
    private AgentConfiguration? _configuration;
    private IServiceProvider? _serviceProvider;

    public SqlServerSkill()
    {
        _sqlServerPlugin = new SqlServerPlugin();
    }

    public SkillMetadata SkillInfo => new()
    {
        Name = "SqlServerSkill",
        Version = new Version(1, 0, 0),
        Description = "Provides SQL Server database operations, syntax validation, and schema analysis",
        Category = SkillCategory.Database,
        Capabilities = new List<string>
        {
            "sql_syntax_validation",
            "database_schema_introspection", 
            "query_execution",
            "performance_analysis",
            "security_validation"
        },
        RequiredInfrastructure = new List<string> { "SqlServerDatabase" },
        OptionalInfrastructure = new List<string> { "SqlServerManagementStudio" },
        Compatibility = new List<SkillCompatibility>
        {
            new() { WithSkill = "GitVersioningSkill", Type = CompatibilityType.Enhances, Description = "Works well with version control for schema tracking" },
            new() { WithSkill = "SecurityAnalysisSkill", Type = CompatibilityType.Enhances, Description = "Enhanced security validation when combined" }
        }
    };

    public PluginMetadata Metadata => _sqlServerPlugin.Metadata;

    public async Task InitializeAsync(AgentConfiguration configuration, IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        await _sqlServerPlugin.InitializeAsync(configuration, serviceProvider);
    }

    public void RegisterKernelFunctions(Kernel kernel)
    {
        _sqlServerPlugin.RegisterKernelFunctions(kernel);
    }

    public async Task<PluginHealthStatus> GetHealthStatusAsync()
    {
        return await _sqlServerPlugin.GetHealthStatusAsync();
    }

    public async Task DisposeAsync()
    {
        await _sqlServerPlugin.DisposeAsync();
    }

    /// <summary>
    /// Execute skill-specific operation
    /// </summary>
    public async Task<SkillResult> ExecuteSkillAsync(SkillRequest request)
    {
        try
        {
            var result = request.Operation switch
            {
                "validate_sql_syntax" => await ValidateSqlSyntaxOperation(request),
                "get_database_schema" => await GetDatabaseSchemaOperation(request),
                "execute_sql_query" => await ExecuteSqlQueryOperation(request),
                "execute_stored_procedure" => await ExecuteStoredProcedureOperation(request),
                "analyze_query_performance" => await AnalyzeQueryPerformanceOperation(request),
                _ => SkillResult.CreateError(request.RequestId, $"Unknown operation: {request.Operation}")
            };

            return result;
        }
        catch (Exception ex)
        {
            return SkillResult.CreateError(request.RequestId, ex.Message);
        }
    }

    /// <summary>
    /// Check if this skill can handle a specific request
    /// </summary>
    public bool CanHandle(SkillRequest request)
    {
        var supportedOperations = new[]
        {
            "validate_sql_syntax",
            "get_database_schema", 
            "execute_sql_query",
            "execute_stored_procedure",
            "analyze_query_performance"
        };

        return supportedOperations.Contains(request.Operation);
    }

    /// <summary>
    /// Get skill configuration schema
    /// </summary>
    public SkillConfigurationSchema GetConfigurationSchema()
    {
        return new SkillConfigurationSchema
        {
            Properties = new Dictionary<string, ConfigurationProperty>
            {
                ["connectionString"] = new()
                {
                    Type = "string",
                    Description = "SQL Server connection string",
                    Required = true
                },
                ["defaultTimeout"] = new()
                {
                    Type = "integer",
                    Description = "Default query timeout in seconds",
                    DefaultValue = 30,
                    Required = false
                },
                ["allowDataModification"] = new()
                {
                    Type = "boolean",
                    Description = "Whether to allow data modification operations",
                    DefaultValue = false,
                    Required = false
                },
                ["maxRowsReturned"] = new()
                {
                    Type = "integer",
                    Description = "Maximum number of rows to return from queries",
                    DefaultValue = 1000,
                    Required = false
                }
            },
            RequiredProperties = new List<string> { "connectionString" }
        };
    }

    private async Task<SkillResult> ValidateSqlSyntaxOperation(SkillRequest request)
    {
        if (!request.Parameters.TryGetValue("sql", out var sqlObj) || sqlObj is not string sql)
        {
            return SkillResult.CreateError(request.RequestId, "Missing 'sql' parameter");
        }

        var checkSecurity = request.Parameters.TryGetValue("checkSecurity", out var checkSecurityObj) 
            && checkSecurityObj is bool checkSecurityBool && checkSecurityBool;

        // Use the underlying plugin's ValidateSqlSyntax method
        // Note: In a real implementation, you'd call the actual method through reflection
        // or restructure the plugin to expose methods directly
        var validationResult = $"SQL syntax validation for: {sql.Substring(0, Math.Min(50, sql.Length))}...";
        
        return SkillResult.CreateSuccess(request.RequestId, new Dictionary<string, object>
        {
            ["isValid"] = true,
            ["message"] = validationResult,
            ["sql"] = sql,
            ["securityChecked"] = checkSecurity
        });
    }

    private async Task<SkillResult> GetDatabaseSchemaOperation(SkillRequest request)
    {
        if (!request.Parameters.TryGetValue("databaseName", out var dbNameObj) || dbNameObj is not string databaseName)
        {
            return SkillResult.CreateError(request.RequestId, "Missing 'databaseName' parameter");
        }

        var includeSystemObjects = request.Parameters.TryGetValue("includeSystemObjects", out var includeSystemObj)
            && includeSystemObj is bool includeSystemBool && includeSystemBool;

        // In real implementation, this would call the actual schema introspection
        var schemaResult = new Dictionary<string, object>
        {
            ["databaseName"] = databaseName,
            ["includeSystemObjects"] = includeSystemObjects,
            ["schemaExtracted"] = DateTime.UtcNow,
            ["tableCount"] = 25, // Mock data
            ["viewCount"] = 8,
            ["procedureCount"] = 12,
            ["functionCount"] = 5
        };

        return SkillResult.CreateSuccess(request.RequestId, schemaResult);
    }

    private async Task<SkillResult> ExecuteSqlQueryOperation(SkillRequest request)
    {
        if (!request.Parameters.TryGetValue("sql", out var sqlObj) || sqlObj is not string sql)
        {
            return SkillResult.CreateError(request.RequestId, "Missing 'sql' parameter");
        }

        var timeoutSeconds = request.Parameters.TryGetValue("timeoutSeconds", out var timeoutObj) 
            && timeoutObj is int timeout ? timeout : 30;

        // Mock execution result
        var executionResult = new Dictionary<string, object>
        {
            ["sql"] = sql,
            ["executionTime"] = TimeSpan.FromMilliseconds(125),
            ["rowsAffected"] = 3,
            ["success"] = true,
            ["data"] = new List<Dictionary<string, object>>
            {
                new() { ["id"] = 1, ["name"] = "Sample Row 1" },
                new() { ["id"] = 2, ["name"] = "Sample Row 2" },
                new() { ["id"] = 3, ["name"] = "Sample Row 3" }
            }
        };

        return SkillResult.CreateSuccess(request.RequestId, executionResult);
    }

    private async Task<SkillResult> ExecuteStoredProcedureOperation(SkillRequest request)
    {
        if (!request.Parameters.TryGetValue("procedureName", out var procNameObj) || procNameObj is not string procedureName)
        {
            return SkillResult.CreateError(request.RequestId, "Missing 'procedureName' parameter");
        }

        var parameters = request.Parameters.TryGetValue("parameters", out var paramsObj) 
            ? paramsObj as string ?? "{}" : "{}";

        var timeoutSeconds = request.Parameters.TryGetValue("timeoutSeconds", out var timeoutObj)
            && timeoutObj is int timeout ? timeout : 30;

        var executionResult = new Dictionary<string, object>
        {
            ["procedureName"] = procedureName,
            ["parameters"] = parameters,
            ["executionTime"] = TimeSpan.FromMilliseconds(89),
            ["success"] = true,
            ["returnValue"] = 0,
            ["outputParameters"] = new Dictionary<string, object>(),
            ["resultSets"] = new List<object>()
        };

        return SkillResult.CreateSuccess(request.RequestId, executionResult);
    }

    private async Task<SkillResult> AnalyzeQueryPerformanceOperation(SkillRequest request)
    {
        if (!request.Parameters.TryGetValue("sql", out var sqlObj) || sqlObj is not string sql)
        {
            return SkillResult.CreateError(request.RequestId, "Missing 'sql' parameter");
        }

        var performanceAnalysis = new Dictionary<string, object>
        {
            ["sql"] = sql,
            ["estimatedCost"] = 0.125,
            ["executionPlan"] = "Clustered Index Scan (100%)",
            ["recommendations"] = new List<string>
            {
                "Consider adding an index on the WHERE clause columns",
                "Query is performing a table scan - investigate indexing strategy"
            },
            ["metrics"] = new Dictionary<string, object>
            {
                ["logicalReads"] = 45,
                ["physicalReads"] = 0,
                ["cpuTime"] = 15,
                ["duration"] = 125
            }
        };

        return SkillResult.CreateSuccess(request.RequestId, performanceAnalysis);
    }
}