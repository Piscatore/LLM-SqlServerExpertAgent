using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SqlServerExpertAgent.Console.Commands;

public class QueryCommand : Command
{
    public QueryCommand(IServiceProvider services) : base("query", "Execute SQL query")
    {
        var sqlArgument = new Argument<string>("sql", "SQL query to execute");
        var timeoutOption = new Option<int>("--timeout", () => 30, "Query timeout in seconds");
        
        AddArgument(sqlArgument);
        AddOption(timeoutOption);

        this.SetHandler(async (sql, timeout) =>
        {
            var agentService = services.GetRequiredService<AgentConsoleService>();
            var logger = services.GetRequiredService<ILogger<QueryCommand>>();

            try
            {
                logger.LogInformation("Initializing agent...");
                if (!await agentService.InitializeAsync())
                {
                    System.Console.WriteLine("❌ Failed to initialize agent");
                    return;
                }

                logger.LogInformation("Executing query...");
                var result = await agentService.ExecuteQueryAsync(sql, timeout);
                
                if (result.Success)
                {
                    System.Console.WriteLine("✅ Query Executed Successfully");
                    System.Console.WriteLine(result.Message);
                    if (!string.IsNullOrEmpty(result.Data))
                    {
                        System.Console.WriteLine("\nResults:");
                        System.Console.WriteLine(result.Data);
                    }
                }
                else
                {
                    System.Console.WriteLine("❌ Query Execution Failed");
                    System.Console.WriteLine(result.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during query execution");
                System.Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }, sqlArgument, timeoutOption);
    }
}