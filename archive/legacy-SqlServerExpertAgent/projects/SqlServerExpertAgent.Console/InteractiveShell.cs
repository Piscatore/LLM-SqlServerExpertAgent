using Microsoft.Extensions.Logging;

namespace SqlServerExpertAgent.Console;

/// <summary>
/// Interactive shell for SQL Server Expert Agent
/// Provides REPL-style interface for agent interaction
/// </summary>
public class InteractiveShell
{
    private readonly AgentConsoleService _agentService;
    private readonly ILogger<InteractiveShell> _logger;
    private bool _isRunning = false;
    private bool _agentInitialized = false;

    public InteractiveShell(AgentConsoleService agentService, ILogger<InteractiveShell> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _isRunning = true;
        ShowWelcomeMessage();

        while (_isRunning)
        {
            try
            {
                System.Console.Write("sql-expert> ");
                var input = System.Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                await ProcessCommand(input.Trim());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command");
                System.Console.WriteLine($"❌ Error: {ex.Message}");
            }
        }

        System.Console.WriteLine("Goodbye! 👋");
    }

    private void ShowWelcomeMessage()
    {
        System.Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        System.Console.WriteLine("║              SQL Server Expert Agent - Interactive Shell    ║");
        System.Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        System.Console.WriteLine();
        System.Console.WriteLine("Available commands:");
        System.Console.WriteLine("  init [config-path]       - Initialize the agent");
        System.Console.WriteLine("  validate <sql>           - Validate SQL syntax and security");
        System.Console.WriteLine("  query <sql>              - Execute SQL query");
        System.Console.WriteLine("  schema <database>        - Get database schema");
        System.Console.WriteLine("  optimize <sql>           - Analyze query performance");
        System.Console.WriteLine("  test                     - Run comprehensive tests");
        System.Console.WriteLine("  health                   - Check agent health status");
        System.Console.WriteLine("  help                     - Show this help message");
        System.Console.WriteLine("  exit / quit              - Exit the shell");
        System.Console.WriteLine();
        System.Console.WriteLine("Type 'init' to initialize the agent, then start using SQL commands.");
        System.Console.WriteLine();
    }

    private async Task ProcessCommand(string input)
    {
        var parts = ParseCommand(input);
        var command = parts[0].ToLower();

        switch (command)
        {
            case "init":
                await InitializeAgent(parts.Length > 1 ? parts[1] : null);
                break;

            case "validate":
                if (!EnsureAgentReady()) return;
                await ValidateSql(string.Join(" ", parts.Skip(1)));
                break;

            case "query":
                if (!EnsureAgentReady()) return;
                await ExecuteQuery(string.Join(" ", parts.Skip(1)));
                break;

            case "schema":
                if (!EnsureAgentReady()) return;
                await GetSchema(parts.Length > 1 ? parts[1] : "master");
                break;

            case "optimize":
                if (!EnsureAgentReady()) return;
                await OptimizeQuery(string.Join(" ", parts.Skip(1)));
                break;

            case "test":
                if (!EnsureAgentReady()) return;
                await RunTests();
                break;

            case "health":
                if (!EnsureAgentReady()) return;
                await CheckHealth();
                break;

            case "help":
                ShowWelcomeMessage();
                break;

            case "exit":
            case "quit":
                _isRunning = false;
                break;

            default:
                System.Console.WriteLine($"❓ Unknown command: {command}");
                System.Console.WriteLine("Type 'help' for available commands.");
                break;
        }
    }

