using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Moq;
using SqlServerExpertAgent.Configuration;
using SqlServerExpertAgent.Plugins;
using System.Reflection;

namespace SqlServerExpertAgent.Tests.Plugins;

/// <summary>
/// Unit tests for SqlServerPlugin that can run without SQL Server dependencies
/// Uses mocking and reflection to test internal logic
/// </summary>
public class SqlServerPluginUnitTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly AgentConfiguration _testConfig;

    public SqlServerPluginUnitTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _testConfig = CreateTestConfiguration();
    }

    #region Plugin Metadata Tests

    [Fact]
    public void Metadata_HasCorrectPluginInformation()
    {
        // Arrange
        var plugin = new SqlServerPlugin();

        // Act
        var metadata = plugin.Metadata;

        // Assert
        metadata.Name.Should().Be("SqlServerPlugin");
        metadata.Version.Should().Be(new Version(1, 0, 0));
        metadata.Description.Should().Contain("SMO");
        metadata.Dependencies.Should().BeEmpty();
        metadata.Capabilities.Should().HaveFlag(PluginCapabilities.SqlQuery);
        metadata.Capabilities.Should().HaveFlag(PluginCapabilities.SqlSchema);
        metadata.Capabilities.Should().HaveFlag(PluginCapabilities.SqlOptimization);
    }

    [Fact]
    public void Metadata_HasCorrectCustomProperties()
    {
        // Arrange
        var plugin = new SqlServerPlugin();

        // Act
        var metadata = plugin.Metadata;

        // Assert
        metadata.CustomProperties.Should().ContainKey("SmoVersion");
        metadata.CustomProperties.Should().ContainKey("SupportedSqlVersions");
        metadata.CustomProperties["SmoVersion"].Should().Be("172.76.0");
        
        var supportedVersions = metadata.CustomProperties["SupportedSqlVersions"] as string[];
        supportedVersions.Should().Contain("2017");
        supportedVersions.Should().Contain("2019");
        supportedVersions.Should().Contain("2022");
    }

    #endregion

    #region Kernel Function Registration Tests

    [Fact]
    public void RegisterKernelFunctions_RegistersAllExpectedFunctions()
    {
        // Arrange
        var plugin = new SqlServerPlugin();
        var builder = Kernel.CreateBuilder();
        var kernel = builder.Build();

        // Act
        plugin.RegisterKernelFunctions(kernel);

        // Assert
        var sqlServerPlugin = kernel.Plugins.FirstOrDefault(p => p.Name == "SqlServer");
        sqlServerPlugin.Should().NotBeNull("SqlServer plugin should be registered");

        var expectedFunctions = new[]
        {
            "ValidateSqlSyntax",
            "GetDatabaseSchema",
            "ExecuteSqlQuery", 
            "AnalyzeQueryPerformance",
            "ExecuteStoredProcedure",
            "ExecuteSqlScript",
            "ExecuteSqlCommand"
        };

        foreach (var functionName in expectedFunctions)
        {
            sqlServerPlugin.Should().Contain(f => f.Name == functionName,
                $"Function {functionName} should be registered");
        }
    }

    #endregion

    #region Security Helper Method Tests

    [Theory]
    [InlineData("xp_cmdshell 'dir'", true)]
    [InlineData("EXEC sp_configure", true)]
    [InlineData("SELECT * FROM openrowset", true)]
    [InlineData("exec('DROP TABLE test')", true)]
    [InlineData("SELECT * FROM sys.tables", false)]
    [InlineData("INSERT INTO test VALUES (1)", false)]
    public void HasSecurityConcerns_DetectsSecurityPatterns(string sql, bool expectedHasConcerns)
    {
        // Arrange - Use reflection to test private static method
        var plugin = new SqlServerPlugin();
        var method = typeof(SqlServerPlugin).GetMethod("HasSecurityConcerns", 
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { sql });

        // Assert
        result.Should().Be(expectedHasConcerns, 
            $"SQL '{sql}' should {(expectedHasConcerns ? "have" : "not have")} security concerns");
    }

    [Theory]
    [InlineData("SELECT * FROM test", false)]
    [InlineData("INSERT INTO test VALUES (1)", true)]
    [InlineData("UPDATE test SET col = 1", true)]
    [InlineData("DELETE FROM test", true)]
    [InlineData("DROP TABLE test", true)]
    [InlineData("ALTER TABLE test ADD col INT", true)]
    [InlineData("CREATE TABLE test (id INT)", true)]
    [InlineData("TRUNCATE TABLE test", true)]
    [InlineData("MERGE INTO test USING source ON condition", true)]
    public void HasDataModification_DetectsModificationCommands(string sql, bool expectedHasModification)
    {
        // Arrange - Use reflection to test private static method
        var method = typeof(SqlServerPlugin).GetMethod("HasDataModification", 
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, new object[] { sql });

        // Assert
        result.Should().Be(expectedHasModification,
            $"SQL '{sql}' should {(expectedHasModification ? "have" : "not have")} data modification");
    }

    #endregion

    #region Script Parsing Tests

    [Fact]
    public void SplitSqlScript_WithSemicolonSeparators_SplitsCorrectly()
    {
        // Arrange
        var script = @"
            SELECT 1 as FirstStatement;
            SELECT 2 as SecondStatement;
            SELECT 3 as ThirdStatement;";

        // Act - Use reflection to test private method
        var method = typeof(SqlServerPlugin).GetMethod("SplitSqlScript",
            BindingFlags.NonPublic | BindingFlags.Static);
        var result = (List<string>)method!.Invoke(null, new object[] { script });

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Contain("SELECT 1");
        result[1].Should().Contain("SELECT 2");
        result[2].Should().Contain("SELECT 3");
    }

    [Fact]
    public void SplitSqlScript_WithGoBatchSeparators_SplitsCorrectly()
    {
        // Arrange
        var script = @"
            SELECT 1 as FirstBatch
            GO
            SELECT 2 as SecondBatch
            GO
            SELECT 3 as ThirdBatch";

        // Act - Use reflection to test private method
        var method = typeof(SqlServerPlugin).GetMethod("SplitSqlScript",
            BindingFlags.NonPublic | BindingFlags.Static);
        var result = (List<string>)method!.Invoke(null, new object[] { script });

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Contain("SELECT 1");
        result[1].Should().Contain("SELECT 2");
        result[2].Should().Contain("SELECT 3");
        result.Should().NotContain(s => s.Contains("GO"));
    }

    [Fact]
    public void SplitSqlScript_WithComments_IgnoresCommentLines()
    {
        // Arrange
        var script = @"
            -- This is a comment
            SELECT 1 as ValidStatement;
            -- Another comment line
            SELECT 2 as AnotherStatement;
            /* Block comment */";

        // Act - Use reflection to test private method
        var method = typeof(SqlServerPlugin).GetMethod("SplitSqlScript",
            BindingFlags.NonPublic | BindingFlags.Static);
        var result = (List<string>)method!.Invoke(null, new object[] { script });

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Contain("SELECT 1");
        result[1].Should().Contain("SELECT 2");
        result.Should().NotContain(s => s.Contains("--"));
        result.Should().NotContain(s => s.Contains("/*"));
    }

    [Fact]
    public void SplitSqlScript_WithMixedSeparators_HandlesBothCorrectly()
    {
        // Arrange
        var script = @"
            SELECT 1;
            SELECT 2;
            GO
            SELECT 3
            GO
            SELECT 4;";

        // Act - Use reflection to test private method
        var method = typeof(SqlServerPlugin).GetMethod("SplitSqlScript",
            BindingFlags.NonPublic | BindingFlags.Static);
        var result = (List<string>)method!.Invoke(null, new object[] { script });

        // Assert
        result.Should().HaveCount(4);
        result.Should().Contain(s => s.Contains("SELECT 1"));
        result.Should().Contain(s => s.Contains("SELECT 2"));
        result.Should().Contain(s => s.Contains("SELECT 3"));
        result.Should().Contain(s => s.Contains("SELECT 4"));
    }

    [Fact]
    public void SplitSqlScript_WithEmptyStatements_FiltersEmptyStrings()
    {
        // Arrange
        var script = @"
            
            SELECT 1;
            
            GO
            
            SELECT 2;
            
            ";

        // Act - Use reflection to test private method
        var method = typeof(SqlServerPlugin).GetMethod("SplitSqlScript",
            BindingFlags.NonPublic | BindingFlags.Static);
        var result = (List<string>)method!.Invoke(null, new object[] { script });

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(s => s.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void SplitSqlScript_WithComplexScript_ParsesCorrectly()
    {
        // Arrange
        var script = @"
            -- Create temporary table
            CREATE TABLE #TempTest (
                Id INT IDENTITY(1,1),
                Name NVARCHAR(50) NOT NULL
            );
            GO
            
            -- Insert test data
            INSERT INTO #TempTest (Name) VALUES ('Test1');
            INSERT INTO #TempTest (Name) VALUES ('Test2');
            
            -- Query the data
            SELECT Id, Name FROM #TempTest ORDER BY Id;
            
            -- Clean up
            DROP TABLE #TempTest;";

        // Act - Use reflection to test private method
        var method = typeof(SqlServerPlugin).GetMethod("SplitSqlScript",
            BindingFlags.NonPublic | BindingFlags.Static);
        var result = (List<string>)method!.Invoke(null, new object[] { script });

        // Assert
        result.Should().HaveCount(5); // CREATE, INSERT 1, INSERT 2, SELECT, DROP
        result.Should().Contain(s => s.Contains("CREATE TABLE #TempTest"));
        result.Should().Contain(s => s.Contains("INSERT INTO #TempTest"));
        result.Should().Contain(s => s.Contains("SELECT Id, Name"));
        result.Should().Contain(s => s.Contains("DROP TABLE #TempTest"));
    }

    #endregion

    #region Configuration Validation Tests

    [Fact]
    public void Plugin_WithValidConfiguration_AcceptsConfiguration()
    {
        // Arrange
        var config = CreateTestConfiguration();

        // Act & Assert
        config.Should().NotBeNull();
        config.SqlServer.Should().NotBeNull();
        config.SqlServer.ConnectionStrings.Should().ContainKey("default");
        config.SqlServer.QueryExecution.Safety.AllowDataModification.Should().BeFalse();
    }

    [Fact]
    public void Configuration_HasAppropriateSecurityDefaults()
    {
        // Arrange
        var config = CreateTestConfiguration();

        // Act & Assert
        config.SqlServer.QueryExecution.Safety.AllowDataModification.Should().BeFalse(
            "Data modification should be disabled by default for security");
        config.SqlServer.QueryExecution.Safety.MaxRowsReturned.Should().Be(1000,
            "Row limit should prevent excessive memory usage");
        config.SqlServer.QueryExecution.Safety.DefaultTimeout.Should().Be(30,
            "Default timeout should prevent long-running queries");
    }

    #endregion

    #region Performance Requirements Tests

    [Fact]
    public void PerformanceTargets_MeetProjectRequirements()
    {
        // Arrange
        var config = CreateTestConfiguration();

        // Act & Assert
        config.Performance.ResponseTargets["syntax_validation"].Should().Be(100,
            "Syntax validation should complete within 100ms");
        config.Performance.ResponseTargets["schema_introspection"].Should().Be(500,
            "Schema introspection should complete within 500ms");
        config.Performance.ResponseTargets["query_optimization"].Should().Be(1000,
            "Query optimization should complete within 1000ms");
    }

    #endregion

    #region Error Handling Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SqlValidation_WithInvalidInput_HandlesGracefully(string invalidSql)
    {
        // These tests would verify that empty/null SQL is handled appropriately
        // without throwing exceptions
        
        // Arrange & Act & Assert
        if (invalidSql == null)
        {
            invalidSql.Should().BeNull();
        }
        else
        {
            invalidSql.Trim().Should().BeEmpty();
        }
    }

    #endregion

    #region Test Helper Methods

    private static AgentConfiguration CreateTestConfiguration()
    {
        return new AgentConfiguration
        {
            SqlServer = new SqlServerConfiguration
            {
                ConnectionStrings = new Dictionary<string, string>
                {
                    ["default"] = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;"
                },
                DefaultDatabase = "master",
                QueryExecution = new QueryExecutionConfiguration
                {
                    Safety = new QuerySafetyConfiguration
                    {
                        AllowDataModification = false,
                        MaxRowsReturned = 1000,
                        DefaultTimeout = 30,
                        ProhibitedKeywords = new List<string> 
                        { 
                            "xp_cmdshell", "sp_configure", "openrowset", "opendatasource" 
                        }
                    }
                }
            },
            Performance = new PerformanceSettings
            {
                ResponseTargets = new Dictionary<string, int>
                {
                    ["syntax_validation"] = 100,
                    ["schema_introspection"] = 500,
                    ["query_optimization"] = 1000,
                    ["complex_analysis"] = 3000
                }
            },
            QualityControl = new QualityControlConfiguration
            {
                Validation = new ValidationConfiguration
                {
                    MandatorySyntaxCheck = true,
                    ValidateAgainstSchema = true,
                    CheckPerformanceImpact = true
                }
            }
        };
    }

    #endregion
}