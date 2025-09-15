using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlServerExpertAgent.Configuration;
using SqlServerExpertAgent.Console.Commands;
using SqlServerExpertAgent.Plugins;
using System.CommandLine;

namespace SqlServerExpertAgent.Console;

/// <summary>
/// Interactive console application for SQL Server Expert Agent
/// Provides direct human interface for testing, debugging, and standalone usage
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Create host for dependency injection and logging
        var host = CreateHostBuilder(args).Build();
        
        try
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Starting SQL Server Expert Agent Console");

            // Create root command
            var rootCommand = CreateRootCommand(host.Services);
            
            // Execute command line
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetService<ILogger<Program>>();
            logger?.LogError(ex, "Fatal error in SQL Server Expert Agent Console");
            System.Console.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, services) =>
            {
                // Register core services
                services.AddSingleton<ConfigurationManager>();
                
                // Register console-specific services  
                services.AddSingleton<AgentConsoleService>();
                services.AddSingleton<InteractiveShell>();
            });

    private static RootCommand CreateRootCommand(IServiceProvider services)
    {
        var configOption = new Option<string>("--config", "Configuration file path");
        var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");
        var interactiveOption = new Option<bool>("--interactive", "Start interactive shell") { IsRequired = false };
        
        var rootCommand = new RootCommand("SQL Server Expert Agent - Interactive Console")
        {
            configOption,
            verboseOption,
            interactiveOption
        };

        // Add commands
        rootCommand.AddCommand(new ValidateCommand(services));
        rootCommand.AddCommand(new QueryCommand(services));
        rootCommand.AddCommand(new SchemaCommand(services));
        rootCommand.AddCommand(new OptimizeCommand(services));
        rootCommand.AddCommand(new TestCommand(services));
        rootCommand.AddCommand(new ConfigCommand(services));

        // Set default handler for interactive mode
        rootCommand.SetHandler(async (configPath, verbose, interactive) =>
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            
            if (verbose)
            {
                logger.LogInformation("Verbose logging enabled");
            }

            if (interactive)
            {
                logger.LogInformation("Starting interactive shell...");
                var shell = services.GetRequiredService<InteractiveShell>();
                await shell.RunAsync();
            }
            else
            {
                // Show help if no specific command provided
                System.Console.WriteLine("SQL Server Expert Agent Console");
                System.Console.WriteLine("Use --help for available commands or --interactive for shell mode");
            }
        }, 
        configOption, 
        verboseOption, 
        interactiveOption);

        return rootCommand;
    }
}