using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SqlServerExpertAgent.Console.Commands;

public class ValidateCommand : Command
{
    public ValidateCommand(IServiceProvider services) : base("validate", "Validate SQL syntax and security")
    {
        var sqlArgument = new Argument<string>("sql", "SQL query to validate");
        var securityOption = new Option<bool>("--security", () => true, "Check for security issues");
        
        AddArgument(sqlArgument);
        AddOption(securityOption);

        this.SetHandler(async (sql, checkSecurity) =>
        {
            var agentService = services.GetRequiredService<AgentConsoleService>();
            var logger = services.GetRequiredService<ILogger<ValidateCommand>>();

            try
            {
                logger.LogInformation("Initializing agent...");
                if (!await agentService.InitializeAsync())
                {
                    System.Console.WriteLine("❌ Failed to initialize agent");
                    return;
                }

                logger.LogInformation("Validating SQL...");
                var result = await agentService.ValidateSqlAsync(sql, checkSecurity);
                
                if (result.Success)
                {
                    System.Console.WriteLine("✅ SQL Validation Successful");
                    System.Console.WriteLine(result.Message);
                    if (!string.IsNullOrEmpty(result.Data))
                    {
                        System.Console.WriteLine("\nDetails:");
                        System.Console.WriteLine(result.Data);
                    }
                }
                else
                {
                    System.Console.WriteLine("❌ SQL Validation Failed");
                    System.Console.WriteLine(result.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during SQL validation");
                System.Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }, sqlArgument, securityOption);
    }
}