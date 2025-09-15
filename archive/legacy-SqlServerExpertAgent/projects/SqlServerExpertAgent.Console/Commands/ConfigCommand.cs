using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SqlServerExpertAgent.Console.Commands;

public class ConfigCommand : Command
{
    public ConfigCommand(IServiceProvider services) : base("config", "Manage agent configuration")
    {
        var showSubCommand = new Command("show", "Show current configuration");
        var validateSubCommand = new Command("validate", "Validate configuration file");
        
        var configPathOption = new Option<string?>("--path", "Configuration file path");
        validateSubCommand.AddOption(configPathOption);

        showSubCommand.SetHandler(async () =>
        {
            var agentService = services.GetRequiredService<AgentConsoleService>();
            var logger = services.GetRequiredService<ILogger<ConfigCommand>>();

            try
            {
                logger.LogInformation("Loading configuration...");
                if (!await agentService.InitializeAsync())
                {
                    System.Console.WriteLine("❌ Failed to initialize agent (configuration may be invalid)");
                    return;
                }

                System.Console.WriteLine("✅ Configuration loaded successfully");
                System.Console.WriteLine("Use health check for detailed status");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading configuration");
                System.Console.WriteLine($"❌ Configuration Error: {ex.Message}");
            }
        });

        validateSubCommand.SetHandler(async (configPath) =>
        {
            var agentService = services.GetRequiredService<AgentConsoleService>();
            var logger = services.GetRequiredService<ILogger<ConfigCommand>>();

            try
            {
                logger.LogInformation("Validating configuration: {ConfigPath}", configPath ?? "default");
                
                var initResult = await agentService.InitializeAsync(configPath);
                if (initResult)
                {
                    System.Console.WriteLine("✅ Configuration is valid");
                    
                    // Run health check for additional validation
                    var healthResult = await agentService.GetHealthStatusAsync();
                    if (healthResult.Success)
                    {
                        System.Console.WriteLine("✅ All plugins healthy");
                        System.Console.WriteLine();
                        System.Console.WriteLine("Configuration Details:");
                        System.Console.WriteLine(healthResult.Message);
                    }
                    else
                    {
                        System.Console.WriteLine("⚠️  Configuration valid but health issues detected:");
                        System.Console.WriteLine(healthResult.Message);
                    }
                }
                else
                {
                    System.Console.WriteLine("❌ Configuration validation failed");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error validating configuration");
                System.Console.WriteLine($"❌ Validation Error: {ex.Message}");
            }
        }, configPathOption);

        AddCommand(showSubCommand);
        AddCommand(validateSubCommand);
    }
}