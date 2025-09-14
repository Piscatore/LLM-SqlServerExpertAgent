using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SqlServerExpertAgent.Console.Commands;

public class TestCommand : Command
{
    public TestCommand(IServiceProvider services) : base("test", "Run comprehensive agent tests")
    {
        this.SetHandler(async () =>
        {
            var agentService = services.GetRequiredService<AgentConsoleService>();
            var logger = services.GetRequiredService<ILogger<TestCommand>>();

            try
            {
                logger.LogInformation("Initializing agent for testing...");
                if (!await agentService.InitializeAsync())
                {
                    System.Console.WriteLine("❌ Failed to initialize agent");
                    return;
                }

                System.Console.WriteLine("🧪 Running comprehensive agent tests...");
                System.Console.WriteLine();
                
                var result = await agentService.RunTestsAsync();
                
                if (result.Success)
                {
                    System.Console.WriteLine("✅ Test Suite Completed");
                    System.Console.WriteLine();
                    System.Console.WriteLine(result.Message);
                }
                else
                {
                    System.Console.WriteLine("❌ Test Suite Failed");
                    System.Console.WriteLine();
                    System.Console.WriteLine(result.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during test execution");
                System.Console.WriteLine($"❌ Error: {ex.Message}");
            }
        });
    }
}