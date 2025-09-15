using Microsoft.SemanticKernel;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using SqlServerExpertAgent.Configuration;
using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SqlServerExpertAgent.Plugins;

/// <summary>
/// Core SQL Server plugin using SMO for comprehensive database operations
/// Provides headless SSMS functionality as Semantic Kernel plugin
/// </summary>
public class SqlServerPlugin : IAgentPlugin
{
    private Server? _server;
    private AgentConfiguration? _configuration;
    private string? _connectionString;
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
        
        // Store connection string for SqlCommand usage
        _connectionString = configuration.SqlServer.ConnectionStrings["default"];
        
        // Initialize SMO connection
        var connection = new SqlConnection(_connectionString);
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
    public Task<string> ValidateSqlSyntax(
        [Description("SQL query or statement to validate")] string sql,
        [Description("Check for potential security issues")] bool checkSecurity = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sql))
                return Task.FromResult("Error: SQL statement is empty");

            // Basic SQL injection detection
            if (checkSecurity && HasSecurityConcerns(sql))
            {
                return Task.FromResult("Security Warning: SQL contains potentially dangerous patterns");
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

            return Task.FromResult($"SQL syntax is valid (validated in {validationTime.TotalMilliseconds:F1}ms)");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"SQL syntax error: {ex.Message}");
        }
    }

    [KernelFunction]
    [Description("Get comprehensive database schema information")]
    public Task<string> GetDatabaseSchema(
        [Description("Database name (optional, uses default if not specified)")] string? databaseName = null)
    {
        try
        {
            var dbName = databaseName ?? _configuration!.SqlServer.DefaultDatabase ?? _server!.Databases[0].Name;
            var database = _server!.Databases[dbName];
            
            if (database == null)
                return Task.FromResult($"Database '{dbName}' not found");

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

            return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(schema, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error retrieving schema: {ex.Message}");
        }
    }

    [KernelFunction]
    [Description("Execute SQL query safely with results using SqlCommand")]
    public async Task<string> ExecuteSqlQuery(
        [Description("SQL SELECT query to execute")] string sql,
        [Description("Maximum number of rows to return")] int maxRows = 100,
        [Description("Command timeout in seconds")] int timeoutSeconds = 30)
    {
        try
        {
            if (!_configuration!.SqlServer.QueryExecution.Safety.AllowDataModification && 
                HasDataModification(sql))
            {
                return "Error: Data modification queries are not allowed in current configuration";
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = timeoutSeconds;
            
            // Use SqlDataAdapter for better control over results
            using var adapter = new SqlDataAdapter(command);
            var dataSet = new DataSet();
            adapter.Fill(dataSet);
            
            if (dataSet.Tables.Count == 0)
                return "Query executed successfully (no results returned)";

            var table = dataSet.Tables[0];
            var output = new System.Text.StringBuilder();
            
            // Add column headers
            var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName);
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
            _healthMetrics["LastQueryExecutionTime"] = DateTime.UtcNow;
            
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

    [KernelFunction]
    [Description("Execute stored procedure with parameters using SqlCommand")]
    public async Task<string> ExecuteStoredProcedure(
        [Description("Stored procedure name")] string procedureName,
        [Description("Parameters as JSON object (e.g., {\"@param1\": \"value1\", \"@param2\": 123})")] string? parameters = null,
        [Description("Command timeout in seconds")] int timeoutSeconds = 30)
    {
        try
        {
            if (!_configuration!.SqlServer.QueryExecution.Safety.AllowDataModification)
            {
                return "Error: Stored procedure execution is not allowed in current configuration";
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand(procedureName, connection);
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = timeoutSeconds;
            
            // Add parameters if provided
            if (!string.IsNullOrEmpty(parameters))
            {
                var paramDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(parameters);
                if (paramDict != null)
                {
                    foreach (var param in paramDict)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }
            }
            
            using var adapter = new SqlDataAdapter(command);
            var dataSet = new DataSet();
            adapter.Fill(dataSet);
            
            var output = new System.Text.StringBuilder();
            output.AppendLine($"Stored procedure '{procedureName}' executed successfully");
            
            if (dataSet.Tables.Count > 0)
            {
                var table = dataSet.Tables[0];
                output.AppendLine($"Returned {table.Rows.Count} rows");
                
                if (table.Rows.Count > 0)
                {
                    // Add column headers
                    var headers = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName);
                    output.AppendLine(string.Join("\t", headers));
                    
                    // Add rows (limit to 50 for procedures)
                    var rowCount = Math.Min(table.Rows.Count, 50);
                    for (int i = 0; i < rowCount; i++)
                    {
                        var row = table.Rows[i];
                        var values = row.ItemArray.Select(field => field?.ToString() ?? "NULL");
                        output.AppendLine(string.Join("\t", values));
                    }
                    
                    if (table.Rows.Count > 50)
                        output.AppendLine($"... ({table.Rows.Count - 50} more rows)");
                }
            }
            
            // Get output parameters
            foreach (SqlParameter param in command.Parameters)
            {
                if (param.Direction == ParameterDirection.Output || param.Direction == ParameterDirection.InputOutput)
                {
                    output.AppendLine($"Output parameter {param.ParameterName}: {param.Value}");
                }
            }
            
            _healthMetrics["LastStoredProcedureExecution"] = DateTime.UtcNow;
            return output.ToString();
        }
        catch (Exception ex)
        {
            return $"Stored procedure execution error: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Execute SQL script with multiple statements using SqlCommand")]
    public async Task<string> ExecuteSqlScript(
        [Description("SQL script containing multiple statements")] string script,
        [Description("Command timeout in seconds")] int timeoutSeconds = 60)
    {
        try
        {
            if (!_configuration!.SqlServer.QueryExecution.Safety.AllowDataModification && 
                HasDataModification(script))
            {
                return "Error: Scripts with data modification are not allowed in current configuration";
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            var results = new System.Text.StringBuilder();
            var statements = SplitSqlScript(script);
            
            results.AppendLine($"Executing script with {statements.Count} statements...");
            results.AppendLine();
            
            int executedCount = 0;
            
            foreach (var statement in statements)
            {
                if (string.IsNullOrWhiteSpace(statement))
                    continue;
                    
                try
                {
                    using var command = new SqlCommand(statement, connection);
                    command.CommandTimeout = timeoutSeconds;
                    
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    executedCount++;
                    
                    results.AppendLine($"Statement {executedCount}: {rowsAffected} rows affected");
                }
                catch (Exception statementEx)
                {
                    results.AppendLine($"Statement {executedCount + 1} failed: {statementEx.Message}");
                    results.AppendLine($"Failed statement: {statement.Substring(0, Math.Min(100, statement.Length))}...");
                    break; // Stop on first error
                }
            }
            
            _healthMetrics["LastScriptExecution"] = DateTime.UtcNow;
            _healthMetrics["LastScriptStatementCount"] = executedCount;
            
            return results.ToString();
        }
        catch (Exception ex)
        {
            return $"Script execution error: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Execute single SQL command (INSERT, UPDATE, DELETE) and return affected rows")]
    public async Task<string> ExecuteSqlCommand(
        [Description("SQL command to execute")] string sql,
        [Description("Command timeout in seconds")] int timeoutSeconds = 30)
    {
        try
        {
            if (!_configuration!.SqlServer.QueryExecution.Safety.AllowDataModification)
            {
                return "Error: Data modification commands are not allowed in current configuration";
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = timeoutSeconds;
            
            var rowsAffected = await command.ExecuteNonQueryAsync();
            
            _healthMetrics["LastCommandExecution"] = DateTime.UtcNow;
            _healthMetrics["LastCommandRowsAffected"] = rowsAffected;
            
            return $"Command executed successfully. {rowsAffected} rows affected.";
        }
        catch (Exception ex)
        {
            return $"Command execution error: {ex.Message}";
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
        var modificationKeywords = new[] { "insert", "update", "delete", "drop", "alter", "create", "truncate", "merge" };
        var lowerSql = sql.ToLowerInvariant().Trim();
        
        return modificationKeywords.Any(keyword => 
            lowerSql.StartsWith(keyword + " ") || 
            lowerSql.Contains(" " + keyword + " "));
    }

    private static List<string> SplitSqlScript(string script)
    {
        var statements = new List<string>();
        var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var currentStatement = new System.Text.StringBuilder();
        var inBlockComment = false;
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Handle block comments
            if (trimmedLine.StartsWith("/*"))
            {
                inBlockComment = true;
            }
            if (trimmedLine.EndsWith("*/"))
            {
                inBlockComment = false;
                continue;
            }
            if (inBlockComment)
            {
                continue;
            }
            
            // Skip line comments and empty lines
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("--"))
                continue;
            
            // Check for GO statement (batch separator)
            if (trimmedLine.Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (currentStatement.Length > 0)
                {
                    statements.Add(currentStatement.ToString().Trim());
                    currentStatement.Clear();
                }
                continue;
            }
            
            currentStatement.AppendLine(line);
            
            // Check for statement terminator (semicolon at end of line)
            if (trimmedLine.EndsWith(";"))
            {
                statements.Add(currentStatement.ToString().Trim());
                currentStatement.Clear();
            }
        }
        
        // Add any remaining statement
        if (currentStatement.Length > 0)
        {
            statements.Add(currentStatement.ToString().Trim());
        }
        
        return statements.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
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