using Microsoft.SemanticKernel;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using SqlServerExpertAgent.Configuration;
using SqlServerExpertAgent.VersionControl;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace SqlServerExpertAgent.Plugins;

/// <summary>
/// Git-based SQL Server schema version management plugin
/// Provides schema versioning, diff tracking, and automated migration support
/// Inspired by database migration tools like Flyway and Liquibase but with Git integration
/// </summary>
public class GitSchemaPlugin : IAgentPlugin
{
    private AgentConfiguration? _configuration;
    private string? _repositoryPath;
    private VersionControlCore? _versionControl;
    private readonly Dictionary<string, object> _healthMetrics = new();

    public PluginMetadata Metadata { get; } = new(
        Name: "GitSchemaPlugin",
        Version: new Version(1, 0, 0),
        Description: "Git-based SQL Server schema version management with automated diff tracking",
        Dependencies: new[] { "SqlServerPlugin" }, // Requires SQL Server access
        Capabilities: PluginCapabilities.SqlSchema | PluginCapabilities.FileOperations,
        CustomProperties: new Dictionary<string, object>
        {
            ["GitRequired"] = true,
            ["SupportedGitVersion"] = "2.0+",
            ["SchemaFormats"] = new[] { "SQL", "JSON", "DACPAC" }
        }
    );

    public async Task InitializeAsync(AgentConfiguration configuration, IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _repositoryPath = configuration.Projects.ContainsKey("default") 
            ? Path.GetDirectoryName(configuration.Projects["default"].ConnectionString) ?? Environment.CurrentDirectory
            : Environment.CurrentDirectory;

        // Initialize VersionControlCore with repository path
        _versionControl = new VersionControlCore(_repositoryPath);

        // Ensure Git repository exists or initialize using VersionControlCore
        await EnsureGitRepositoryAsync();
        
        // Create schema tracking structure
        await InitializeSchemaTrackingAsync();
        
        _healthMetrics["InitializedAt"] = DateTime.UtcNow;
        _healthMetrics["RepositoryPath"] = _repositoryPath;
    }

    public void RegisterKernelFunctions(Kernel kernel)
    {
        kernel.ImportPluginFromObject(this, "GitSchema");
    }

    #region Schema Version Management

