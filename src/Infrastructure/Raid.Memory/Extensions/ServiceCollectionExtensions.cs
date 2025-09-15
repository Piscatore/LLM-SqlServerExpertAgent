using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Raid.Memory.Configuration;
using Raid.Memory.Data;
using Raid.Memory.Interfaces;
using Raid.Memory.Services;
using StackExchange.Redis;

namespace Raid.Memory.Extensions;

/// <summary>
/// Dependency injection extensions for Memory Agent
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Memory Agent services to the service collection
    /// </summary>
    public static IServiceCollection AddMemoryAgent(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Memory settings
        var section = configuration.GetSection("RaidMemory");
        services.Configure<MemoryConfiguration>(section);

        // Add configuration as singleton for direct injection
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MemoryConfiguration>>();
            return options.Value;
        });

        // Add Entity Framework DbContext
        services.AddDbContext<MemoryDbContext>((serviceProvider, options) =>
        {
            var config = serviceProvider.GetRequiredService<MemoryConfiguration>();
            options.UseSqlServer(config.SqlConnectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            });
        });

        // Add Redis connection
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = sp.GetRequiredService<MemoryConfiguration>();
            var connectionString = config.RedisConnectionString;

            var configOptions = ConfigurationOptions.Parse(connectionString);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectRetry = 3;
            configOptions.ConnectTimeout = 5000;

            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Register Memory Agent interfaces and implementations
        services.AddScoped<IVectorSearchEngine, InMemoryVectorSearchEngine>();
        services.AddScoped<IContextManager, RedisContextManager>();
        services.AddScoped<IKnowledgeBase, SqlKnowledgeBase>();
        services.AddScoped<IMemoryAgent, MemoryAgent>();

        return services;
    }

    /// <summary>
    /// Adds Memory Agent with custom configuration
    /// </summary>
    public static IServiceCollection AddMemoryAgent(this IServiceCollection services, Action<MemoryConfiguration> configure)
    {
        var config = new MemoryConfiguration();
        configure(config);

        services.AddSingleton(config);

        // Add Entity Framework DbContext
        services.AddDbContext<MemoryDbContext>(options =>
        {
            options.UseSqlServer(config.SqlConnectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            });
        });

        // Add Redis connection
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = config.RedisConnectionString;

            var configOptions = ConfigurationOptions.Parse(connectionString);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectRetry = 3;
            configOptions.ConnectTimeout = 5000;

            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Register Memory Agent interfaces and implementations
        services.AddScoped<IVectorSearchEngine, InMemoryVectorSearchEngine>();
        services.AddScoped<IContextManager, RedisContextManager>();
        services.AddScoped<IKnowledgeBase, SqlKnowledgeBase>();
        services.AddScoped<IMemoryAgent, MemoryAgent>();

        return services;
    }

    /// <summary>
    /// Ensures Memory Agent database is created and migrated
    /// </summary>
    public static async Task<IServiceProvider> EnsureMemoryDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

        await context.Database.EnsureCreatedAsync();

        return serviceProvider;
    }

    /// <summary>
    /// Tests Memory Agent health
    /// </summary>
    public static async Task<bool> TestMemoryAgentHealthAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var memoryAgent = scope.ServiceProvider.GetRequiredService<IMemoryAgent>();

        return await memoryAgent.IsHealthyAsync();
    }
}