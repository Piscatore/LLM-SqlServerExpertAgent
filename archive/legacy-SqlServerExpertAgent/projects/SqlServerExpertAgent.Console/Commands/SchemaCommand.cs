using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SqlServerExpertAgent.Console.Commands;

public class SchemaCommand : Command
{
    public SchemaCommand(IServiceProvider services) : base("schema", "Retrieve database schema information")
    {
        var databaseArgument = new Argument<string>("database", "Database name");
        var systemOption = new Option<bool>("--system", () => false, "Include system objects");
        
        AddArgument(databaseArgument);
        AddOption(systemOption);

        this.SetHandler(async (database, includeSystem) =>
        {
            var agentService = services.GetRequiredService<AgentConsoleService>();
            var logger = services.GetRequiredService<ILogger<SchemaCommand>>();

            try
            {
                logger.LogInformation("Initializing agent...");
                if (!await agentService.InitializeAsync())
                {
                    System.Console.WriteLine("❌ Failed to initialize agent");
                    return;
                }

                logger.LogInformation("Retrieving schema for database: {Database}", database);
                var result = await agentService.GetSchemaAsync(database, includeSystem);
                
                if (result.Success)
                {
                    System.Console.WriteLine($"✅ Schema Retrieved for '{database}'");
                    System.Console.WriteLine(result.Message);
                    if (!string.IsNullOrEmpty(result.Data))
                    {
                        System.Console.WriteLine("\nSchema Details:");
                        System.Console.WriteLine(result.Data);
                    }
                }
                else
                {
                    System.Console.WriteLine("❌ Schema Retrieval Failed");
                    System.Console.WriteLine(result.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during schema retrieval");
                System.Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }, databaseArgument, systemOption);
    }
}