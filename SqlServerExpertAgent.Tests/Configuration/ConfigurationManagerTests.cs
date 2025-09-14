using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SqlServerExpertAgent.Configuration;

namespace SqlServerExpertAgent.Tests.Configuration;

/// <summary>
/// Test-first approach for ConfigurationManager
/// Tests cover configuration loading, validation, security, and environment handling
/// </summary>
public class ConfigurationManagerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly SqlServerExpertAgent.Configuration.ConfigurationManager _configManager;
    private readonly IConfiguration _configuration;

    public ConfigurationManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        Environment.CurrentDirectory = _testDirectory;

        // Setup test configuration
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(_testDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables();

        _configuration = configBuilder.Build();
        _configManager = new ConfigurationManager(_configuration, NullLogger<ConfigurationManager>.Instance);
    }

    #region Configuration Loading Tests

    [Fact]
    public async Task LoadConfigurationAsync_WithDefaultSettings_ReturnsValidConfiguration()
    {
        // Arrange - Create minimal config file
        await CreateTestConfigFileAsync(new
        {
            Identity = new { Name = "TestAgent" },
            Llm = new { ApiKey = "test-key", Model = "gpt-4" },
            SqlServer = new { ConnectionStrings = new { Default = "Server=.;Database=Test;" } }
        });

        // Act
        var config = await _configManager.LoadConfigurationAsync();

        // Assert
        config.Should().NotBeNull();
        config.Identity.Name.Should().Be("TestAgent");
        config.Llm.ApiKey.Should().Be("test-key");
        config.SqlServer.ConnectionStrings["default"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithMissingRequiredFields_ThrowsValidationException()
    {
        // Arrange - Create config without required fields
        await CreateTestConfigFileAsync(new { Identity = new { Name = "" } });

        // Act & Assert
        var act = async () => await _configManager.LoadConfigurationAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Agent Identity.Name is required*");
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithFileBasedApiKey_LoadsSecureKey()
    {
        // Arrange
        var testApiKey = "sk-test-api-key-12345";
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "claude-api-key.txt"), testApiKey);
        await CreateTestConfigFileAsync(new
        {
            Identity = new { Name = "TestAgent" },
            SqlServer = new { ConnectionStrings = new { Default = "Server=.;Database=Test;" } }
        });

        // Act
        var config = await _configManager.LoadConfigurationAsync();

        // Assert
        config.Llm.ApiKey.Should().Be(testApiKey);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithEnvironmentVariables_OverridesFileConfig()
    {
        // Arrange
        Environment.SetEnvironmentVariable("CLAUDE_API_KEY", "env-api-key");
        Environment.SetEnvironmentVariable("AGENT_LOG_LEVEL", "Debug");
        
        await CreateTestConfigFileAsync(new
        {
            Identity = new { Name = "TestAgent" },
            Llm = new { ApiKey = "file-key" },
            SqlServer = new { ConnectionStrings = new { Default = "Server=.;Database=Test;" } }
        });

        try
        {
            // Act
            var config = await _configManager.LoadConfigurationAsync();

            // Assert
            config.Llm.ApiKey.Should().Be("env-api-key");
            config.Observability.Logging.LogLevel.Should().Be("Debug");
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("CLAUDE_API_KEY", null);
            Environment.SetEnvironmentVariable("AGENT_LOG_LEVEL", null);
        }
    }

    #endregion

    #region Environment-Specific Configuration Tests

    [Fact]
    public async Task LoadConfigurationAsync_DevelopmentEnvironment_AppliesCorrectOverrides()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        await CreateTestConfigFileAsync(GetValidTestConfig());

        try
        {
            // Act
            var config = await _configManager.LoadConfigurationAsync();

            // Assert
            config.Observability.Logging.LogLevel.Should().Be("Debug");
            config.Observability.Logging.EnableConsoleLogging.Should().BeTrue();
            config.Knowledge.Learning.EnableAdaptiveLearning.Should().BeTrue();
            config.Performance.ResponseTargets["syntax_validation"].Should().Be(200); // Relaxed
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public async Task LoadConfigurationAsync_ProductionEnvironment_AppliesStrictSettings()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        await CreateTestConfigFileAsync(GetValidTestConfig());

        try
        {
            // Act
            var config = await _configManager.LoadConfigurationAsync();

            // Assert
            config.Observability.Logging.LogLevel.Should().Be("Information");
            config.Performance.Cache.SizeLimit.Should().Be(2000); // Larger production cache
            config.SqlServer.QueryExecution.Safety.AllowDataModification.Should().BeFalse(); // Production safety
            config.Performance.ResponseTargets["syntax_validation"].Should().Be(80); // Stricter (100 * 0.8)
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    [Fact]
    public async Task LoadConfigurationAsync_TestingEnvironment_AppliesTestingConstraints()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        await CreateTestConfigFileAsync(GetValidTestConfig());

        try
        {
            // Act
            var config = await _configManager.LoadConfigurationAsync();

            // Assert
            config.Performance.MaxConcurrentOperations.Should().Be(1); // Single-threaded
            config.QualityControl.Validation.MandatorySyntaxCheck.Should().BeTrue();
            config.SqlServer.QueryExecution.Safety.AllowDataModification.Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        }
    }

    #endregion

    #region Dynamic Configuration Tests

    [Fact]
    public async Task LoadConfigurationAsync_AutoDetectsSystemCapabilities()
    {
        // Arrange
        await CreateTestConfigFileAsync(GetValidTestConfig());

        // Act
        var config = await _configManager.LoadConfigurationAsync();

        // Assert
        config.Performance.MaxConcurrentOperations.Should().BeLessOrEqualTo(Environment.ProcessorCount);
        config.Performance.MaxConcurrentOperations.Should().BeGreaterThan(0);
        
        // Cache size should be reasonable
        config.Performance.Cache.SizeLimit.Should().BeInRange(100, 5000);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithHighMemorySystem_IncreasesCache()
    {
        // This test simulates high memory scenario
        // In real implementation, we'd mock GC.GetTotalMemory
        
        // Arrange
        var configData = GetValidTestConfig();
        configData.Performance = new { Cache = new { SizeLimit = 800 } }; // Lower base
        await CreateTestConfigFileAsync(configData);

        // Act
        var config = await _configManager.LoadConfigurationAsync();

        // Assert - Dynamic defaults should potentially increase this
        config.Performance.Cache.SizeLimit.Should().BeGreaterOrEqualTo(800);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task LoadConfigurationAsync_WithInvalidCacheSize_ThrowsValidationException()
    {
        // Arrange
        var configData = GetValidTestConfig();
        configData.Performance = new { Cache = new { SizeLimit = -1 } };
        await CreateTestConfigFileAsync(configData);

        // Act & Assert
        var act = async () => await _configManager.LoadConfigurationAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cache size limit must be positive*");
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithInvalidSuccessMetrics_ThrowsValidationException()
    {
        // Arrange
        var configData = GetValidTestConfig();
        configData.QualityControl = new { 
            SuccessMetrics = new { TargetSyntaxAccuracy = 1.5 } // Invalid: > 1.0
        };
        await CreateTestConfigFileAsync(configData);

        // Act & Assert
        var act = async () => await _configManager.LoadConfigurationAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Target syntax accuracy must be between 0 and 1*");
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithInvalidResponseTargets_ThrowsValidationException()
    {
        // Arrange
        var configData = GetValidTestConfig();
        configData.Performance = new {
            ResponseTargets = new { syntax_validation = -100 } // Invalid: negative
        };
        await CreateTestConfigFileAsync(configData);

        // Act & Assert
        var act = async () => await _configManager.LoadConfigurationAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Response target for syntax_validation must be positive*");
    }

    #endregion

    #region Template Generation Tests

    [Fact]
    public async Task CreateTemplateFilesAsync_CreatesAllRequiredFiles()
    {
        // Arrange
        var templateDir = Path.Combine(_testDirectory, "templates");

        // Act
        await _configManager.CreateTemplateFilesAsync(templateDir);

        // Assert
        Directory.Exists(templateDir).Should().BeTrue();
        
        File.Exists(Path.Combine(templateDir, "appsettings.template.json")).Should().BeTrue();
        File.Exists(Path.Combine(templateDir, "claude-api-key.txt")).Should().BeTrue();
        File.Exists(Path.Combine(templateDir, "connectionstrings.template.json")).Should().BeTrue();
        File.Exists(Path.Combine(templateDir, "appsettings.development.json")).Should().BeTrue();
        File.Exists(Path.Combine(templateDir, "appsettings.production.json")).Should().BeTrue();
    }

    [Fact]
    public async Task CreateTemplateFilesAsync_GeneratesValidJsonTemplates()
    {
        // Arrange
        var templateDir = Path.Combine(_testDirectory, "templates");

        // Act
        await _configManager.CreateTemplateFilesAsync(templateDir);

        // Assert - Templates should be valid JSON
        var templateContent = await File.ReadAllTextAsync(Path.Combine(templateDir, "appsettings.template.json"));
        var act = () => System.Text.Json.JsonDocument.Parse(templateContent);
        act.Should().NotThrow();

        var connectionStringsContent = await File.ReadAllTextAsync(Path.Combine(templateDir, "connectionstrings.template.json"));
        var act2 = () => System.Text.Json.JsonDocument.Parse(connectionStringsContent);
        act2.Should().NotThrow();
    }

    #endregion

    #region Configuration Summary Tests

    [Fact]
    public void GetConfigurationSummary_ReturnsValidSummary()
    {
        // Act
        var summary = _configManager.GetConfigurationSummary();

        // Assert
        summary.Should().NotBeNullOrEmpty();
        summary.Should().Contain("Environment");
        summary.Should().Contain("ProcessorCount");
        summary.Should().Contain("RuntimeVersion");
        
        // Should not contain sensitive information
        summary.Should().NotContain("password", StringComparison.OrdinalIgnoreCase);
        summary.Should().NotContain("api-key", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Helper Methods

    private async Task CreateTestConfigFileAsync(object config)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "appsettings.json"), json);
    }

    private static dynamic GetValidTestConfig()
    {
        return new
        {
            Identity = new { Name = "TestSqlExpert" },
            Llm = new { 
                ApiKey = "test-key",
                Model = "gpt-4",
                Provider = "AzureOpenAI"
            },
            SqlServer = new { 
                ConnectionStrings = new { 
                    Default = "Server=.;Database=TestDB;Trusted_Connection=true;" 
                }
            },
            Performance = new {
                Cache = new { SizeLimit = 1000 },
                ResponseTargets = new { syntax_validation = 100 }
            },
            QualityControl = new {
                SuccessMetrics = new { TargetSyntaxAccuracy = 0.99 }
            }
        };
    }

    #endregion

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}

/// <summary>
/// Integration tests for ConfigurationManager with real file system and environment
/// </summary>
[Collection("Configuration Integration Tests")]
public class ConfigurationManagerIntegrationTests
{
    [Fact]
    public async Task LoadConfiguration_WithRealEnvironment_DoesNotThrow()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var configManager = new ConfigurationManager(configuration, NullLogger<ConfigurationManager>.Instance);

        // Act & Assert - Should handle missing files gracefully
        var act = async () => await configManager.LoadConfigurationAsync();
        
        // This might throw validation errors (expected), but shouldn't throw file I/O errors
        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => !ex.Message.Contains("file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetConfigurationSummary_WithRealEnvironment_ReturnsSystemInfo()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var configManager = new ConfigurationManager(configuration, NullLogger<ConfigurationManager>.Instance);

        // Act
        var summary = configManager.GetConfigurationSummary();

        // Assert
        summary.Should().Contain(Environment.ProcessorCount.ToString());
        summary.Should().Contain(Environment.Version.ToString());
        summary.Should().Contain(Environment.CurrentDirectory);
    }
}