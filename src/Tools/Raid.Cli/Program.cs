using System.CommandLine;

namespace Raid.Cli;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("RAID Platform CLI - Rapid AI Development Platform");

        var agentCommand = new Command("agent", "Agent management commands");
        var listCommand = new Command("list", "List available agents");
        var createCommand = new Command("create", "Create new agent from template");

        agentCommand.AddCommand(listCommand);
        agentCommand.AddCommand(createCommand);
        rootCommand.AddCommand(agentCommand);

        listCommand.SetHandler(() =>
        {
            Console.WriteLine("Available RAID Platform Agents:");
            Console.WriteLine("- Database Agent (Raid.Agents.Database)");
            Console.WriteLine("- Memory Agent (Raid.Memory) - Coming Soon");
            Console.WriteLine("- Security Agent (Raid.Security) - Coming Soon");
            Console.WriteLine("- Orchestrator Agent (Raid.Orchestrator) - Coming Soon");
            Console.WriteLine("- Analytics Agent (Raid.Analytics) - Coming Soon");
        });

        createCommand.SetHandler(() =>
        {
            Console.WriteLine("Agent creation from templates - Coming Soon");
            Console.WriteLine("This will use the Raid.Templates library for declarative agent creation.");
        });

        return await rootCommand.InvokeAsync(args);
    }
}