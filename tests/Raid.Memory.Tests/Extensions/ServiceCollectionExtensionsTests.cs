using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Raid.Memory.Configuration;
using Raid.Memory.Extensions;
using Raid.Memory.Interfaces;
using Xunit;

namespace Raid.Memory.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMemoryAgent_WithConfiguration_ShouldRegisterAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var configurationMock = new Mock<IConfiguration>();
        var sectionMock = new Mock<IConfigurationSection>();

        configurationMock
            .Setup(c => c.GetSection("RaidMemory"))
            .Returns(sectionMock.Object);

        // Create minimal configuration values
        sectionMock.Setup(s => s["RedisConnectionString"]).Returns("localhost:6379");
        sectionMock.Setup(s => s["SqlConnectionString"]).Returns("Server=(localdb)\\mssqllocaldb;Database=TestMemory;Trusted_Connection=true;");

        // Act
        services.AddMemoryAgent(configurationMock.Object);

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Verify core services are registered
        Assert.NotNull(serviceProvider.GetService<MemoryConfiguration>());
        Assert.NotNull(serviceProvider.GetService<IVectorSearchEngine>());
        Assert.NotNull(serviceProvider.GetService<IContextManager>());
        Assert.NotNull(serviceProvider.GetService<IKnowledgeBase>());
        Assert.NotNull(serviceProvider.GetService<IMemoryAgent>());
    }

    [Fact]
    public void AddMemoryAgent_WithCustomConfiguration_ShouldRegisterAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var customConfig = new MemoryConfiguration
        {
            RedisConnectionString = "localhost:6380",
            SqlConnectionString = "Server=(localdb)\\mssqllocaldb;Database=CustomMemory;Trusted_Connection=true;",
            Management = new MemoryManagementConfiguration
            {
                MinKnowledgeConfidence = 0.5f,
                MaxSimilarKnowledgeResults = 15
            }
        };

        // Act
        services.AddMemoryAgent(config =>
        {
            config.RedisConnectionString = customConfig.RedisConnectionString;
            config.SqlConnectionString = customConfig.SqlConnectionString;
            config.Management = customConfig.Management;
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var registeredConfig = serviceProvider.GetService<MemoryConfiguration>();
        Assert.NotNull(registeredConfig);
        Assert.Equal("localhost:6380", registeredConfig.RedisConnectionString);
        Assert.Equal(0.5f, registeredConfig.Management.MinKnowledgeConfidence);
        Assert.Equal(15, registeredConfig.Management.MaxSimilarKnowledgeResults);

        // Verify all services are registered
        Assert.NotNull(serviceProvider.GetService<IMemoryAgent>());
        Assert.NotNull(serviceProvider.GetService<IVectorSearchEngine>());
        Assert.NotNull(serviceProvider.GetService<IContextManager>());
        Assert.NotNull(serviceProvider.GetService<IKnowledgeBase>());
    }

    [Fact]
    public void AddMemoryAgent_ShouldRegisterScopedServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddMemoryAgent(config =>
        {
            config.SqlConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TestMemory;Trusted_Connection=true;";
            config.RedisConnectionString = "localhost:6379";
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var memoryAgent1 = scope1.ServiceProvider.GetService<IMemoryAgent>();
        var memoryAgent2 = scope2.ServiceProvider.GetService<IMemoryAgent>();

        Assert.NotNull(memoryAgent1);
        Assert.NotNull(memoryAgent2);
        Assert.NotSame(memoryAgent1, memoryAgent2); // Should be different instances (scoped)
    }

    [Fact]
    public void AddMemoryAgent_ShouldRegisterSingletonConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddMemoryAgent(config =>
        {
            config.SqlConnectionString = "test-connection";
            config.RedisConnectionString = "localhost:6379";
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        var config1 = serviceProvider.GetService<MemoryConfiguration>();
        var config2 = serviceProvider.GetService<MemoryConfiguration>();

        Assert.NotNull(config1);
        Assert.NotNull(config2);
        Assert.Same(config1, config2); // Should be same instance (singleton)
        Assert.Equal("test-connection", config1.SqlConnectionString);
    }

    [Fact]
    public void AddMemoryAgent_WithDefaultConfiguration_ShouldHaveCorrectDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddMemoryAgent(config =>
        {
            config.SqlConnectionString = "test";
            config.RedisConnectionString = "localhost:6379";
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider.GetService<MemoryConfiguration>();

        Assert.NotNull(configuration);
        Assert.NotNull(configuration.Management);
        Assert.NotNull(configuration.OpenAI);
        Assert.NotNull(configuration.VectorDatabase);

        // Check default values
        Assert.Equal(0.3f, configuration.Management.MinKnowledgeConfidence);
        Assert.Equal(0.7f, configuration.Management.DefaultSimilarityThreshold);
        Assert.Equal(TimeSpan.FromHours(24), configuration.Management.SessionContextTtl);
        Assert.Equal("InMemory", configuration.VectorDatabase.Provider);
        Assert.Equal(1536, configuration.VectorDatabase.VectorDimension);
    }

    [Fact]
    public void AddMemoryAgent_ServiceResolution_ShouldNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMemoryAgent(config =>
        {
            config.SqlConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TestMemory;Trusted_Connection=true;";
            config.RedisConnectionString = "localhost:6379";
        });

        // Act & Assert - Should not throw when resolving services
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var services_to_test = new[]
        {
            typeof(MemoryConfiguration),
            typeof(IVectorSearchEngine),
            typeof(IMemoryAgent)
        };

        foreach (var serviceType in services_to_test)
        {
            var service = scope.ServiceProvider.GetService(serviceType);
            Assert.NotNull(service);
        }
    }

    [Fact]
    public void AddMemoryAgent_WithLoggingConfiguration_ShouldInjectLoggers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        services.AddMemoryAgent(config =>
        {
            config.SqlConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TestMemory;Trusted_Connection=true;";
            config.RedisConnectionString = "localhost:6379";
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify loggers are properly injected
        using var scope = serviceProvider.CreateScope();
        var memoryAgent = scope.ServiceProvider.GetService<IMemoryAgent>();

        Assert.NotNull(memoryAgent);
        // If we get here without exceptions, logging injection worked
    }
}