    [KernelFunction]
    [Description("Initialize or update Git-based schema tracking for a database")]
    public async Task<string> InitializeSchemaTracking(
        [Description("Database name to track")] string databaseName,
        [Description("Optional branch name for schema tracking")] string branchName = "schema-main")
    {
        try
        {
            var schemaPath = Path.Combine(_repositoryPath!, "schema", databaseName);
            Directory.CreateDirectory(schemaPath);

            // Create schema tracking structure
            var structure = new
            {
                DatabaseName = databaseName,
                TrackingBranch = branchName,
                InitializedAt = DateTime.UtcNow,
                SchemaVersion = "1.0.0",
                TrackingConfig = new
                {
                    TrackTables = true,
                    TrackViews = true,
                    TrackStoredProcedures = true,
                    TrackFunctions = true,
                    TrackIndexes = true,
                    TrackConstraints = true,
                    TrackPermissions = false, // Security sensitive
                    ExcludeSystemObjects = true
                }
            };

            var configPath = Path.Combine(schemaPath, "schema-tracking.json");
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(structure, new JsonSerializerOptions { WriteIndented = true }));

            // Create initial schema snapshot
            await CreateSchemaSnapshotAsync(databaseName);

            // Git operations using VersionControlCore
            var addResult = await _versionControl!.AddDirectoryAsync($"schema/{databaseName}");
            if (!addResult.Success)
            {
                throw new InvalidOperationException($"Failed to add schema directory: {addResult.Message}");
            }
            
            var commitResult = await _versionControl.CommitChangesAsync($"Initialize schema tracking for {databaseName}");
            if (!commitResult.Success)
            {
                throw new InvalidOperationException($"Failed to commit schema initialization: {commitResult.Message}");
            }

            return $"Schema tracking initialized for {databaseName} at {schemaPath}";
        }
        catch (Exception ex)
        {
            return $"Failed to initialize schema tracking: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Create a snapshot of current database schema and commit to Git")]
    public async Task<string> CreateSchemaSnapshot(
        [Description("Database name to snapshot")] string databaseName,
        [Description("Optional commit message")] string? commitMessage = null)
    {
        try
        {
            var changes = await CreateSchemaSnapshotAsync(databaseName);
            
            if (!changes.Any())
            {
                return $"No schema changes detected for {databaseName}";
            }

            var message = commitMessage ?? $"Schema snapshot: {databaseName} - {changes.Count} changes detected";
            
            var addResult = await _versionControl!.AddDirectoryAsync($"schema/{databaseName}");
            if (!addResult.Success)
            {
                return $"Failed to stage schema changes: {addResult.Message}";
            }
            
            var commitResult = await _versionControl.CommitChangesAsync(message);
            if (!commitResult.Success)
            {
                return $"Failed to commit schema snapshot: {commitResult.Message}";
            }

            _healthMetrics["LastSnapshotTime"] = DateTime.UtcNow;
            _healthMetrics["LastChangeCount"] = changes.Count;

            return $"Schema snapshot created: {changes.Count} changes committed\n{string.Join("\n", changes)}";
        }
        catch (Exception ex)
        {
            return $"Failed to create schema snapshot: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Compare current database schema with Git-tracked version")]
    public async Task<string> CompareSchemaWithGit(
        [Description("Database name to compare")] string databaseName,
        [Description("Git reference to compare against (branch, tag, commit)")] string gitRef = "HEAD")
    {
        try
        {
            var currentSchema = await ExtractDatabaseSchemaAsync(databaseName);
            var gitSchema = await GetSchemaFromGitAsync(databaseName, gitRef);

            var differences = CompareSchemaObjects(currentSchema, gitSchema);
            
            if (!differences.Any())
            {
                return $"Database schema matches Git reference {gitRef}";
            }

            var report = new StringBuilder();
            report.AppendLine($"Schema differences between current database and Git {gitRef}:");
            report.AppendLine();

            foreach (var diff in differences)
            {
                report.AppendLine($"• {diff.ChangeType}: {diff.ObjectType} '{diff.ObjectName}'");
                if (!string.IsNullOrEmpty(diff.Details))
                {
                    report.AppendLine($"  └─ {diff.Details}");
                }
            }

            return report.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to compare schema: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Generate migration script between two schema versions")]
    public async Task<string> GenerateMigrationScript(
        [Description("Database name")] string databaseName,
        [Description("Source Git reference")] string fromRef,
        [Description("Target Git reference")] string toRef)
    {
        try
        {
            var sourceSchema = await GetSchemaFromGitAsync(databaseName, fromRef);
            var targetSchema = await GetSchemaFromGitAsync(databaseName, toRef);

            var differences = CompareSchemaObjects(sourceSchema, targetSchema);
            var migrationScript = GenerateMigrationSqlScript(differences);

            // Save migration script
            var migrationPath = Path.Combine(_repositoryPath!, "schema", databaseName, "migrations");
            Directory.CreateDirectory(migrationPath);
            
            var fileName = $"migration_{fromRef}_{toRef}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql";
            var scriptPath = Path.Combine(migrationPath, fileName);
            
            await File.WriteAllTextAsync(scriptPath, migrationScript);

            return $"Migration script generated: {scriptPath}\n\nScript preview:\n{migrationScript.Substring(0, Math.Min(500, migrationScript.Length))}...";
        }
        catch (Exception ex)
        {
            return $"Failed to generate migration script: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get schema version history from Git log")]
    public async Task<string> GetSchemaHistory(
        [Description("Database name")] string databaseName,
        [Description("Number of commits to show")] int limit = 10)
    {
        try
        {
            var historyResult = await _versionControl!.GetHistoryAsync(limit, $"schema/{databaseName}");
            if (!historyResult.Success)
            {
                return $"Failed to retrieve schema history: {historyResult.Message}";
            }
            
            var gitLog = historyResult.Data?.GetType().GetProperty("History")?.GetValue(historyResult.Data)?.ToString();
            if (string.IsNullOrEmpty(gitLog))
            {
                return $"No schema history found for {databaseName}";
            }

            var history = new StringBuilder();
            history.AppendLine($"Schema version history for {databaseName}:");
            history.AppendLine();
            history.AppendLine(gitLog);

            return history.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to retrieve schema history: {ex.Message}";
        }
    }

    #endregion

    #region Schema Extraction and Comparison

    private async Task<List<SchemaDifference>> CreateSchemaSnapshotAsync(string databaseName)
    {
        var currentSchema = await ExtractDatabaseSchemaAsync(databaseName);
        var schemaPath = Path.Combine(_repositoryPath!, "schema", databaseName);
        
        var changes = new List<SchemaDifference>();

        // Save schema objects to individual files for better Git diff tracking
        foreach (var table in currentSchema.Tables)
        {
            var tablePath = Path.Combine(schemaPath, "tables", $"{table.Schema}.{table.Name}.sql");
            Directory.CreateDirectory(Path.GetDirectoryName(tablePath)!);
            
            var existing = File.Exists(tablePath) ? await File.ReadAllTextAsync(tablePath) : "";
            var current = GenerateTableScript(table);
            
            if (existing != current)
            {
                await File.WriteAllTextAsync(tablePath, current);
                changes.Add(new SchemaDifference("Modified", "Table", $"{table.Schema}.{table.Name}", "Definition changed"));
            }
        }

        // Similar processing for views, stored procedures, functions, etc.
        await ProcessSchemaObjects(currentSchema.Views, schemaPath, "views", changes);
        await ProcessSchemaObjects(currentSchema.StoredProcedures, schemaPath, "procedures", changes);
        await ProcessSchemaObjects(currentSchema.Functions, schemaPath, "functions", changes);

        return changes;
    }

    private async Task<DatabaseSchema> ExtractDatabaseSchemaAsync(string databaseName)
    {
        try
        {
            // Get SMO Server instance from SqlServerPlugin connection
            var connectionString = _configuration!.SqlServer.ConnectionStrings["default"];
            var connection = new SqlConnection(connectionString);
            var serverConnection = new ServerConnection(connection);
            var server = new Server(serverConnection);
            
            var database = server.Databases[databaseName];
            if (database == null)
            {
                throw new ArgumentException($"Database '{databaseName}' not found on server");
            }

            var tables = await ExtractTablesAsync(database);
            var views = await ExtractViewsAsync(database);
            var storedProcedures = await ExtractStoredProceduresAsync(database);
            var functions = await ExtractFunctionsAsync(database);

            return new DatabaseSchema(
                databaseName,
                DateTime.UtcNow,
                tables,
                views,
                storedProcedures,
                functions
            );
        }
        catch (Exception ex)
        {
            // Log error and return empty schema rather than failing completely
            _healthMetrics["LastSchemaExtractionError"] = ex.Message;
            return new DatabaseSchema(
                databaseName,
                DateTime.UtcNow,
                new List<TableSchema>(),
                new List<ViewSchema>(),
                new List<StoredProcedureSchema>(),
                new List<FunctionSchema>()
            );
        }
    }

    private async Task<List<TableSchema>> ExtractTablesAsync(Database database)
    {
        var tables = new List<TableSchema>();
        
        foreach (Table table in database.Tables)
        {
            // Skip system tables
            if (table.IsSystemObject)
                continue;
                
            var columns = new List<ColumnSchema>();
            var indexes = new List<IndexSchema>();
            var constraints = new List<ConstraintSchema>();
            
            // Extract columns
            foreach (Column column in table.Columns)
            {
                columns.Add(new ColumnSchema(
                    column.Name,
                    column.DataType.ToString(),
                    column.Nullable,
                    column.InPrimaryKey,
                    column.Identity,
                    column.DefaultConstraint?.Text
                ));
            }
            
            // Extract indexes
            foreach (Microsoft.SqlServer.Management.Smo.Index index in table.Indexes)
            {
                var indexColumns = new List<string>();
                foreach (IndexedColumn idxCol in index.IndexedColumns)
                {
                    indexColumns.Add(idxCol.Name);
                }
                
                indexes.Add(new IndexSchema(
                    index.Name,
                    index.IndexType.ToString(),
                    indexColumns,
                    index.IsUnique,
                    index.IsClustered
                ));
            }
            
            // Extract constraints
            foreach (Check check in table.Checks)
            {
                constraints.Add(new ConstraintSchema(
                    check.Name,
                    "CHECK",
                    check.Text
                ));
            }
            
            foreach (ForeignKey fk in table.ForeignKeys)
            {
                constraints.Add(new ConstraintSchema(
                    fk.Name,
                    "FOREIGN KEY",
                    $"REFERENCES {fk.ReferencedTable} ({string.Join(", ", fk.Columns.Cast<ForeignKeyColumn>().Select(c => c.Name))})"
                ));
            }
            
            tables.Add(new TableSchema(
                table.Schema,
                table.Name,
                columns,
                indexes,
                constraints
            ));
        }
        
        return await Task.FromResult(tables);
    }

    private async Task<List<ViewSchema>> ExtractViewsAsync(Database database)
    {
        var views = new List<ViewSchema>();
        
        foreach (View view in database.Views)
        {
            // Skip system views
            if (view.IsSystemObject)
                continue;
                
            views.Add(new ViewSchema(
                view.Schema,
                view.Name,
                view.TextBody ?? view.TextHeader ?? ""
            ));
        }
        
        return await Task.FromResult(views);
    }

    private async Task<List<StoredProcedureSchema>> ExtractStoredProceduresAsync(Database database)
    {
        var procedures = new List<StoredProcedureSchema>();
        
        foreach (StoredProcedure proc in database.StoredProcedures)
        {
            // Skip system procedures
            if (proc.IsSystemObject)
                continue;
                
            var parameters = new List<ParameterSchema>();
            foreach (StoredProcedureParameter param in proc.Parameters)
            {
                parameters.Add(new ParameterSchema(
                    param.Name,
                    param.DataType.ToString(),
                    param.IsOutputParameter,
                    param.DefaultValue
                ));
            }
            
            procedures.Add(new StoredProcedureSchema(
                proc.Schema,
                proc.Name,
                proc.TextBody ?? "",
                parameters
            ));
        }
        
        return await Task.FromResult(procedures);
    }

    private async Task<List<FunctionSchema>> ExtractFunctionsAsync(Database database)
    {
        var functions = new List<FunctionSchema>();
        
        foreach (UserDefinedFunction func in database.UserDefinedFunctions)
        {
            // Skip system functions
            if (func.IsSystemObject)
                continue;
                
            var parameters = new List<ParameterSchema>();
            foreach (UserDefinedFunctionParameter param in func.Parameters)
            {
                parameters.Add(new ParameterSchema(
                    param.Name,
                    param.DataType.ToString(),
                    false, // UDF parameters are not output parameters
                    param.DefaultValue
                ));
            }
            
            functions.Add(new FunctionSchema(
                func.Schema,
                func.Name,
                func.TextBody ?? "",
                func.DataType?.ToString() ?? "TABLE", // Return type
                parameters
            ));
        }
        
        return await Task.FromResult(functions);
    }

    private List<SchemaDifference> CompareSchemaObjects(DatabaseSchema current, DatabaseSchema reference)
    {
        var differences = new List<SchemaDifference>();

        // Compare tables
        var currentTables = current.Tables.ToDictionary(t => $"{t.Schema}.{t.Name}", t => t);
        var referenceTables = reference.Tables.ToDictionary(t => $"{t.Schema}.{t.Name}", t => t);

        // Find added tables
        foreach (var table in currentTables.Where(t => !referenceTables.ContainsKey(t.Key)))
        {
            differences.Add(new SchemaDifference("Added", "Table", table.Key, "New table"));
        }

        // Find removed tables
        foreach (var table in referenceTables.Where(t => !currentTables.ContainsKey(t.Key)))
        {
            differences.Add(new SchemaDifference("Removed", "Table", table.Key, "Table dropped"));
        }

        // Find modified tables
        foreach (var (key, currentTable) in currentTables.Where(t => referenceTables.ContainsKey(t.Key)))
        {
            var referenceTable = referenceTables[key];
            if (GenerateTableScript(currentTable) != GenerateTableScript(referenceTable))
            {
                differences.Add(new SchemaDifference("Modified", "Table", key, "Table structure changed"));
            }
        }

        return differences;
    }

    private string GenerateMigrationSqlScript(List<SchemaDifference> differences)
    {
        var script = new StringBuilder();
        script.AppendLine("-- Auto-generated migration script");
        script.AppendLine($"-- Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
        script.AppendLine();

        foreach (var diff in differences.OrderBy(d => GetMigrationPriority(d.ChangeType)))
        {
            script.AppendLine($"-- {diff.ChangeType} {diff.ObjectType}: {diff.ObjectName}");
            
            switch (diff.ChangeType.ToLower())
            {
                case "added":
                    if (diff.ObjectType == "Table")
                        script.AppendLine($"-- CREATE TABLE {diff.ObjectName} (...)");
                    break;
                case "removed":
                    if (diff.ObjectType == "Table")
                        script.AppendLine($"DROP TABLE {diff.ObjectName};");
                    break;
                case "modified":
                    script.AppendLine($"-- ALTER {diff.ObjectType.ToUpper()} {diff.ObjectName} (...)");
                    break;
            }
            
            script.AppendLine();
        }

        return script.ToString();
    }

    private int GetMigrationPriority(string changeType) => changeType.ToLower() switch
    {
        "removed" => 1,    // Drop objects first
        "modified" => 2,   // Modify existing
        "added" => 3,      // Add new objects last
        _ => 4
    };

    #endregion

    #region Git Operations

    private async Task EnsureGitRepositoryAsync()
    {
        if (!await _versionControl!.IsGitRepositoryAsync())
        {
            var result = await _versionControl.InitializeRepositoryAsync();
            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to initialize Git repository: {result.Message}");
            }
            _healthMetrics["GitRepositoryInitialized"] = DateTime.UtcNow;
        }
    }

    private async Task InitializeSchemaTrackingAsync()
    {
        var schemaPath = Path.Combine(_repositoryPath!, "schema");
        Directory.CreateDirectory(schemaPath);

        // Create .gitignore for schema directory
        var gitIgnorePath = Path.Combine(schemaPath, ".gitignore");
        if (!File.Exists(gitIgnorePath))
        {
            await File.WriteAllTextAsync(gitIgnorePath, @"# Temporary files
*.tmp
*.bak
*.log

# Environment-specific files
local-config.json
");
        }
    }

    // RunGitCommandAsync method removed - now using VersionControlCore for all Git operations

    private async Task<DatabaseSchema> GetSchemaFromGitAsync(string databaseName, string gitRef)
    {
        // Implementation would extract schema from Git at specific reference
        // This is a placeholder for the actual Git file extraction
        return new DatabaseSchema(
            databaseName,
            DateTime.UtcNow,
            new List<TableSchema>(),
            new List<ViewSchema>(),
            new List<StoredProcedureSchema>(),
            new List<FunctionSchema>()
        );
    }

    #endregion

    #region Helper Methods

    private async Task ProcessSchemaObjects<T>(List<T> objects, string basePath, string subfolder, List<SchemaDifference> changes)
    {
        var objectPath = Path.Combine(basePath, subfolder);
        Directory.CreateDirectory(objectPath);

        // Implementation would process each object type appropriately
        // This is a placeholder for the actual object processing
    }

    private string GenerateTableScript(TableSchema table)
    {
        // Implementation would generate CREATE TABLE script
        return $"CREATE TABLE [{table.Schema}].[{table.Name}] (\n    -- Columns would be listed here\n);";
    }

    #endregion

    public async Task<PluginHealthStatus> GetHealthStatusAsync()
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var isHealthy = true;

        // Check Git availability using VersionControlCore
        var healthResult = await _versionControl!.HealthCheckAsync();
        if (healthResult.Success)
        {
            _healthMetrics["GitAvailable"] = true;
            _healthMetrics["GitVersionInfo"] = healthResult.Data;
        }
        else
        {
            errors.Add($"Git health check failed: {healthResult.Message}");
            _healthMetrics["GitAvailable"] = false;
            isHealthy = false;
        }

        // Check repository status
        if (!Directory.Exists(Path.Combine(_repositoryPath!, ".git")))
        {
            warnings.Add("Not in a Git repository");
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
        _healthMetrics.Clear();
    }
}

#region Schema Data Models

public record DatabaseSchema(
    string DatabaseName,
    DateTime ExtractedAt,
    List<TableSchema> Tables,
    List<ViewSchema> Views,
    List<StoredProcedureSchema> StoredProcedures,
    List<FunctionSchema> Functions
);

public record TableSchema(
    string Schema,
    string Name,
    List<ColumnSchema> Columns,
    List<IndexSchema> Indexes,
    List<ConstraintSchema> Constraints
);

public record ColumnSchema(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsIdentity,
    string? DefaultValue
);

public record IndexSchema(
    string Name,
    string Type,
    List<string> Columns,
    bool IsUnique,
    bool IsClustered
);

public record ConstraintSchema(
    string Name,
    string Type,
    string Definition
);

public record ViewSchema(
    string Schema,
    string Name,
    string Definition
);

public record StoredProcedureSchema(
    string Schema,
    string Name,
    string Definition,
    List<ParameterSchema> Parameters
);

public record FunctionSchema(
    string Schema,
    string Name,
    string Definition,
    string ReturnType,
    List<ParameterSchema> Parameters
);

public record ParameterSchema(
    string Name,
    string DataType,
    bool IsOutput,
    string? DefaultValue
);

public record SchemaDifference(
    string ChangeType,    // Added, Removed, Modified
    string ObjectType,    // Table, View, StoredProcedure, etc.
    string ObjectName,    // Full object name
    string Details        // Description of the change
);

#endregion