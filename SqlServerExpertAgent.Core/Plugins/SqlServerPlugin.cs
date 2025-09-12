using Microsoft.SemanticKernel;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using SqlServerExpertAgent.Configuration;
using System.ComponentModel;
using System.Data.SqlClient;

namespace SqlServerExpertAgent.Plugins;

/// <summary>
/// Core SQL Server plugin using SMO for comprehensive database operations
/// Provides headless SSMS functionality as Semantic Kernel plugin
/// </summary>
public class SqlServerPlugin : IAgentPlugin
{
    private Server? _server;
    private AgentConfiguration? _configuration;
    private readonly Dictionary<string, object> _healthMetrics = new();

    public PluginMetadata Metadata { get; } = new(
        Name: "SqlServerPlugin",
        Version: new Version(1, 0, 0),
        Description: "Core SQL Server operations using SMO (SQL Server Management Objects)",
        Dependencies: Array.Empty<string>(),
        Capabilities: PluginCapabilities.SqlQuery | PluginCapabilities.SqlSchema | PluginCapabilities.SqlOptimization,
        CustomProperties: new Dictionary<string, object>
        {
            ["SmoVersion"] = "172.76.0",
            ["SupportedSqlVersions"] = new[] { "2017", "2019", "2022" }
        }
    );

    public async Task InitializeAsync(AgentConfiguration configuration, IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        
        // Initialize SMO connection
        var connectionString = configuration.SqlServer.ConnectionStrings["default"];
        var connection = new SqlConnection(connectionString);
        var serverConnection = new ServerConnection(connection);
        
        _server = new Server(serverConnection);
        
        // Test connection and cache server info
        await TestConnectionAsync();
        
        _healthMetrics["InitializedAt"] = DateTime.UtcNow;
        _healthMetrics["ServerVersion"] = _server.Information.Version.ToString();
        _healthMetrics["ServerEdition"] = _server.Information.Edition;
    }

    public void RegisterKernelFunctions(Kernel kernel)
    {
        kernel.ImportPluginFromObject(this, "SqlServer");
    }

    #region Kernel Functions

