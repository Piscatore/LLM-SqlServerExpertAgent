using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SqlServerExpertAgent.Console.Commands;

public class OptimizeCommand : Command
{
    public OptimizeCommand(IServiceProvider services) : base("optimize", "Analyze and optimize SQL query performance")
    {
        var sqlArgument = new Argument<string>("sql", "SQL query to analyze");
        
        AddArgument(sqlArgument);

        this.SetHandler(async (sql) =>
        {
            var agentService = services.GetRequiredService<AgentConsoleService>();
            var logger = services.GetRequiredService<ILogger<OptimizeCommand>>();

            try
            {
                logger.LogInformation("Initializing agent...");
                if (!await agentService.InitializeAsync())
                {
                    System.Console.WriteLine("❌ Failed to initialize agent");
                    return;
                }

                logger.LogInformation("Analyzing query performance...");
                var result = await agentService.AnalyzePerformanceAsync(sql);
                
                if (result.Success)
                {
                    System.Console.WriteLine("✅ Performance Analysis Complete");
                    System.Console.WriteLine(result.Message);
                    if (!string.IsNullOrEmpty(result.Data))
                    {
                        System.Console.WriteLine("\nOptimization Recommendations:");
                        System.Console.WriteLine(result.Data);
                    }
                }
                else
                {
                    System.Console.WriteLine("❌ Performance Analysis Failed");
                    System.Console.WriteLine(result.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during performance analysis");
                System.Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }, sqlArgument);
    }
}