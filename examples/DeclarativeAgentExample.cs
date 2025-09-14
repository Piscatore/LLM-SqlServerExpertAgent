using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlServerExpertAgent.Configuration;
using SqlServerExpertAgent.Templates;
using SqlServerExpertAgent.Templates.Skills;

namespace SqlServerExpertAgent.Examples;

/// <summary>
/// Example demonstrating declarative agent creation and usage
/// Shows how agents can be created entirely from YAML/JSON configuration
/// </summary>
public class DeclarativeAgentExample
{
    public static async Task RunExampleAsync()
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<SkillRegistry>();
        services.AddSingleton<DeclarativeAgentFactory>();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<DeclarativeAgentExample>>();

        try
        {
            logger.LogInformation("Starting Declarative Agent Example");

            // Initialize skill registry and factory
            var skillRegistry = serviceProvider.GetRequiredService<SkillRegistry>();
            var agentFactory = serviceProvider.GetRequiredService<DeclarativeAgentFactory>();

            // 1. Register skills manually (normally loaded from skill directories)
            skillRegistry.RegisterSkill(new SqlServerSkill());
            
            // 2. Load agent templates from directory
            await agentFactory.LoadTemplatesFromDirectoriesAsync(new[] { "templates" });
            
            logger.LogInformation("Available templates: {Templates}", 
                string.Join(", ", agentFactory.GetAvailableTemplates()));

            // 3. Create agent from template
            var configuration = CreateSampleConfiguration();
            var agent = await agentFactory.CreateAgentAsync("SqlServerExpertAgent", configuration);
            
            logger.LogInformation("Created agent: {AgentName} v{Version}", 
                agent.Identity.Name, agent.Identity.Version);

            // 4. Execute operations using the agent
            await DemonstrateAgentOperations(agent, logger);

            // 5. Demonstrate inline agent creation
            await DemonstrateInlineAgentCreation(agentFactory, configuration, logger);

            logger.LogInformation("Declarative Agent Example completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Declarative Agent Example");
            throw;
        }
    }

    private static async Task DemonstrateAgentOperations(IExpertAgent agent, ILogger logger)
    {
        logger.LogInformation("Demonstrating agent operations...");

        // Test SQL syntax validation
        var syntaxRequest = new AgentRequest
        {
            Operation = "validate_sql_syntax",
            Parameters = new Dictionary<string, object>
            {
                ["sql"] = "SELECT * FROM Users WHERE Id = @UserId",
                ["checkSecurity"] = true
            }
        };

        var syntaxResponse = await agent.ExecuteAsync(syntaxRequest);
        logger.LogInformation("Syntax validation result: {Success}", syntaxResponse.Success);

        // Test database schema retrieval
        var schemaRequest = new AgentRequest
        {
            Operation = "get_database_schema",
            Parameters = new Dictionary<string, object>
            {
                ["databaseName"] = "MyDatabase",
                ["includeSystemObjects"] = false
            }
        };

        var schemaResponse = await agent.ExecuteAsync(schemaRequest);
        logger.LogInformation("Schema retrieval result: {Success}, Tables: {TableCount}", 
            schemaResponse.Success, schemaResponse.Data.TryGetValue("tableCount", out var tableCount) ? tableCount : 0);

        // Test query execution
        var queryRequest = new AgentRequest
        {
            Operation = "execute_sql_query",
            Parameters = new Dictionary<string, object>
            {
                ["sql"] = "SELECT TOP 10 * FROM sys.tables",
                ["timeoutSeconds"] = 30
            }
        };

        var queryResponse = await agent.ExecuteAsync(queryRequest);
        logger.LogInformation("Query execution result: {Success}, Rows: {RowCount}", 
            queryResponse.Success, 
            queryResponse.Data.TryGetValue("data", out var data) && data is List<object> list ? list.Count : 0);

        // Check agent health
        var health = await agent.GetHealthStatusAsync();
        logger.LogInformation("Agent health: {IsHealthy}, Warnings: {WarningCount}, Errors: {ErrorCount}", 
            health.IsHealthy, health.Warnings.Count, health.Errors.Count);
    }

    private static async Task DemonstrateInlineAgentCreation(
        DeclarativeAgentFactory factory, 
        AgentConfiguration configuration, 
        ILogger logger)
    {
        logger.LogInformation("Demonstrating inline agent creation...");

        // Create agent declaration inline
        var declaration = new AgentDeclaration
        {
            Name = "CustomSqlAgent",
            Version = "1.0.0",
            Description = "Custom SQL agent created inline",
            Extends = "DatabaseExpertAgent", // Inherit from base template
            Personality = new DeclarationPersonality
            {
                ResponseStyle = "friendly_expert",
                ExpertiseLevel = "junior",
                Authoritative = false
            },
            Skills = new List<DeclarationSkill>
            {
                new() { Name = "SqlServerSkill", Version = "1.0.0", Required = true },
                new() { Name = "GitVersioningSkill", Version = "1.0.0", Required = false }
            },
            Configuration = new Dictionary<string, object>
            {
                ["customSetting"] = "inline_configuration_value"
            }
        };

        try
        {
            var customAgent = await factory.CreateAgentAsync(declaration, configuration);
            logger.LogInformation("Successfully created custom agent: {AgentName}", customAgent.Identity.Name);
            
            // Test the custom agent
            var testRequest = new AgentRequest
            {
                Operation = "validate_sql_syntax",
                Parameters = new Dictionary<string, object>
                {
                    ["sql"] = "SELECT COUNT(*) FROM MyTable"
                }
            };

            var response = await customAgent.ExecuteAsync(testRequest);
            logger.LogInformation("Custom agent test result: {Success}", response.Success);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create custom agent (expected - base template not fully implemented)");
        }
    }

    private static AgentConfiguration CreateSampleConfiguration()
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
                        ProhibitedKeywords = new List<string> { "xp_cmdshell", "sp_configure" }
                    }
                }
            },
            Performance = new PerformanceSettings
            {
                ResponseTargets = new Dictionary<string, int>
                {
                    ["syntax_validation"] = 100,
                    ["schema_introspection"] = 500,
                    ["query_optimization"] = 1000
                }
            },
            Plugins = new PluginConfiguration
            {
                PluginDirectories = new List<string> { "plugins", "skills" },
                EnableHotReload = true,
                PluginSettings = new Dictionary<string, object>
                {
                    ["SqlServerSkill"] = new { enabled = true, priority = 100 }
                }
            }
        };
    }
}