using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SqlServerExpertAgent.Configuration;

namespace SqlServerExpertAgent.Tests.SqlServer;

/// <summary>
/// Test-first approach for SQL syntax validation using SMO
/// These tests define the expected behavior BEFORE implementation
/// </summary>
public class SqlSyntaxValidatorTests
{
    private readonly Mock<ISqlServerConnectionProvider> _connectionProviderMock;
    private readonly AgentConfiguration _testConfig;

    public SqlSyntaxValidatorTests()
    {
        _connectionProviderMock = new Mock<ISqlServerConnectionProvider>();
        _testConfig = CreateTestConfiguration();
    }

    #region Basic Syntax Validation Tests

    [Fact]
    public async Task ValidateSqlSyntax_WithValidSelectStatement_ReturnsValid()
    {
        // Arrange
        var sql = "SELECT ProductName, Price FROM Products WHERE CategoryId = 1";
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
        result.ValidationTimeMs.Should().BeLessOrEqualTo(1000); // Performance requirement
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithInvalidSyntax_ReturnsInvalidWithError()
    {
        // Arrange
        var sql = "SELECT * FROMM Products"; // Typo in FROM
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.ErrorMessage.Should().Contain("FROMM"); // Should identify the error
        result.LineNumber.Should().Be(1);
        result.ColumnNumber.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithEmptyString_ReturnsInvalid()
    {
        // Arrange
        var sql = "";
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithNullInput_ThrowsArgumentException()
    {
        // Arrange
        var validator = CreateValidator();

        // Act & Assert
        var act = async () => await validator.ValidateSqlSyntaxAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region SQL Server Specific Syntax Tests

    [Fact]
    public async Task ValidateSqlSyntax_WithCorrectBracketEscaping_ReturnsValid()
    {
        // Arrange - Test the specific bracket syntax that caused OrfPIM2 issues
        var sql = "SELECT [Variant Metafield: custom.deprecated [boolean]]] FROM Products";
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue("Correct bracket escaping should be valid");
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithIncorrectBracketEscaping_ReturnsInvalid()
    {
        // Arrange - Test the incorrect bracket syntax that caused errors
        var sql = "SELECT [Variant Metafield: custom.deprecated [boolean]] FROM Products";
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse("Incorrect bracket escaping should be invalid");
        result.ErrorMessage.Should().Contain("bracket", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithMissingTableAlias_ReturnsWarning()
    {
        // Arrange - Test table alias requirement
        var sql = @"
            SELECT ProductName, Price 
            FROM Products p
            JOIN Categories c ON ProductId = c.Id"; // Missing table alias on ProductId

        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.Warnings.Should().Contain(w => w.Contains("table alias", StringComparison.OrdinalIgnoreCase));
        result.SuggestedFixes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithCTEScopeIssue_ReturnsInvalid()
    {
        // Arrange - Test CTE scope limitations
        var sql = @"
            WITH OrderedData AS (SELECT * FROM Products ORDER BY Price)
            SELECT * FROM Products; -- Invalid: CTE not used immediately
            SELECT * FROM OrderedData;"; // This should fail

        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse("CTE scope violation should be invalid");
        result.ErrorMessage.Should().Contain("CTE", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Performance and Optimization Tests

    [Fact]
    public async Task ValidateSqlSyntax_WithPerformanceAnalysis_ReturnsOptimizationSuggestions()
    {
        // Arrange - SQL that works but could be optimized
        var sql = "SELECT * FROM Products WHERE UPPER(ProductName) = 'WIDGET'";
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.OptimizationSuggestions.Should().NotBeEmpty();
        result.OptimizationSuggestions.Should().Contain(s => 
            s.Contains("UPPER", StringComparison.OrdinalIgnoreCase) && 
            s.Contains("index", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithSelectStar_ReturnsPerformanceWarning()
    {
        // Arrange
        var sql = "SELECT * FROM Products";
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("SELECT *", StringComparison.OrdinalIgnoreCase));
        result.PerformanceImpact.Should().Be(ValidationResult.PerformanceLevel.Medium);
    }

    #endregion

    #region Schema Validation Tests

    [Fact]
    public async Task ValidateSqlSyntax_WithValidTableName_ReturnsValid()
    {
        // Arrange
        var sql = "SELECT ProductId, ProductName FROM Products";
        var validator = CreateValidator();

        // Setup mock to return valid schema info
        _connectionProviderMock.Setup(x => x.GetTableInfoAsync("Products"))
            .ReturnsAsync(new TableInfo { Name = "Products", Exists = true, 
                Columns = new[] { "ProductId", "ProductName", "Price" } });

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.SchemaValidation.Should().NotBeNull();
        result.SchemaValidation.TablesValidated.Should().Contain("Products");
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithInvalidTableName_ReturnsSchemaError()
    {
        // Arrange
        var sql = "SELECT * FROM NonExistentTable";
        var validator = CreateValidator();

        // Setup mock to return table not found
        _connectionProviderMock.Setup(x => x.GetTableInfoAsync("NonExistentTable"))
            .ReturnsAsync(new TableInfo { Name = "NonExistentTable", Exists = false });

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("NonExistentTable");
        result.ErrorType.Should().Be(ValidationResult.ErrorTypes.SchemaError);
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithInvalidColumnName_ReturnsSchemaError()
    {
        // Arrange
        var sql = "SELECT InvalidColumn FROM Products";
        var validator = CreateValidator();

        // Setup mock schema info
        _connectionProviderMock.Setup(x => x.GetTableInfoAsync("Products"))
            .ReturnsAsync(new TableInfo { Name = "Products", Exists = true, 
                Columns = new[] { "ProductId", "ProductName", "Price" } });

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("InvalidColumn");
        result.SchemaValidation.InvalidColumns.Should().Contain("InvalidColumn");
    }

    #endregion

    #region Security and Safety Tests

    [Fact]
    public async Task ValidateSqlSyntax_WithDataModificationAttempt_ReturnsSecurityError()
    {
        // Arrange
        var sql = "DELETE FROM Products WHERE Price > 1000";
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.ErrorType.Should().Be(ValidationResult.ErrorTypes.SecurityViolation);
        result.ErrorMessage.Should().Contain("DELETE", StringComparison.OrdinalIgnoreCase);
        result.SecurityRisk.Should().Be(ValidationResult.RiskLevel.High);
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithSqlInjectionPattern_ReturnsSecurityWarning()
    {
        // Arrange
        var sql = "SELECT * FROM Users WHERE Username = '" + "admin' OR '1'='1" + "'";
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.Warnings.Should().Contain(w => w.Contains("injection", StringComparison.OrdinalIgnoreCase));
        result.SecurityRisk.Should().Be(ValidationResult.RiskLevel.High);
        result.SuggestedFixes.Should().Contain(f => f.Contains("parameter", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task ValidateSqlSyntax_WithVeryLongQuery_HandlesGracefully()
    {
        // Arrange
        var longSql = "SELECT " + string.Join(", ", Enumerable.Range(1, 1000).Select(i => $"Column{i}")) + " FROM LargeTable";
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(longSql);

        // Assert
        result.Should().NotBeNull();
        result.ValidationTimeMs.Should().BeLessOrEqualTo(5000); // Should handle within reasonable time
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithMultilineQuery_ReturnsCorrectLineNumbers()
    {
        // Arrange
        var sql = @"SELECT ProductId, 
                    ProductName,
                    Invalid Column Name
                    FROM Products";
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        if (!result.IsValid)
        {
            result.LineNumber.Should().Be(3); // Error on line 3
        }
    }

    [Fact]
    public async Task ValidateSqlSyntax_WithConnectionFailure_HandlesGracefully()
    {
        // Arrange
        var sql = "SELECT * FROM Products";
        var validator = CreateValidator();

        // Setup mock to simulate connection failure
        _connectionProviderMock.Setup(x => x.GetTableInfoAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.Should().NotBeNull();
        result.Warnings.Should().Contain(w => w.Contains("schema validation", StringComparison.OrdinalIgnoreCase));
        result.IsValid.Should().BeTrue("Syntax should still be validated even if schema validation fails");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task ValidateSqlSyntax_MeetsPerformanceTargets()
    {
        // Arrange
        var sql = "SELECT ProductId, ProductName FROM Products WHERE CategoryId = 1";
        var validator = CreateValidator();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await validator.ValidateSqlSyntaxAsync(sql);
        stopwatch.Stop();

        // Assert - Should meet the 100ms target for syntax validation
        stopwatch.ElapsedMilliseconds.Should().BeLessOrEqualTo(_testConfig.Performance.ResponseTargets["syntax_validation"]);
        result.ValidationTimeMs.Should().Be(stopwatch.ElapsedMilliseconds, within: 10);
    }

    [Theory]
    [InlineData("SELECT * FROM Products", true)]
    [InlineData("SELECT ProductId FROM Products", true)]
    [InlineData("SELECT FROM Products", false)]
    [InlineData("ELECT * FROM Products", false)]
    [InlineData("SELECT * Products", false)]
    public async Task ValidateSqlSyntax_WithVariousSyntaxPatterns_ReturnsExpectedResults(string sql, bool expectedValid)
    {
        // Arrange
        var validator = CreateValidator();

        // Act
        var result = await validator.ValidateSqlSyntaxAsync(sql);

        // Assert
        result.IsValid.Should().Be(expectedValid, $"SQL '{sql}' should be {(expectedValid ? "valid" : "invalid")}");
    }

    #endregion

    #region Helper Methods

    private ISqlSyntaxValidator CreateValidator()
    {
        // This interface doesn't exist yet - it will be implemented after these tests pass
        // For now, we'll create a mock or use a test implementation
        return new SqlSyntaxValidator(_connectionProviderMock.Object, _testConfig, 
            NullLogger<SqlSyntaxValidator>.Instance);
    }

    private static AgentConfiguration CreateTestConfiguration()
    {
        return new AgentConfiguration
        {
            Performance = new PerformanceSettings
            {
                ResponseTargets = new Dictionary<string, int>
                {
                    ["syntax_validation"] = 100
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
            },
            SqlServer = new SqlServerConfiguration
            {
                QueryExecution = new QueryExecutionConfiguration
                {
                    Safety = new QuerySafetyConfiguration
                    {
                        AllowDataModification = false,
                        ProhibitedKeywords = new List<string> { "DROP", "DELETE", "UPDATE", "INSERT", "TRUNCATE" }
                    }
                }
            }
        };
    }

    #endregion
}

#region Supporting Types (These will be implemented)

// These interfaces and classes don't exist yet - they define the contract
// that the implementation must fulfill

public interface ISqlSyntaxValidator
{
    Task<ValidationResult> ValidateSqlSyntaxAsync(string sql);
}

public interface ISqlServerConnectionProvider  
{
    Task<TableInfo> GetTableInfoAsync(string tableName);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
    public long ValidationTimeMs { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> SuggestedFixes { get; set; } = new();
    public List<string> OptimizationSuggestions { get; set; } = new();
    public PerformanceLevel PerformanceImpact { get; set; }
    public ErrorTypes ErrorType { get; set; }
    public RiskLevel SecurityRisk { get; set; } = RiskLevel.None;
    public SchemaValidationResult? SchemaValidation { get; set; }

    public enum PerformanceLevel { Low, Medium, High, Critical }
    public enum ErrorTypes { SyntaxError, SchemaError, SecurityViolation, PerformanceWarning }
    public enum RiskLevel { None, Low, Medium, High, Critical }
}

public class SchemaValidationResult
{
    public List<string> TablesValidated { get; set; } = new();
    public List<string> InvalidColumns { get; set; } = new();
    public List<string> MissingTables { get; set; } = new();
}

public class TableInfo
{
    public string Name { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public string[] Columns { get; set; } = Array.Empty<string>();
}

// Placeholder for actual implementation - these tests drive the interface design
public class SqlSyntaxValidator : ISqlSyntaxValidator
{
    private readonly ISqlServerConnectionProvider _connectionProvider;
    private readonly AgentConfiguration _config;
    private readonly ILogger<SqlSyntaxValidator> _logger;

    public SqlSyntaxValidator(ISqlServerConnectionProvider connectionProvider, 
        AgentConfiguration config, ILogger<SqlSyntaxValidator> logger)
    {
        _connectionProvider = connectionProvider;
        _config = config;
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateSqlSyntaxAsync(string sql)
    {
        // Implementation will be driven by these tests
        // For now, return a basic result to make tests compilable
        if (string.IsNullOrEmpty(sql))
        {
            return new ValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "SQL cannot be empty",
                ValidationTimeMs = 1
            };
        }

        await Task.Delay(1); // Simulate async work
        return new ValidationResult { IsValid = true, ValidationTimeMs = 50 };
    }
}

#endregion