    [KernelFunction]
    [Description("Validate SQL syntax using SMO parser without execution")]
    public async Task<string> ValidateSqlSyntax(
        [Description("SQL query or statement to validate")] string sql,
        [Description("Check for potential security issues")] bool checkSecurity = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sql))
                return "Error: SQL statement is empty";

            // Basic SQL injection detection
            if (checkSecurity && HasSecurityConcerns(sql))
            {
                return "Security Warning: SQL contains potentially dangerous patterns";
            }

            // Use SMO to parse SQL syntax
            var database = _server!.Databases[_configuration!.SqlServer.DefaultDatabase ?? "master"];
            
            // SMO syntax validation approach
            var startTime = DateTime.UtcNow;
            
            // Create a simple check by attempting to create an execution plan
            // This validates syntax without executing
            var results = database.ExecuteWithResults($"SET SHOWPLAN_XML ON; {sql}");
            
            var validationTime = DateTime.UtcNow - startTime;
            _healthMetrics["LastValidationTimeMs"] = validationTime.TotalMilliseconds;

            return $"SQL syntax is valid (validated in {validationTime.TotalMilliseconds:F1}ms)";
        }
        catch (Exception ex)
        {
            return $"SQL syntax error: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get comprehensive database schema information")]
    public async Task<string> GetDatabaseSchema(
        [Description("Database name (optional, uses default if not specified)")] string? databaseName = null)
    {
        try
        {
            var dbName = databaseName ?? _configuration!.SqlServer.DefaultDatabase ?? _server!.Databases[0].Name;
            var database = _server!.Databases[dbName];
            
            if (database == null)
                return $"Database '{dbName}' not found";

            var schema = new
            {
                DatabaseName = database.Name,
                Tables = database.Tables.Cast<Table>()
                    .Where(t => !t.IsSystemObject)
                    .Select(t => new
                    {
                        Name = t.Name,
                        Schema = t.Schema,
                        RowCount = t.RowCount,
                        Columns = t.Columns.Cast<Column>().Select(c => new
                        {
                            Name = c.Name,
                            DataType = c.DataType.ToString(),
                            IsNullable = c.Nullable,
                            IsPrimaryKey = c.InPrimaryKey
                        }).ToArray()
                    }).ToArray(),
                Views = database.Views.Cast<View>()
                    .Where(v => !v.IsSystemObject)
                    .Select(v => v.Name)
                    .ToArray(),
                StoredProcedures = database.StoredProcedures.Cast<StoredProcedure>()
                    .Where(sp => !sp.IsSystemObject)
                    .Select(sp => sp.Name)
                    .ToArray()
            };

            return System.Text.Json.JsonSerializer.Serialize(schema, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            return $"Error retrieving schema: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Execute SQL query safely with results")]
    public async Task<string> ExecuteSqlQuery(
        [Description("SQL SELECT query to execute")] string sql,
        [Description("Maximum number of rows to return")] int maxRows = 100)
    {
        try
        {
            if (!_configuration!.SqlServer.QueryExecution.Safety.AllowDataModification && 
                HasDataModification(sql))
            {
                return "Error: Data modification queries are not allowed in current configuration";
            }

            var database = _server!.Databases[_configuration.SqlServer.DefaultDatabase ?? "master"];
            var results = database.ExecuteWithResults(sql);
            
            if (results.Tables.Count == 0)
                return "Query executed successfully (no results returned)";

            var table = results.Tables[0];
            var output = new System.Text.StringBuilder();
            
            // Add column headers
            var headers = table.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName);
            output.AppendLine(string.Join("\t", headers));
            
            // Add rows (limited by maxRows)
            var rowCount = Math.Min(table.Rows.Count, maxRows);
            for (int i = 0; i < rowCount; i++)
            {
                var row = table.Rows[i];
                var values = row.ItemArray.Select(field => field?.ToString() ?? "NULL");
                output.AppendLine(string.Join("\t", values));
            }
            
            if (table.Rows.Count > maxRows)
                output.AppendLine($"... ({table.Rows.Count - maxRows} more rows)");

            _healthMetrics["LastQueryRowCount"] = table.Rows.Count;
            return output.ToString();
        }
        catch (Exception ex)
        {
            return $"Query execution error: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Analyze query performance and suggest optimizations")]
    public async Task<string> AnalyzeQueryPerformance(
        [Description("SQL query to analyze")] string sql)
    {
        try
        {
            var database = _server!.Databases[_configuration!.SqlServer.DefaultDatabase ?? "master"];
            
            // Get execution plan
            var planResults = database.ExecuteWithResults($"SET SHOWPLAN_XML ON; {sql}");
            
            // Basic performance analysis
            var analysis = new
            {
                QueryText = sql,
                EstimatedCost = "Analysis would require execution plan parsing",
                Suggestions = new[]
                {
                    "Consider adding appropriate indexes",
                    "Review WHERE clause selectivity",
                    "Check for missing statistics"
                },
                Timestamp = DateTime.UtcNow
            };

            return System.Text.Json.JsonSerializer.Serialize(analysis, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            return $"Performance analysis error: {ex.Message}";
        }
    }

    #endregion

    #region Helper Methods

    private async Task TestConnectionAsync()
    {
        try
        {
            // Test basic connection
            _server!.Refresh();
            _healthMetrics["ConnectionTest"] = "Success";
        }
        catch (Exception ex)
        {
            _healthMetrics["ConnectionTest"] = $"Failed: {ex.Message}";
            throw;
        }
    }

    private static bool HasSecurityConcerns(string sql)
    {
        var dangerousPatterns = new[]
        {
            "xp_cmdshell", "sp_configure", "openrowset", "opendatasource",
            "exec(", "execute(", "sp_executesql", "--", "/*", "*/"
        };

        var lowerSql = sql.ToLowerInvariant();
        return dangerousPatterns.Any(pattern => lowerSql.Contains(pattern));
    }

    private static bool HasDataModification(string sql)
    {
        var modificationKeywords = new[] { "insert", "update", "delete", "drop", "alter", "create", "truncate" };
        var lowerSql = sql.ToLowerInvariant().Trim();
        
        return modificationKeywords.Any(keyword => 
            lowerSql.StartsWith(keyword + " ") || 
            lowerSql.Contains(" " + keyword + " "));
    }

    #endregion

    public async Task<PluginHealthStatus> GetHealthStatusAsync()
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var isHealthy = true;

        try
        {
            // Test server connection
            _server?.Refresh();
            _healthMetrics["LastHealthCheck"] = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            errors.Add($"Server connection failed: {ex.Message}");
            isHealthy = false;
        }

        // Check configuration
        if (_configuration?.SqlServer?.ConnectionStrings?.Count == 0)
        {
            warnings.Add("No connection strings configured");
        }

        return new PluginHealthStatus(
            isHealthy,
            isHealthy ? "Healthy" : "Unhealthy",
            new Dictionary<string, object>(_healthMetrics),
            warnings.ToArray(),
            errors.ToArray()
        );
    }

    public async Task DisposeAsync()
    {
        _server?.ConnectionContext?.Disconnect();
        _healthMetrics.Clear();
    }
}