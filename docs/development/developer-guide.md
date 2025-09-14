# Developer Guide - SQL Server Expert Agent

*A comprehensive guide for mid-level .NET developers with basic AI/LLM experience*

## Table of Contents
1. [Getting Started](#getting-started)
2. [Architecture Deep Dive](#architecture-deep-dive)
3. [Plugin Development](#plugin-development)
4. [Configuration System](#configuration-system)
5. [Testing Strategy](#testing-strategy)
6. [AI Integration with Semantic Kernel](#ai-integration)
7. [Troubleshooting](#troubleshooting)

## Getting Started

### Prerequisites
- .NET 9.0 SDK or later
- Visual Studio 2022 or VS Code with C# extension
- SQL Server instance (LocalDB, SQL Server Express, or full SQL Server)
- Basic understanding of Microsoft Semantic Kernel concepts
- Familiarity with dependency injection and configuration patterns

### Development Environment Setup

1. **Clone and Build**
   ```bash
   git clone <repository-url>
   cd LLM-SqlServerExpertAgent
   dotnet restore
   dotnet build
   ```

2. **Run Tests to Verify Setup**
   ```bash
   dotnet test
   # Expected: All 54+ tests should pass
   ```

3. **Configure Development Database**
   
   Create or update `SqlServerExpertAgent.Console/appsettings.json`:
   ```json
   {
     "SqlServer": {
       "DefaultDatabase": "AgentSandbox",
       "ConnectionStrings": {
         "default": "Server=.;Database=AgentSandbox;Integrated Security=true;TrustServerCertificate=true;"
       },
       "QueryExecution": {
         "Safety": {
           "AllowDataModification": true,
           "MaxRowsReturned": 1000,
           "DefaultTimeout": 30
         }
       }
     }
   }
   ```

4. **First Run**
   ```bash
   cd SqlServerExpertAgent.Console
   dotnet run -- --interactive
   ```

## Architecture Deep Dive

### Hybrid Console + Core Architecture

The solution uses a hybrid architecture that separates concerns while maintaining cohesion:

```
┌─────────────────────────────────────────────┐
│         SqlServerExpertAgent.Console        │  ← Entry Point & CLI
│                                             │
│ ┌─────────────┐  ┌──────────────────────┐  │
│ │ Interactive │  │ Direct Commands      │  │
│ │ Shell       │  │ (validate, query,    │  │
│ │             │  │  schema, optimize)   │  │
│ └─────────────┘  └──────────────────────┘  │
└─────────────────┬───────────────────────────┘
                  │
┌─────────────────▼───────────────────────────┐
│         SqlServerExpertAgent.Core           │  ← Engine & Plugins
│                                             │
│ ┌─────────────┐  ┌─────────────────────────┐│
│ │ Agent       │  │ Plugin Manager          ││
│ │ Console     │  │                         ││
│ │ Service     │  │ ┌─────────────────────┐ ││
│ └─────────────┘  │ │ SqlServerPlugin     │ ││
│                  │ │ (Built-in SMO)      │ ││
│ ┌─────────────┐  │ └─────────────────────┘ ││
│ │ Config      │  │ ┌─────────────────────┐ ││
│ │ Manager     │  │ │ GitSchemaPlugin     │ ││
│ └─────────────┘  │ │ (Version Control)   │ ││
│                  │ └─────────────────────┘ ││
│                  └─────────────────────────┘│
└─────────────────┬───────────────────────────┘
                  │
┌─────────────────▼───────────────────────────┐
│              SQL Server                     │  ← Data Layer
│        (via SMO Integration)               │
└─────────────────────────────────────────────┘
```

### Core Components Explained

#### 1. AgentConsoleService (The Orchestrator)
**Location**: `SqlServerExpertAgent.Console/AgentConsoleService.cs`

The central service that manages the agent lifecycle:

```csharp
public class AgentConsoleService
{
    // Core orchestration logic
    public async Task<bool> InitializeAsync(string? configPath = null)
    {
        // 1. Load configuration from multiple sources
        _configuration = await LoadConfigurationAsync();
        
        // 2. Create and configure Semantic Kernel
        var builder = Kernel.CreateBuilder();
        _kernel = builder.Build();
        
        // 3. Load and initialize plugins
        _pluginManager = new PluginManager(_configuration, _serviceProvider, _logger);
        var pluginResult = await _pluginManager.LoadPluginsAsync(_kernel);
        
        // 4. Register plugin functions with Kernel
        // All SqlServerPlugin functions are now available to AI
    }
}
```

**Key Responsibilities**:
- Agent lifecycle management (initialize, dispose)
- Plugin coordination and health monitoring
- Semantic Kernel orchestration
- Error handling and logging coordination

#### 2. PluginManager (Dynamic Loading System)
**Location**: `SqlServerExpertAgent.Core/Plugins/PluginManager.cs`

Handles dynamic plugin loading with assembly isolation:

```csharp
public async Task<PluginLoadResult> LoadPluginsAsync(Kernel kernel)
{
    // 1. Load built-in plugins first
    await LoadBuiltInPluginsAsync(results);
    
    // 2. Scan plugin directories for external plugins
    foreach (var pluginDirectory in _configuration.Plugins.PluginDirectories)
    {
        var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll");
        // Load with assembly isolation
    }
    
    // 3. Resolve dependencies and initialize
    var dependencyOrder = ResolveDependencyOrder();
    foreach (var pluginName in dependencyOrder)
    {
        await loadedPlugin.Plugin.InitializeAsync(_configuration, _serviceProvider);
        loadedPlugin.Plugin.RegisterKernelFunctions(kernel);
    }
}
```

**Advanced Features**:
- **Assembly Isolation**: Each plugin loads in separate `AssemblyLoadContext`
- **Dependency Resolution**: Topological sorting for initialization order
- **Hot-Reload**: Development-time plugin updates (configurable)
- **Health Monitoring**: Individual plugin diagnostics

#### 3. SqlServerPlugin (The SMO Expert)
**Location**: `SqlServerExpertAgent.Core/Plugins/SqlServerPlugin.cs`

The core SQL Server integration using SMO (SQL Server Management Objects):

```csharp
[KernelFunction("ValidateSqlSyntax")]
public async Task<string> ValidateSqlSyntaxAsync(
    [Description("SQL query to validate")] string sql,
    [Description("Check for security issues")] bool checkSecurity = true)
{
    // Security check first
    if (!_configuration!.SqlServer.QueryExecution.Safety.AllowDataModification && 
        HasDataModification(sql))
    {
        return "Error: Data modification queries are not allowed in current configuration";
    }
    
    // Use SMO for syntax validation WITHOUT execution
    using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync();
    
    using var command = new SqlCommand("SET SHOWPLAN_XML ON", connection);
    await command.ExecuteNonQueryAsync();
    
    // Parse without executing - this validates syntax
    using var validateCommand = new SqlCommand(sql, connection);
    var reader = await validateCommand.ExecuteReaderAsync();
    
    return "SQL syntax is valid";
}
```

**SMO Integration Benefits**:
- **Headless SSMS**: All SQL Server Management Studio functionality programmatically
- **No SQL Execution**: Syntax validation without running dangerous queries
- **Rich Metadata**: Complete database schema information
- **Performance Analysis**: Execution plan analysis and optimization suggestions

### Configuration System Architecture

The configuration system supports multiple sources with proper precedence:

```
Command Line Args (Highest Priority)
         ↓
Environment Variables
         ↓
appsettings.{Environment}.json
         ↓
appsettings.json (Lowest Priority)
```

**Example Configuration Structure**:
```json
{
  "Identity": {
    "Name": "SqlServerExpertAgent",
    "Personality": {
      "response_style": "expert_concise",
      "authoritative": true
    }
  },
  "SqlServer": {
    "DefaultDatabase": "AgentSandbox",
    "ConnectionStrings": {
      "default": "Server=.;Database=AgentSandbox;...",
      "development": "Server=.;Database=AgentSandbox_Dev;...",
      "testing": "Server=.;Database=AgentSandbox_Test;..."
    },
    "QueryExecution": {
      "AllowExecutionPlanAnalysis": true,
      "Safety": {
        "AllowDataModification": false,  // ← Critical safety setting
        "MaxRowsReturned": 1000,
        "DefaultTimeout": 30,
        "ProhibitedKeywords": ["xp_cmdshell", "sp_configure"]
      }
    }
  }
}
```

## Plugin Development

### Creating Custom Plugins

1. **Implement IAgentPlugin Interface**:

```csharp
public class MyCustomPlugin : IAgentPlugin
{
    public PluginMetadata Metadata => new(
        "MyCustomPlugin", 
        new Version(1, 0, 0),
        "Custom functionality for specialized operations",
        new List<string>(), // Dependencies
        PluginCapabilities.Custom,
        new Dictionary<string, object>
        {
            ["CustomProperty"] = "CustomValue"
        });

    private AgentConfiguration? _configuration;
    private IServiceProvider? _serviceProvider;

    public async Task InitializeAsync(
        AgentConfiguration configuration, 
        IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        
        // Initialize plugin resources
        // Connect to external systems
        // Validate configuration
    }

    public void RegisterKernelFunctions(Kernel kernel)
    {
        // Register functions that AI can call
        kernel.Plugins.AddFromObject(this, "MyPlugin");
    }

    // Semantic Kernel Functions
    [KernelFunction("CustomOperation")]
    [Description("Performs a custom operation with AI assistance")]
    public async Task<string> CustomOperationAsync(
        [Description("Input parameter")] string input)
    {
        // Your custom logic here
        return $"Processed: {input}";
    }

    public async Task<PluginHealthStatus> GetHealthStatusAsync()
    {
        return new PluginHealthStatus
        {
            IsHealthy = true,
            Status = "Healthy",
            Metrics = new Dictionary<string, object>
            {
                ["LastOperation"] = DateTime.UtcNow
            }
        };
    }

    public async Task DisposeAsync()
    {
        // Clean up resources
    }
}
```

2. **Plugin Assembly Structure**:
```
MyPlugin.dll
├── MyCustomPlugin.cs (implements IAgentPlugin)
├── Models/ (supporting classes)
└── Services/ (business logic)
```

3. **Deployment**:
```bash
# Copy to plugins directory
cp MyPlugin.dll SqlServerExpertAgent.Console/plugins/

# Plugin will be automatically discovered and loaded
dotnet run -- --interactive
```

### Advanced Plugin Features

#### Dependency Management
```csharp
public PluginMetadata Metadata => new(
    "AdvancedPlugin",
    new Version(1, 0, 0),
    "Advanced plugin with dependencies",
    new List<string> { "SqlServerPlugin" }, // ← Dependency on SQL Server plugin
    PluginCapabilities.SqlQuery | PluginCapabilities.Custom);
```

#### Hot-Reload Support
```json
{
  "Plugins": {
    "EnableHotReload": true,  // ← Development only
    "PluginDirectories": ["plugins", "extensions"]
  }
}
```

## AI Integration with Semantic Kernel

### Understanding the AI Flow

The agent uses Microsoft Semantic Kernel for AI orchestration:

```
User Input → Agent → Semantic Kernel → Plugin Functions → SMO → SQL Server
```

**Example AI Integration**:
```csharp
// This is how the AI uses your plugin functions
var kernel = Kernel.CreateBuilder().Build();

// Plugin functions are registered automatically
plugin.RegisterKernelFunctions(kernel);

// AI can now call ValidateSqlSyntax, ExecuteSqlQuery, etc.
var result = await kernel.InvokeAsync(
    "SqlServer", 
    "ValidateSqlSyntax", 
    new KernelArguments 
    { 
        ["sql"] = "SELECT * FROM Users",
        ["checkSecurity"] = true 
    });
```

### Function Calling Best Practices

1. **Use Descriptive Attributes**:
```csharp
[KernelFunction("GetDatabaseSchema")]
[Description("Retrieves comprehensive database schema information including tables, views, indexes, and relationships")]
public async Task<string> GetDatabaseSchemaAsync(
    [Description("Database name to analyze")] string databaseName,
    [Description("Include system tables and views")] bool includeSystemObjects = false)
```

2. **Provide Rich Error Context**:
```csharp
try
{
    // Operation logic
    return "Success: Operation completed";
}
catch (SqlException ex)
{
    // Provide context AI can understand and act on
    return $"SQL Error {ex.Number}: {ex.Message}. " +
           $"Suggested action: Check table existence and permissions.";
}
```

3. **Return Structured Information**:
```csharp
[KernelFunction("AnalyzePerformance")]
public async Task<string> AnalyzePerformanceAsync(string sql)
{
    var analysis = await PerformAnalysis(sql);
    
    // Return structured JSON that AI can interpret
    return JsonSerializer.Serialize(new
    {
        Status = "Analyzed",
        ExecutionTime = analysis.ExecutionTime,
        Recommendations = analysis.Recommendations,
        IndexSuggestions = analysis.IndexSuggestions
    }, new JsonSerializerOptions { WriteIndented = true });
}
```

## Testing Strategy

### Test Architecture

The project uses a comprehensive testing strategy with multiple layers:

```
Unit Tests (Fast, Isolated)
├── Configuration Tests
├── Plugin Loading Tests
├── Command Parsing Tests
└── Business Logic Tests

Integration Tests (Slower, Real Dependencies)
├── SQL Server Connectivity Tests
├── SMO Integration Tests
├── End-to-End Workflow Tests
└── Performance Validation Tests
```

### Writing Effective Tests

1. **Unit Test Example**:
```csharp
[Fact]
public async Task SqlServerPlugin_ValidateSyntax_ValidSql_ReturnsSuccess()
{
    // Arrange
    var config = CreateTestConfiguration();
    var plugin = new SqlServerPlugin();
    var mockServiceProvider = new Mock<IServiceProvider>();
    
    await plugin.InitializeAsync(config, mockServiceProvider.Object);
    
    // Act
    var result = await plugin.ValidateSqlSyntaxAsync("SELECT 1 as TestColumn");
    
    // Assert
    result.Should().Contain("valid");
}
```

2. **Integration Test Example**:
```csharp
[Fact]
public async Task AgentConsoleService_FullWorkflow_CreatesTablesSuccessfully()
{
    // Arrange
    var agentService = _serviceProvider.GetRequiredService<AgentConsoleService>();
    await agentService.InitializeAsync();
    
    // Act
    var createResult = await agentService.ExecuteQueryAsync(
        "CREATE TABLE TestUsers (Id INT PRIMARY KEY, Name NVARCHAR(50))");
    var validateResult = await agentService.ValidateSqlAsync(
        "SELECT * FROM TestUsers");
    
    // Assert
    createResult.Success.Should().BeTrue();
    validateResult.Success.Should().BeTrue();
    
    // Cleanup
    await agentService.ExecuteQueryAsync("DROP TABLE TestUsers");
}
```

3. **Performance Test Example**:
```csharp
[Fact]
public async Task SqlServerPlugin_ValidateSyntax_MeetsPerformanceTarget()
{
    // Arrange
    var plugin = CreateConfiguredPlugin();
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    await plugin.ValidateSqlSyntaxAsync("SELECT COUNT(*) FROM sys.tables");
    stopwatch.Stop();
    
    // Assert - Must meet 100ms target
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
}
```

### Test Configuration Management

Create test-specific configurations:

```csharp
private static AgentConfiguration CreateTestConfiguration()
{
    return new AgentConfiguration
    {
        SqlServer = new SqlServerConfiguration
        {
            ConnectionStrings = new Dictionary<string, string>
            {
                ["default"] = "Server=(localdb)\\MSSQLLocalDB;Database=TestDB;Integrated Security=true;"
            },
            QueryExecution = new QueryExecutionConfiguration
            {
                Safety = new QuerySafetyConfiguration
                {
                    AllowDataModification = true, // ← Allow for testing
                    MaxRowsReturned = 100,
                    DefaultTimeout = 10
                }
            }
        }
    };
}
```

## Troubleshooting

### Common Issues and Solutions

#### 1. Plugin Loading Failures
**Symptom**: "Plugin loading results: 0 successful, 1 failed"
**Causes & Solutions**:
- **Missing Dependencies**: Ensure all plugin dependencies are in the output directory
- **Configuration Issues**: Check plugin-specific configuration in appsettings.json
- **Assembly Loading**: Verify plugin assembly targets correct .NET version

**Debug Steps**:
```csharp
// Enable detailed plugin logging
services.AddLogging(builder => 
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
});
```

#### 2. Database Connection Issues
**Symptom**: "Failed to connect to server"
**Solutions**:
```json
{
  "SqlServer": {
    "ConnectionStrings": {
      "default": "Server=.;Database=AgentSandbox;Integrated Security=true;TrustServerCertificate=true;Encrypt=false;"
    }
  }
}
```

#### 3. SMO Integration Problems
**Symptom**: "Object reference not set to an instance of an object" from SMO
**Common Causes**:
- SQL Server instance not running
- Insufficient permissions on database
- SMO version compatibility

**Debug Approach**:
```csharp
// Test SMO connectivity directly
using var server = new Server(".");
try 
{
    server.Refresh();
    Console.WriteLine($"Connected to: {server.Name} - {server.Edition}");
}
catch (Exception ex)
{
    Console.WriteLine($"SMO Error: {ex.Message}");
}
```

#### 4. Configuration Validation Failures
**Symptom**: "Configuration validation failed"
**Debug Process**:
```csharp
// Add configuration validation logging
public async Task<AgentConfiguration?> LoadConfigurationAsync()
{
    try 
    {
        var config = await LoadFromSources();
        ValidateConfiguration(config); // ← Add detailed validation
        return config;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Configuration loading failed");
        // Log exact configuration that failed
        return null;
    }
}
```

### Development Tools and Debugging

#### 1. Enable Detailed Logging
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "SqlServerExpertAgent": "Trace"
    }
  }
}
```

#### 2. SQL Server Profiler Integration
Monitor actual SQL being generated:
```sql
-- Start trace to see SMO-generated SQL
SELECT 
    application_name,
    host_name,
    login_name,
    text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle)
WHERE application_name LIKE '%SMO%'
```

#### 3. Performance Monitoring
```csharp
// Add performance counters
public async Task<string> ValidateSqlSyntaxAsync(string sql)
{
    using var activity = ActivitySource.StartActivity("ValidateSqlSyntax");
    var stopwatch = Stopwatch.StartNew();
    
    try 
    {
        var result = await PerformValidation(sql);
        activity?.SetTag("success", true);
        return result;
    }
    finally
    {
        stopwatch.Stop();
        activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
        _logger.LogDebug("Validation took {Duration}ms", stopwatch.ElapsedMilliseconds);
    }
}
```

## Best Practices Summary

### Architecture
- Keep plugins focused on single responsibility
- Use dependency injection consistently
- Implement comprehensive error handling
- Follow async/await patterns throughout

### AI Integration
- Provide rich, descriptive function metadata
- Return structured, actionable results
- Include context in error messages
- Design functions for AI decision-making

### Testing
- Write tests before implementation (TDD approach)
- Test both happy path and error conditions
- Include performance validation
- Use integration tests for end-to-end validation

### Configuration
- Support multiple environments
- Validate configuration at startup
- Provide sensible defaults
- Document all configuration options

### Security
- Never trust user input - always validate
- Use parameterized queries exclusively
- Implement comprehensive audit logging
- Follow principle of least privilege

---

This guide provides the foundation for developing with the SQL Server Expert Agent. For specific implementation questions, refer to the comprehensive test suite and existing plugin implementations as working examples.