    private static string[] ParseCommand(string input)
    {
        var parts = new List<string>();
        var current = "";
        var inQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            
            if (c == '"' || c == '\'')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    parts.Add(current);
                    current = "";
                }
            }
            else
            {
                current += c;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            parts.Add(current);
        }

        return parts.ToArray();
    }

    private async Task InitializeAgent(string? configPath)
    {
        try
        {
            System.Console.WriteLine("🔄 Initializing SQL Server Expert Agent...");
            
            var result = await _agentService.InitializeAsync(configPath);
            
            if (result)
            {
                _agentInitialized = true;
                System.Console.WriteLine("✅ Agent initialized successfully!");
                
                // Show basic health info
                var healthResult = await _agentService.GetHealthStatusAsync();
                if (healthResult.Success)
                {
                    System.Console.WriteLine("✅ All plugins loaded and healthy");
                }
                else
                {
                    System.Console.WriteLine("⚠️  Agent initialized but some plugins have issues");
                }
            }
            else
            {
                System.Console.WriteLine("❌ Failed to initialize agent. Check configuration and logs.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing agent");
            System.Console.WriteLine($"❌ Initialization error: {ex.Message}");
        }
    }

    private bool EnsureAgentReady()
    {
        if (!_agentInitialized)
        {
            System.Console.WriteLine("⚠️  Agent not initialized. Use 'init' command first.");
            return false;
        }
        return true;
    }

    private async Task ValidateSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            System.Console.WriteLine("❓ Please provide SQL to validate.");
            return;
        }

        try
        {
            System.Console.WriteLine($"🔍 Validating SQL: {TruncateForDisplay(sql)}");
            var result = await _agentService.ValidateSqlAsync(sql);
            
            System.Console.WriteLine(result.Success ? "✅ SQL is valid" : "❌ SQL validation failed");
            System.Console.WriteLine(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating SQL");
            System.Console.WriteLine($"❌ Validation error: {ex.Message}");
        }
    }

    private async Task ExecuteQuery(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            System.Console.WriteLine("❓ Please provide SQL to execute.");
            return;
        }

        try
        {
            System.Console.WriteLine($"⚡ Executing query: {TruncateForDisplay(sql)}");
            var result = await _agentService.ExecuteQueryAsync(sql);
            
            System.Console.WriteLine(result.Success ? "✅ Query executed successfully" : "❌ Query execution failed");
            System.Console.WriteLine(result.Message);
            
            if (!string.IsNullOrEmpty(result.Data))
            {
                System.Console.WriteLine();
                System.Console.WriteLine("Results:");
                System.Console.WriteLine(result.Data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing query");
            System.Console.WriteLine($"❌ Execution error: {ex.Message}");
        }
    }

    private async Task GetSchema(string database)
    {
        try
        {
            System.Console.WriteLine($"📋 Retrieving schema for database: {database}");
            var result = await _agentService.GetSchemaAsync(database);
            
            System.Console.WriteLine(result.Success ? $"✅ Schema retrieved for '{database}'" : "❌ Schema retrieval failed");
            System.Console.WriteLine(result.Message);
            
            if (!string.IsNullOrEmpty(result.Data))
            {
                System.Console.WriteLine();
                System.Console.WriteLine("Schema Details:");
                System.Console.WriteLine(result.Data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schema");
            System.Console.WriteLine($"❌ Schema error: {ex.Message}");
        }
    }

    private async Task OptimizeQuery(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            System.Console.WriteLine("❓ Please provide SQL to optimize.");
            return;
        }

        try
        {
            System.Console.WriteLine($"⚡ Analyzing performance: {TruncateForDisplay(sql)}");
            var result = await _agentService.AnalyzePerformanceAsync(sql);
            
            System.Console.WriteLine(result.Success ? "✅ Performance analysis complete" : "❌ Performance analysis failed");
            System.Console.WriteLine(result.Message);
            
            if (!string.IsNullOrEmpty(result.Data))
            {
                System.Console.WriteLine();
                System.Console.WriteLine("Optimization Recommendations:");
                System.Console.WriteLine(result.Data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing performance");
            System.Console.WriteLine($"❌ Analysis error: {ex.Message}");
        }
    }

    private async Task RunTests()
    {
        try
        {
            System.Console.WriteLine("🧪 Running comprehensive agent tests...");
            var result = await _agentService.RunTestsAsync();
            
            System.Console.WriteLine(result.Success ? "✅ Test suite completed" : "❌ Test suite failed");
            System.Console.WriteLine();
            System.Console.WriteLine(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running tests");
            System.Console.WriteLine($"❌ Test error: {ex.Message}");
        }
    }

    private async Task CheckHealth()
    {
        try
        {
            System.Console.WriteLine("🏥 Checking agent health...");
            var result = await _agentService.GetHealthStatusAsync();
            
            System.Console.WriteLine(result.Success ? "✅ Health check complete" : "❌ Health check failed");
            System.Console.WriteLine();
            System.Console.WriteLine(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health");
            System.Console.WriteLine($"❌ Health check error: {ex.Message}");
        }
    }

    private static string TruncateForDisplay(string text, int maxLength = 80)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength - 3) + "...";
    }
}