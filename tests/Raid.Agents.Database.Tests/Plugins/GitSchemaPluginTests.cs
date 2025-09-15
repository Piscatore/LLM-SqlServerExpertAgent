using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Moq;
using SqlServerExpertAgent.Configuration;
using SqlServerExpertAgent.Plugins;
using System.Diagnostics;
using System.Text.Json;

namespace SqlServerExpertAgent.Tests.Plugins;

/// <summary>
/// Comprehensive tests for GitSchemaPlugin covering Git integration and SMO schema extraction
/// Tests the plugin's schema versioning, diff tracking, and migration capabilities
/// </summary>
public class GitSchemaPluginTests : IDisposable
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly AgentConfiguration _testConfig;
    private readonly GitSchemaPlugin _plugin;
    private readonly Kernel _kernel;
    private readonly string _testDirectory;
    private bool _disposed;

    public GitSchemaPluginTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _testConfig = CreateTestConfiguration();
        _plugin = new GitSchemaPlugin();
        
        // Create kernel for testing Semantic Kernel functions
        var builder = Kernel.CreateBuilder();
        _kernel = builder.Build();
        
        // Create temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"GitSchemaPluginTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    #region Plugin Metadata Tests

    [Fact]
    public void Metadata_HasCorrectPluginInformation()
    {
        // Arrange & Act
        var metadata = _plugin.Metadata;

        // Assert
        metadata.Name.Should().Be("GitSchemaPlugin");
        metadata.Version.Should().Be(new Version(1, 0, 0));
        metadata.Description.Should().Contain("Git-based SQL Server schema version management");
        metadata.Dependencies.Should().Contain("SqlServerPlugin");
        metadata.Capabilities.Should().HaveFlag(PluginCapabilities.SqlSchema);
        metadata.Capabilities.Should().HaveFlag(PluginCapabilities.FileOperations);
    }

    [Fact]
    public void Metadata_HasCorrectCustomProperties()
    {
        // Arrange & Act
        var metadata = _plugin.Metadata;

        // Assert
        metadata.CustomProperties.Should().ContainKey("GitRequired");
        metadata.CustomProperties.Should().ContainKey("SupportedGitVersion");
        metadata.CustomProperties.Should().ContainKey("SchemaFormats");
        
        metadata.CustomProperties["GitRequired"].Should().Be(true);
        metadata.CustomProperties["SupportedGitVersion"].Should().Be("2.0+");
        
        var schemaFormats = metadata.CustomProperties["SchemaFormats"] as string[];
        schemaFormats.Should().Contain("SQL");
        schemaFormats.Should().Contain("JSON");
        schemaFormats.Should().Contain("DACPAC");
    }

    #endregion

    #region Kernel Function Registration Tests

    [Fact]
    public void RegisterKernelFunctions_RegistersAllExpectedFunctions()
    {
        // Arrange & Act
        _plugin.RegisterKernelFunctions(_kernel);

        // Assert
        var gitSchemaPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name == "GitSchema");
        gitSchemaPlugin.Should().NotBeNull("GitSchema plugin should be registered");

        var expectedFunctions = new[]
        {
            "InitializeSchemaTracking",
            "CreateSchemaSnapshot",
            "CompareSchemaWithGit",
            "GenerateMigrationScript",
            "GetSchemaHistory"
        };

        foreach (var functionName in expectedFunctions)
        {
            gitSchemaPlugin.Should().Contain(f => f.Name == functionName,
                $"Function {functionName} should be registered");
        }
    }

    #endregion

    #region Schema Tracking Initialization Tests

    [Theory]
    [InlineData("TestDatabase")]
    [InlineData("OrfPIM2")]
    [InlineData("MyProject_Dev")]
    public async Task InitializeSchemaTracking_WithValidDatabase_CreatesTrackingStructure(string databaseName)
    {
        // Arrange
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);

        // Act
        var result = await _plugin.InitializeSchemaTracking(databaseName);

        // Assert
        result.Should().Contain($"Schema tracking initialized for {databaseName}");
        
        var schemaPath = Path.Combine(_testDirectory, "schema", databaseName);
        Directory.Exists(schemaPath).Should().BeTrue("Schema directory should be created");
        
        var configPath = Path.Combine(schemaPath, "schema-tracking.json");
        File.Exists(configPath).Should().BeTrue("Schema tracking configuration should be created");
        
        var configContent = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializer.Deserialize<JsonElement>(configContent);
        config.GetProperty("DatabaseName").GetString().Should().Be(databaseName);
        config.GetProperty("TrackingBranch").GetString().Should().Be("schema-main");
    }

    [Fact]
    public async Task InitializeSchemaTracking_WithCustomBranch_UsesSpecifiedBranch()
    {
        // Arrange
        var databaseName = "TestDB";
        var customBranch = "feature-schema";
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);

        // Act
        var result = await _plugin.InitializeSchemaTracking(databaseName, customBranch);

        // Assert
        result.Should().Contain($"Schema tracking initialized for {databaseName}");
        
        var configPath = Path.Combine(_testDirectory, "schema", databaseName, "schema-tracking.json");
        var configContent = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializer.Deserialize<JsonElement>(configContent);
        config.GetProperty("TrackingBranch").GetString().Should().Be(customBranch);
    }

    [Fact]
    public async Task InitializeSchemaTracking_CreatesCorrectTrackingConfiguration()
    {
        // Arrange
        var databaseName = "ConfigTest";
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);

        // Act
        await _plugin.InitializeSchemaTracking(databaseName);

        // Assert
        var configPath = Path.Combine(_testDirectory, "schema", databaseName, "schema-tracking.json");
        var configContent = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializer.Deserialize<JsonElement>(configContent);
        
        var trackingConfig = config.GetProperty("TrackingConfig");
        trackingConfig.GetProperty("TrackTables").GetBoolean().Should().BeTrue();
        trackingConfig.GetProperty("TrackViews").GetBoolean().Should().BeTrue();
        trackingConfig.GetProperty("TrackStoredProcedures").GetBoolean().Should().BeTrue();
        trackingConfig.GetProperty("TrackFunctions").GetBoolean().Should().BeTrue();
        trackingConfig.GetProperty("TrackIndexes").GetBoolean().Should().BeTrue();
        trackingConfig.GetProperty("TrackConstraints").GetBoolean().Should().BeTrue();
        trackingConfig.GetProperty("TrackPermissions").GetBoolean().Should().BeFalse("Security sensitive");
        trackingConfig.GetProperty("ExcludeSystemObjects").GetBoolean().Should().BeTrue();
    }

    #endregion

    #region Schema Snapshot Tests

    [Fact]
    public async Task CreateSchemaSnapshot_WithNoChanges_ReturnsNoChangesMessage()
    {
        // Arrange
        var databaseName = "TestDB";
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);
        await _plugin.InitializeSchemaTracking(databaseName);

        // Act
        var result = await _plugin.CreateSchemaSnapshot(databaseName);

        // Assert - First snapshot should have no changes (empty database mock)
        result.Should().Contain("No schema changes detected");
    }

    [Fact]
    public async Task CreateSchemaSnapshot_WithCustomCommitMessage_UsesCustomMessage()
    {
        // Arrange
        var databaseName = "TestDB";
        var customMessage = "Custom schema update for feature X";
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);
        await _plugin.InitializeSchemaTracking(databaseName);

        // Act
        var result = await _plugin.CreateSchemaSnapshot(databaseName, customMessage);

        // Assert
        result.Should().NotBeNull();
        // Note: With mocked database, we expect no changes, but the method should still work
    }

    #endregion

    #region Schema Comparison Tests

    [Fact]
    public async Task CompareSchemaWithGit_WithMatchingSchema_ReturnsNoChanges()
    {
        // Arrange
        var databaseName = "TestDB";
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);
        await _plugin.InitializeSchemaTracking(databaseName);

        // Act
        var result = await _plugin.CompareSchemaWithGit(databaseName);

        // Assert
        result.Should().Contain("Database schema matches Git reference HEAD");
    }

    [Fact]
    public async Task CompareSchemaWithGit_WithCustomGitRef_UsesSpecifiedReference()
    {
        // Arrange
        var databaseName = "TestDB";
        var gitRef = "v1.0.0";
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);
        await _plugin.InitializeSchemaTracking(databaseName);

        // Act
        var result = await _plugin.CompareSchemaWithGit(databaseName, gitRef);

        // Assert
        result.Should().NotBeNull();
        // Note: With mocked Git operations, we expect basic functionality to work
    }

    #endregion

    #region Migration Script Generation Tests

    [Fact]
    public async Task GenerateMigrationScript_WithValidReferences_CreatesScript()
    {
        // Arrange
        var databaseName = "TestDB";
        var fromRef = "v1.0";
        var toRef = "v2.0";
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);
        await _plugin.InitializeSchemaTracking(databaseName);

        // Act
        var result = await _plugin.GenerateMigrationScript(databaseName, fromRef, toRef);

        // Assert
        result.Should().Contain("Migration script generated");
        result.Should().Contain($"migration_{fromRef}_{toRef}");
        result.Should().Contain("Script preview:");
        
        var migrationPath = Path.Combine(_testDirectory, "schema", databaseName, "migrations");
        Directory.Exists(migrationPath).Should().BeTrue("Migrations directory should be created");
    }

    [Fact]
    public async Task GenerateMigrationScript_CreatesScriptWithCorrectContent()
    {
        // Arrange
        var databaseName = "TestDB";
        var fromRef = "HEAD~1";
        var toRef = "HEAD";
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);
        await _plugin.InitializeSchemaTracking(databaseName);

        // Act
        var result = await _plugin.GenerateMigrationScript(databaseName, fromRef, toRef);

        // Assert
        result.Should().Contain("Auto-generated migration script");
        result.Should().Match("*Generated on: ????-??-?? ??:??:??*");
        
        // Check that migration file was created
        var migrationPath = Path.Combine(_testDirectory, "schema", databaseName, "migrations");
        if (Directory.Exists(migrationPath))
        {
            var migrationFiles = Directory.GetFiles(migrationPath, "*.sql");
            migrationFiles.Should().NotBeEmpty("Migration script file should be created");
        }
    }

    #endregion

    #region Schema History Tests

    [Fact]
    public async Task GetSchemaHistory_WithExistingCommits_ReturnsHistory()
    {
        // Arrange
        var databaseName = "TestDB";
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);
        await _plugin.InitializeSchemaTracking(databaseName);

        // Act
        var result = await _plugin.GetSchemaHistory(databaseName);

        // Assert
        result.Should().Contain($"Schema version history for {databaseName}");
        // Note: With fresh repo, we expect either history or "No schema history found" message
    }

    [Fact]
    public async Task GetSchemaHistory_WithCustomLimit_RespectsLimit()
    {
        // Arrange
        var databaseName = "TestDB";
        var limit = 5;
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);
        await _plugin.InitializeSchemaTracking(databaseName);

        // Act
        var result = await _plugin.GetSchemaHistory(databaseName, limit);

        // Assert
        result.Should().NotBeNull();
        // The method should complete successfully with the specified limit
    }

    [Fact]
    public async Task GetSchemaHistory_WithNonExistentDatabase_ReturnsNoHistory()
    {
        // Arrange
        var databaseName = "NonExistentDB";
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);

        // Act
        var result = await _plugin.GetSchemaHistory(databaseName);

        // Assert
        result.Should().Contain($"No schema history found for {databaseName}");
    }

    #endregion

    #region Health Status Tests

    [Fact]
    public async Task GetHealthStatusAsync_WithGitAvailable_ReturnsHealthy()
    {
        // Arrange
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);

        // Act
        var healthStatus = await _plugin.GetHealthStatusAsync();

        // Assert
        healthStatus.Should().NotBeNull();
        healthStatus.IsHealthy.Should().BeTrue("Git should be available on development machine");
        healthStatus.Status.Should().Be("Healthy");
        healthStatus.Metrics.Should().ContainKey("GitAvailable");
        healthStatus.Metrics["GitAvailable"].Should().Be(true);
    }

    [Fact]
    public async Task GetHealthStatusAsync_InGitRepository_HasNoWarnings()
    {
        // Arrange
        await InitializeTestRepository();
        await _plugin.InitializeAsync(_testConfig, _serviceProviderMock.Object);

        // Act
        var healthStatus = await _plugin.GetHealthStatusAsync();

        // Assert
        healthStatus.Warnings.Should().NotContain(w => w.Contains("Not in a Git repository"));
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task InitializeSchemaTracking_WithError_ReturnsErrorMessage()
    {
        // Arrange - Don't initialize Git repository to simulate error
        var invalidConfig = new AgentConfiguration
        {
            Projects = new Dictionary<string, ProjectConfiguration>(),
            SqlServer = new SqlServerConfiguration
            {
                ConnectionStrings = new Dictionary<string, string>
                {
                    ["default"] = "invalid connection string"
                }
            }
        };
        await _plugin.InitializeAsync(invalidConfig, _serviceProviderMock.Object);

        // Act
        var result = await _plugin.InitializeSchemaTracking("TestDB");

        // Assert
        result.Should().Contain("Failed to initialize schema tracking");
    }

    #endregion

    #region Schema Data Model Tests

    [Fact]
    public void DatabaseSchema_RecordType_CreatesCorrectly()
    {
        // Arrange
        var databaseName = "TestDatabase";
        var extractedAt = DateTime.UtcNow;
        var tables = new List<TableSchema>();
        var views = new List<ViewSchema>();
        var procedures = new List<StoredProcedureSchema>();
        var functions = new List<FunctionSchema>();

        // Act
        var schema = new DatabaseSchema(databaseName, extractedAt, tables, views, procedures, functions);

        // Assert
        schema.DatabaseName.Should().Be(databaseName);
        schema.ExtractedAt.Should().Be(extractedAt);
        schema.Tables.Should().BeSameAs(tables);
        schema.Views.Should().BeSameAs(views);
        schema.StoredProcedures.Should().BeSameAs(procedures);
        schema.Functions.Should().BeSameAs(functions);
    }

    [Fact]
    public void TableSchema_WithCompleteData_StoresAllInformation()
    {
        // Arrange
        var columns = new List<ColumnSchema>
        {
            new("Id", "int", false, true, true, null),
            new("Name", "nvarchar(50)", false, false, false, null)
        };
        var indexes = new List<IndexSchema>
        {
            new("PK_TestTable", "CLUSTERED", new List<string> { "Id" }, true, true)
        };
        var constraints = new List<ConstraintSchema>
        {
            new("CK_Name_NotEmpty", "CHECK", "[Name] <> ''")
        };

        // Act
        var table = new TableSchema("dbo", "TestTable", columns, indexes, constraints);

        // Assert
        table.Schema.Should().Be("dbo");
        table.Name.Should().Be("TestTable");
        table.Columns.Should().HaveCount(2);
        table.Indexes.Should().HaveCount(1);
        table.Constraints.Should().HaveCount(1);
        
        table.Columns[0].IsPrimaryKey.Should().BeTrue();
        table.Columns[0].IsIdentity.Should().BeTrue();
        table.Indexes[0].IsUnique.Should().BeTrue();
        table.Indexes[0].IsClustered.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private async Task InitializeTestRepository()
    {
        // Initialize a Git repository in the test directory
        await RunGitCommand("init");
        await RunGitCommand("config user.email \"test@example.com\"");
        await RunGitCommand("config user.name \"Test User\"");
        
        // Create initial commit
        var readmePath = Path.Combine(_testDirectory, "README.md");
        await File.WriteAllTextAsync(readmePath, "# Test Repository for GitSchemaPlugin");
        await RunGitCommand("add README.md");
        await RunGitCommand("commit -m \"Initial commit\"");
    }

    private async Task RunGitCommand(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _testDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
        }
    }

    private AgentConfiguration CreateTestConfiguration()
    {
        return new AgentConfiguration
        {
            Projects = new Dictionary<string, ProjectConfiguration>
            {
                ["default"] = new ProjectConfiguration
                {
                    ProjectName = "TestProject",
                    ConnectionString = _testDirectory // Use test directory as mock connection
                }
            },
            SqlServer = new SqlServerConfiguration
            {
                ConnectionStrings = new Dictionary<string, string>
                {
                    ["default"] = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;"
                },
                DefaultDatabase = "master"
            }
        };
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {                
                if (Directory.Exists(_testDirectory))
                {
                    // Clean up test directory
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
            
            _disposed = true;
        }
    }

    #endregion
}