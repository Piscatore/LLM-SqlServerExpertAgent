# RAID Platform Developer Guide

**Comprehensive development guide for the Rapid AI Development Platform (RAID Platform)**

*A complete resource for platform developers, agent developers, and system architects building enterprise multi-agent AI ecosystems*

## Table of Contents
1. [Getting Started](#getting-started)
2. [Platform Architecture Deep Dive](#platform-architecture-deep-dive)
3. [Agent Development](#agent-development)
4. [A2A Communication Integration](#a2a-communication-integration)
5. [Memory Agent Integration](#memory-agent-integration)
6. [Configuration Management](#configuration-management)
7. [Testing Strategies](#testing-strategies)
8. [Deployment and DevOps](#deployment-and-devops)
9. [Performance Optimization](#performance-optimization)
10. [Troubleshooting](#troubleshooting)

## Getting Started

### Prerequisites

#### Development Environment
- **.NET SDK**: 9.0 or later (LTS recommended)
- **IDE**: Visual Studio 2022 17.8+, VS Code with C# Dev Kit, or JetBrains Rider 2024.1+
- **Container Runtime**: Docker Desktop 4.20+ or Podman 4.5+
- **Database**: SQL Server 2022+ (Express, Developer, or Standard edition)
- **Cache**: Redis 7.0+ (local installation or Docker container)

#### Platform Knowledge Requirements
- **C# and .NET**: Intermediate to advanced proficiency
- **Microsoft Semantic Kernel**: Basic understanding of function calling and plugins
- **Dependency Injection**: Experience with .NET DI container patterns
- **Async Programming**: Understanding of async/await and Task-based operations
- **Enterprise Patterns**: Familiarity with microservices, messaging, and distributed systems

### Quick Platform Setup

#### Option 1: Docker Compose (Recommended for Development)

1. **Clone and Initialize Platform**
   ```bash
   git clone <repository-url>
   cd LLM-SqlServerExpertAgent

   # Start all infrastructure services
   docker-compose -f docker/docker-compose.ci.yml up -d

   # Verify services are running
   docker-compose ps
   ```

2. **Build and Run Platform**
   ```bash
   # Restore dependencies and build
   dotnet restore
   dotnet build --configuration Debug

   # Run tests to verify setup
   dotnet test
   ```

3. **Start Individual Agents**
   ```bash
   # Terminal 1: Start Memory Agent
   cd src/Infrastructure/Raid.Memory
   dotnet run --environment Development

   # Terminal 2: Start Database Agent
   cd src/Agents/Raid.Agents.Database/Raid.Agents.Database.Console
   dotnet run -- --interactive --environment Development
   ```

#### Option 2: Local Development Environment

1. **Install Infrastructure Dependencies**
   ```bash
   # Install Redis locally
   # Windows (using Chocolatey)
   choco install redis-64

   # macOS (using Homebrew)
   brew install redis

   # Ubuntu/Debian
   sudo apt-get install redis-server

   # Start Redis
   redis-server
   ```

2. **Configure SQL Server**
   ```bash
   # Using Docker for SQL Server
   docker run -d --name raid-sqlserver \
     -e "SA_PASSWORD=YourStrong@Passw0rd" \
     -e "ACCEPT_EULA=Y" \
     -e "MSSQL_PID=Developer" \
     -p 1433:1433 \
     mcr.microsoft.com/mssql/server:2022-latest
   ```

3. **Set Environment Variables**
   ```bash
   # Create appsettings.Development.json or set environment variables
   export ASPNETCORE_ENVIRONMENT=Development
   export Redis__ConnectionString="localhost:6379"
   export SqlServer__ConnectionString="Server=localhost;Database=RaidDev;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;"
   export AzureOpenAI__Endpoint="your-azure-openai-endpoint"
   export AzureOpenAI__ApiKey="your-api-key"
   ```

### Verify Installation

```bash
# Run comprehensive test suite
dotnet test --logger console

# Expected output: 100+ tests passing
# Memory Agent Tests: 42 tests
# Database Agent Tests: 54+ tests
# Integration Tests: Multiple scenarios
```

### First Agent Interaction

```bash
# Start Database Agent interactive mode
cd src/Agents/Raid.Agents.Database/Raid.Agents.Database.Console
dotnet run -- --interactive

# Example commands:
raid-db> health                                    # Check agent health
raid-db> discover                                  # Find other agents
raid-db> memory store "SQL best practices"         # Store knowledge
raid-db> validate "SELECT * FROM Users"           # Validate SQL
raid-db> query "SELECT @@VERSION"                 # Execute query
```

## Platform Architecture Deep Dive

### Multi-Agent Ecosystem Overview

The RAID Platform employs a sophisticated multi-agent architecture where specialized agents collaborate to solve complex problems:

```csharp
// Platform composition example
public class RaidPlatform
{
    // Infrastructure Agents - Platform Services
    public IMemoryAgent MemoryAgent { get; }
    public ISecurityAgent SecurityAgent { get; }
    public IAnalyticsAgent AnalyticsAgent { get; }

    // Specialist Agents - Domain Experts
    public IDatabaseAgent DatabaseAgent { get; }
    public ICodeReviewAgent CodeReviewAgent { get; }
    public IApiDesignAgent ApiDesignAgent { get; }

    // Platform Services
    public IAgentDiscovery Discovery { get; }
    public IAgentCommunication Communication { get; }
    public IConfigurationService Configuration { get; }
}
```

### Core Platform Components

#### 1. Agent Registry and Discovery

Central service for agent registration, capability discovery, and health monitoring:

```csharp
// Agent registration example
public class MySpecialistAgent : ISpecialistAgent
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var agentInfo = new AgentInfo
        {
            Id = "my-specialist-agent",
            Name = "My Specialist Agent",
            Description = "Specialized AI agent for domain-specific tasks",
            Version = "1.0.0",
            Capabilities = new[] { "analysis", "reporting", "optimization" },
            Endpoints = new Dictionary<string, string>
            {
                ["health"] = "/health",
                ["api"] = "/api/v1",
                ["metrics"] = "/metrics"
            },
            Status = AgentStatus.Online
        };

        await _agentDiscovery.RegisterAgentAsync(agentInfo);
    }
}
```

#### 2. A2A Communication Protocol

Standardized messaging system for inter-agent communication:

```csharp
// Example: Database Agent requesting memory storage
public class DatabaseAgentService
{
    private readonly IAgentCommunication _communication;

    public async Task<string> ProcessQueryWithContext(string query)
    {
        // Store query context in Memory Agent
        var memoryRequest = new AgentRequest
        {
            TargetAgentId = "memory-agent",
            RequestType = "store-context",
            Content = new
            {
                QueryText = query,
                Timestamp = DateTime.UtcNow,
                AgentId = "database-agent",
                Context = "SQL query execution"
            }
        };

        var memoryResponse = await _communication.SendRequestAsync(memoryRequest);

        // Process query...
        var result = await ProcessQuery(query);

        // Store result for future learning
        var learningRequest = new AgentRequest
        {
            TargetAgentId = "memory-agent",
            RequestType = "store-knowledge",
            Content = new
            {
                Query = query,
                Result = result,
                Category = "successful-queries"
            }
        };

        await _communication.SendRequestAsync(learningRequest);

        return result;
    }
}
```

#### 3. Hybrid Storage Architecture

The platform uses a sophisticated storage strategy optimized for different data types:

```csharp
public class HybridStorageService
{
    // Redis: Session cache and real-time data
    private readonly IDatabase _redis;

    // SQL Server: Persistent storage and complex queries
    private readonly ApplicationDbContext _dbContext;

    // Vector Store: Semantic search and embeddings
    private readonly IVectorSearchEngine _vectorSearch;

    public async Task StoreContextAsync(AgentContext context)
    {
        // Fast session storage in Redis
        await _redis.StringSetAsync(
            $"session:{context.SessionId}",
            JsonSerializer.Serialize(context),
            TimeSpan.FromMinutes(30)
        );

        // Persistent storage in SQL Server
        _dbContext.AgentContexts.Add(context);
        await _dbContext.SaveChangesAsync();

        // Semantic indexing for search
        await _vectorSearch.IndexAsync(context.Content, context.Id);
    }
}
```

## Agent Development

### Creating a New Specialist Agent

#### 1. Project Structure Setup

```bash
# Create new agent project
mkdir -p src/Agents/Raid.Agents.MyDomain
cd src/Agents/Raid.Agents.MyDomain

# Core library
dotnet new classlib -n Raid.Agents.MyDomain.Core
# Console interface (optional)
dotnet new console -n Raid.Agents.MyDomain.Console
# Tests
dotnet new xunit -n Raid.Agents.MyDomain.Tests
```

#### 2. Implement Core Agent Interface

```csharp
// Core agent implementation
public class MyDomainAgent : ISpecialistAgent
{
    private readonly ILogger<MyDomainAgent> _logger;
    private readonly IAgentCommunication _communication;
    private readonly IMemoryAgent _memoryAgent;
    private readonly IConfiguration _configuration;

    public MyDomainAgent(
        ILogger<MyDomainAgent> logger,
        IAgentCommunication communication,
        IMemoryAgent memoryAgent,
        IConfiguration configuration)
    {
        _logger = logger;
        _communication = communication;
        _memoryAgent = memoryAgent;
        _configuration = configuration;
    }

    public async Task<AgentResponse> ProcessRequestAsync(AgentRequest request)
    {
        _logger.LogInformation("Processing request: {RequestType}", request.RequestType);

        try
        {
            // Store request context for learning
            await _memoryAgent.StoreContextAsync(new AgentContext
            {
                AgentId = "my-domain-agent",
                SessionId = request.SessionId,
                Topic = request.RequestType,
                Content = request.Content.ToString(),
                CreatedAt = DateTime.UtcNow
            });

            // Process request based on type
            var result = request.RequestType switch
            {
                "analyze" => await AnalyzeAsync(request.Content),
                "generate" => await GenerateAsync(request.Content),
                "optimize" => await OptimizeAsync(request.Content),
                _ => throw new NotSupportedException($"Request type '{request.RequestType}' not supported")
            };

            return new AgentResponse
            {
                Success = true,
                Content = result,
                Metadata = new Dictionary<string, object>
                {
                    ["ProcessedAt"] = DateTime.UtcNow,
                    ["AgentVersion"] = "1.0.0"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request: {RequestType}", request.RequestType);

            return new AgentResponse
            {
                Success = false,
                Error = ex.Message,
                Content = null
            };
        }
    }

    private async Task<object> AnalyzeAsync(object content)
    {
        // Implement domain-specific analysis
        // This could involve AI model calls, data processing, etc.

        // Example: Get relevant context from memory
        var relevantContext = await _memoryAgent.SearchKnowledgeAsync(
            content.ToString(),
            maxResults: 5
        );

        // Process with domain expertise...
        return new
        {
            Analysis = "Domain-specific analysis result",
            Confidence = 0.95,
            RelevantContext = relevantContext
        };
    }
}
```

#### 3. Dependency Injection Setup

```csharp
// Startup configuration for new agent
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Core platform services
        services.AddRaidPlatform(Configuration);

        // Agent-specific services
        services.AddScoped<IMyDomainAgent, MyDomainAgent>();
        services.AddScoped<IMyDomainService, MyDomainService>();

        // AI/ML services if needed
        services.AddAzureOpenAI(Configuration);
        services.AddSemanticKernel();

        // Specialized dependencies
        services.Configure<MyDomainOptions>(
            Configuration.GetSection("MyDomain")
        );
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHealthChecks("/health");
            endpoints.MapMetrics("/metrics");
        });
    }
}
```

### Advanced Agent Features

#### 1. Semantic Kernel Integration

```csharp
// Semantic Kernel function integration
public class MyDomainPlugin
{
    private readonly IMyDomainService _domainService;

    public MyDomainPlugin(IMyDomainService domainService)
    {
        _domainService = domainService;
    }

    [KernelFunction("analyze_data")]
    [Description("Analyzes domain-specific data and provides insights")]
    public async Task<string> AnalyzeDataAsync(
        [Description("The data to analyze")] string data,
        [Description("Analysis type (summary, detailed, statistical)")] string analysisType = "summary")
    {
        var result = await _domainService.AnalyzeAsync(data, analysisType);
        return JsonSerializer.Serialize(result);
    }

    [KernelFunction("generate_report")]
    [Description("Generates a comprehensive report based on analysis")]
    public async Task<string> GenerateReportAsync(
        [Description("Analysis results to include in report")] string analysisData,
        [Description("Report format (json, markdown, html)")] string format = "markdown")
    {
        var report = await _domainService.GenerateReportAsync(analysisData, format);
        return report;
    }
}
```

#### 2. Circuit Breaker Pattern for Resilience

```csharp
// Resilient agent communication
public class ResilientAgentCommunication
{
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly IRetryPolicy _retryPolicy;

    public async Task<AgentResponse> SendRequestWithResilienceAsync(AgentRequest request)
    {
        return await _circuitBreaker.ExecuteAsync(async () =>
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var response = await _communication.SendRequestAsync(request);

                if (!response.Success)
                {
                    throw new AgentCommunicationException(
                        $"Agent request failed: {response.Error}"
                    );
                }

                return response;
            });
        });
    }
}
```

## A2A Communication Integration

### Agent Discovery Implementation

```csharp
// Implementing agent discovery in your agent
public class AgentStartupService : IHostedService
{
    private readonly IAgentDiscovery _discovery;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentStartupService> _logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var agentConfig = _configuration.GetSection("Agent").Get<AgentConfiguration>();

        var agentInfo = new AgentInfo
        {
            Id = agentConfig.Id,
            Name = agentConfig.Name,
            Description = agentConfig.Description,
            Version = agentConfig.Version,
            Capabilities = agentConfig.Capabilities,
            Endpoints = new Dictionary<string, string>
            {
                ["health"] = $"{agentConfig.BaseUrl}/health",
                ["api"] = $"{agentConfig.BaseUrl}/api/v1",
                ["metrics"] = $"{agentConfig.BaseUrl}/metrics"
            },
            Status = AgentStatus.Starting,
            LastSeen = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                ["MachineName"] = Environment.MachineName,
                ["ProcessId"] = Environment.ProcessId
            }
        };

        await _discovery.RegisterAgentAsync(agentInfo);

        // Start health check reporting
        _ = Task.Run(async () => await HealthCheckLoop(cancellationToken), cancellationToken);

        _logger.LogInformation("Agent {AgentId} registered successfully", agentInfo.Id);
    }

    private async Task HealthCheckLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _discovery.UpdateAgentStatusAsync(
                    _configuration["Agent:Id"],
                    AgentStatus.Online
                );

                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating agent health status");
            }
        }
    }
}
```

### Capability-Based Agent Discovery

```csharp
// Finding agents by capability
public class CapabilityBasedOrchestration
{
    private readonly IAgentDiscovery _discovery;
    private readonly IAgentCommunication _communication;

    public async Task<string> ProcessComplexWorkflowAsync(WorkflowRequest request)
    {
        // Find agents capable of different workflow steps
        var analysisAgents = await _discovery.FindAgentsByCapabilityAsync("analysis");
        var reportingAgents = await _discovery.FindAgentsByCapabilityAsync("reporting");
        var optimizationAgents = await _discovery.FindAgentsByCapabilityAsync("optimization");

        var results = new List<object>();

        // Step 1: Analysis
        foreach (var agent in analysisAgents.Take(3)) // Parallel analysis
        {
            var analysisRequest = new AgentRequest
            {
                TargetAgentId = agent.Id,
                RequestType = "analyze",
                Content = request.Data,
                SessionId = request.SessionId
            };

            var result = await _communication.SendRequestAsync(analysisRequest);
            if (result.Success)
            {
                results.Add(result.Content);
            }
        }

        // Step 2: Optimization based on analysis
        var optimizationRequest = new AgentRequest
        {
            TargetAgentId = optimizationAgents.First().Id,
            RequestType = "optimize",
            Content = results,
            SessionId = request.SessionId
        };

        var optimizationResult = await _communication.SendRequestAsync(optimizationRequest);

        // Step 3: Report generation
        var reportRequest = new AgentRequest
        {
            TargetAgentId = reportingAgents.First().Id,
            RequestType = "generate-report",
            Content = new { Analysis = results, Optimization = optimizationResult.Content },
            SessionId = request.SessionId
        };

        var reportResult = await _communication.SendRequestAsync(reportRequest);

        return reportResult.Content?.ToString() ?? "Workflow completed";
    }
}
```

## Memory Agent Integration

### Storing and Retrieving Context

```csharp
// Example: Database Agent with Memory integration
public class DatabaseAgentWithMemory
{
    private readonly IMemoryAgent _memoryAgent;
    private readonly IDatabaseService _databaseService;

    public async Task<QueryResult> ExecuteQueryWithLearningAsync(string query, string sessionId)
    {
        // Check for similar queries in memory
        var similarQueries = await _memoryAgent.SearchKnowledgeAsync(
            $"SQL query: {query}",
            maxResults: 5
        );

        if (similarQueries.Any())
        {
            // Use cached optimization hints
            var hints = similarQueries
                .Where(k => k.Category == "optimization-hints")
                .Select(k => k.Content)
                .ToList();

            query = ApplyOptimizationHints(query, hints);
        }

        // Execute query
        var result = await _databaseService.ExecuteQueryAsync(query);

        // Store execution context for learning
        var context = new AgentContext
        {
            AgentId = "database-agent",
            SessionId = sessionId,
            Topic = "Query Execution",
            Content = query,
            Entities = ExtractEntities(query),
            Decisions = new List<string> { "Query executed successfully" },
            Outcome = result.Success ? "success" : "failure",
            CreatedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["ExecutionTime"] = result.ExecutionTime,
                ["RowCount"] = result.RowCount,
                ["QueryPlan"] = result.ExecutionPlan
            }
        };

        await _memoryAgent.StoreContextAsync(context);

        // Store optimization knowledge if query was successful
        if (result.Success && result.ExecutionTime < TimeSpan.FromSeconds(1))
        {
            var knowledge = new KnowledgeItem
            {
                Topic = "Fast Query Pattern",
                Content = query,
                Category = "optimization-hints",
                Tags = ExtractQueryTags(query),
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["Pattern"] = IdentifyQueryPattern(query),
                    ["Performance"] = "excellent"
                }
            };

            await _memoryAgent.StoreKnowledgeAsync(knowledge);
        }

        return result;
    }
}
```

### Cross-Agent Knowledge Sharing

```csharp
// Sharing knowledge between agents
public class CrossAgentLearningService
{
    private readonly IMemoryAgent _memoryAgent;
    private readonly IAgentDiscovery _discovery;

    public async Task SharePerformanceInsightAsync(string insight, string category)
    {
        // Store insight in shared knowledge base
        var knowledge = new KnowledgeItem
        {
            Topic = "Performance Insight",
            Content = insight,
            Category = category,
            Tags = new[] { "performance", "optimization", "best-practice" },
            CreatedAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["Source"] = "automated-analysis",
                ["Confidence"] = 0.95
            }
        };

        await _memoryAgent.StoreKnowledgeAsync(knowledge);

        // Notify relevant agents about new insight
        var interestedAgents = await _discovery.FindAgentsByCapabilityAsync("performance-optimization");

        foreach (var agent in interestedAgents)
        {
            var notification = new AgentRequest
            {
                TargetAgentId = agent.Id,
                RequestType = "knowledge-update",
                Content = new
                {
                    Type = "performance-insight",
                    Knowledge = knowledge,
                    Action = "review-and-apply"
                }
            };

            // Fire and forget notification
            _ = Task.Run(async () =>
            {
                try
                {
                    await _communication.SendRequestAsync(notification);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to notify agent {AgentId} about knowledge update", agent.Id);
                }
            });
        }
    }
}
```

## Configuration Management

### Multi-Environment Configuration

```csharp
// Configuration hierarchy example
public class AgentConfiguration
{
    public AgentIdentity Identity { get; set; } = new();
    public DatabaseConfiguration Database { get; set; } = new();
    public RedisConfiguration Redis { get; set; } = new();
    public A2AConfiguration A2A { get; set; } = new();
    public SecurityConfiguration Security { get; set; } = new();
    public PerformanceConfiguration Performance { get; set; } = new();
}

public class AgentIdentity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string[] Capabilities { get; set; } = Array.Empty<string>();
    public string BaseUrl { get; set; } = "http://localhost:8080";
}

// Configuration validation
public class ConfigurationValidator : IValidateOptions<AgentConfiguration>
{
    public ValidateOptionsResult Validate(string name, AgentConfiguration options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Identity.Id))
        {
            errors.Add("Agent ID is required");
        }

        if (string.IsNullOrWhiteSpace(options.Identity.Name))
        {
            errors.Add("Agent Name is required");
        }

        if (!options.Identity.Capabilities.Any())
        {
            errors.Add("At least one capability must be defined");
        }

        if (!Uri.TryCreate(options.Identity.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("Base URL must be a valid absolute URI");
        }

        return errors.Any()
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
```

### Environment-Specific Configurations

```json
{
  // appsettings.Development.json
  "Agent": {
    "Identity": {
      "Id": "my-agent-dev",
      "Name": "My Agent (Development)",
      "BaseUrl": "http://localhost:8080"
    },
    "Database": {
      "ConnectionString": "Server=localhost;Database=RaidDev;Integrated Security=true;"
    },
    "Redis": {
      "ConnectionString": "localhost:6379",
      "Database": 1
    },
    "A2A": {
      "Discovery": {
        "RefreshInterval": "00:00:30"
      }
    },
    "Performance": {
      "EnableDetailedMetrics": true,
      "LogSlowOperations": true
    }
  }
}
```

```json
{
  // appsettings.Production.json
  "Agent": {
    "Identity": {
      "Id": "my-agent-prod",
      "Name": "My Agent",
      "BaseUrl": "${AGENT_BASE_URL}"
    },
    "Database": {
      "ConnectionString": "${DATABASE_CONNECTION_STRING}"
    },
    "Redis": {
      "ConnectionString": "${REDIS_CONNECTION_STRING}",
      "Database": 0
    },
    "A2A": {
      "Discovery": {
        "RefreshInterval": "00:05:00"
      }
    },
    "Security": {
      "Authentication": {
        "Enabled": true
      },
      "Encryption": {
        "Enabled": true
      }
    }
  }
}
```

## Testing Strategies

### Unit Testing Patterns

```csharp
// Example: Testing agent functionality
public class MyDomainAgentTests
{
    private readonly Mock<ILogger<MyDomainAgent>> _loggerMock;
    private readonly Mock<IAgentCommunication> _communicationMock;
    private readonly Mock<IMemoryAgent> _memoryAgentMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly MyDomainAgent _agent;

    public MyDomainAgentTests()
    {
        _loggerMock = new Mock<ILogger<MyDomainAgent>>();
        _communicationMock = new Mock<IAgentCommunication>();
        _memoryAgentMock = new Mock<IMemoryAgent>();
        _configurationMock = new Mock<IConfiguration>();

        _agent = new MyDomainAgent(
            _loggerMock.Object,
            _communicationMock.Object,
            _memoryAgentMock.Object,
            _configurationMock.Object
        );
    }

    [Fact]
    public async Task ProcessRequestAsync_WithAnalyzeRequest_ReturnsSuccessfulResponse()
    {
        // Arrange
        var request = new AgentRequest
        {
            RequestType = "analyze",
            Content = "test data to analyze",
            SessionId = "test-session"
        };

        _memoryAgentMock
            .Setup(m => m.SearchKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<KnowledgeSearchResult>());

        // Act
        var response = await _agent.ProcessRequestAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Content.Should().NotBeNull();

        // Verify memory interaction
        _memoryAgentMock.Verify(
            m => m.StoreContextAsync(It.Is<AgentContext>(c =>
                c.AgentId == "my-domain-agent" &&
                c.Topic == "analyze"
            )),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessRequestAsync_WithUnsupportedRequestType_ReturnsFailureResponse()
    {
        // Arrange
        var request = new AgentRequest
        {
            RequestType = "unsupported-operation",
            Content = "test data",
            SessionId = "test-session"
        };

        // Act
        var response = await _agent.ProcessRequestAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("not supported");
    }
}
```

### Integration Testing with Testcontainers

```csharp
// Integration testing with real infrastructure
public class PlatformIntegrationTests : IClassFixture<TestFixture>, IAsyncDisposable
{
    private readonly TestFixture _fixture;
    private readonly IServiceProvider _serviceProvider;

    public PlatformIntegrationTests(TestFixture fixture)
    {
        _fixture = fixture;
        _serviceProvider = CreateServiceProvider();
    }

    [Fact]
    public async Task AgentCommunication_EndToEnd_WorksCorrectly()
    {
        // Arrange
        var memoryAgent = _serviceProvider.GetRequiredService<IMemoryAgent>();
        var databaseAgent = _serviceProvider.GetRequiredService<IDatabaseAgent>();
        var communication = _serviceProvider.GetRequiredService<IAgentCommunication>();

        var testQuery = "SELECT COUNT(*) FROM Users";
        var sessionId = Guid.NewGuid().ToString();

        // Act: Database agent processes query and stores context
        var queryResult = await databaseAgent.ValidateQueryAsync(testQuery);

        // Store context in memory
        var context = new AgentContext
        {
            AgentId = "database-agent",
            SessionId = sessionId,
            Topic = "Query Validation",
            Content = testQuery,
            CreatedAt = DateTime.UtcNow
        };

        await memoryAgent.StoreContextAsync(context);

        // Retrieve context from different session
        var retrievedContext = await memoryAgent.RetrieveContextAsync(sessionId);

        // Assert
        queryResult.Should().NotBeNull();
        queryResult.IsValid.Should().BeTrue();

        retrievedContext.Should().NotBeNull();
        retrievedContext.SessionId.Should().Be(sessionId);
        retrievedContext.Content.Should().Be(testQuery);
    }

    [Fact]
    public async Task AgentDiscovery_RegisterAndFind_WorksCorrectly()
    {
        // Arrange
        var discovery = _serviceProvider.GetRequiredService<IAgentDiscovery>();

        var agentInfo = new AgentInfo
        {
            Id = "test-agent",
            Name = "Test Agent",
            Capabilities = new[] { "testing", "integration" },
            Status = AgentStatus.Online
        };

        // Act
        await discovery.RegisterAgentAsync(agentInfo);
        var foundAgents = await discovery.FindAgentsByCapabilityAsync("testing");

        // Assert
        foundAgents.Should().NotBeEmpty();
        foundAgents.Should().Contain(a => a.Id == "test-agent");
    }

    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        var configuration = _fixture.CreateConfiguration();

        // Add platform services
        services.AddRaidPlatform(configuration);
        services.AddSingleton(configuration);

        return services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
```

### Performance Testing

```csharp
// Performance benchmarking
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class AgentPerformanceBenchmarks
{
    private IMemoryAgent _memoryAgent;
    private IServiceProvider _serviceProvider;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        // Configure test services...
        _serviceProvider = services.BuildServiceProvider();
        _memoryAgent = _serviceProvider.GetRequiredService<IMemoryAgent>();
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    [Arguments(10000)]
    public async Task StoreContext_Benchmark(int iterations)
    {
        var tasks = new List<Task>();

        for (int i = 0; i < iterations; i++)
        {
            var context = new AgentContext
            {
                AgentId = "benchmark-agent",
                SessionId = $"session-{i}",
                Topic = $"Benchmark Test {i}",
                Content = $"Test content for iteration {i}",
                CreatedAt = DateTime.UtcNow
            };

            tasks.Add(_memoryAgent.StoreContextAsync(context));
        }

        await Task.WhenAll(tasks);
    }

    [Benchmark]
    public async Task SearchKnowledge_Benchmark()
    {
        var results = await _memoryAgent.SearchKnowledgeAsync(
            "performance optimization best practices",
            maxResults: 10
        );

        // Ensure we got results
        if (!results.Any())
        {
            throw new InvalidOperationException("No search results returned");
        }
    }
}
```

## Deployment and DevOps

### Container Deployment

```dockerfile
# Multi-stage Dockerfile for agent deployment
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["RaidPlatform.sln", "."]
COPY ["src/Agents/Raid.Agents.MyDomain/Raid.Agents.MyDomain.Core/Raid.Agents.MyDomain.Core.csproj", "src/Agents/Raid.Agents.MyDomain/Raid.Agents.MyDomain.Core/"]
COPY ["src/Common/Raid.Common/Raid.Common.csproj", "src/Common/Raid.Common/"]
COPY ["src/Common/Raid.A2A/Raid.A2A.csproj", "src/Common/Raid.A2A/"]

# Restore dependencies
RUN dotnet restore "src/Agents/Raid.Agents.MyDomain/Raid.Agents.MyDomain.Core/Raid.Agents.MyDomain.Core.csproj"

# Copy source code
COPY . .
WORKDIR "/src/src/Agents/Raid.Agents.MyDomain/Raid.Agents.MyDomain.Core"

# Build the application
RUN dotnet build "Raid.Agents.MyDomain.Core.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Raid.Agents.MyDomain.Core.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "Raid.Agents.MyDomain.Core.dll"]
```

### Kubernetes Deployment

```yaml
# Kubernetes deployment manifest
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-domain-agent
  labels:
    app: my-domain-agent
    component: specialist-agent
spec:
  replicas: 2
  selector:
    matchLabels:
      app: my-domain-agent
  template:
    metadata:
      labels:
        app: my-domain-agent
    spec:
      containers:
      - name: my-domain-agent
        image: raid-platform/my-domain-agent:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: Agent__Identity__Id
          value: "my-domain-agent"
        - name: Redis__ConnectionString
          valueFrom:
            secretKeyRef:
              name: raid-platform-secrets
              key: redis-connection-string
        - name: SqlServer__ConnectionString
          valueFrom:
            secretKeyRef:
              name: raid-platform-secrets
              key: sqlserver-connection-string
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: my-domain-agent-service
spec:
  selector:
    app: my-domain-agent
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
  type: ClusterIP
```

### CI/CD Pipeline

```yaml
# GitHub Actions workflow
name: Build and Deploy Agent

on:
  push:
    branches: [main, develop]
    paths:
      - 'src/Agents/Raid.Agents.MyDomain/**'
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    services:
      redis:
        image: redis:alpine
        ports:
          - 6379:6379
      sqlserver:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          SA_PASSWORD: YourStrong@Passw0rd
          ACCEPT_EULA: Y
        ports:
          - 1433:1433

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore --configuration Release

    - name: Test
      run: dotnet test --no-build --configuration Release --logger trx --collect:"XPlat Code Coverage"

    - name: Upload coverage to Codecov
      uses: codecov/codecov-action@v4
      with:
        file: ./coverage.xml

  build-and-push:
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'

    steps:
    - uses: actions/checkout@v4

    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v3

    - name: Login to Container Registry
      uses: docker/login-action@v3
      with:
        registry: ghcr.io
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}

    - name: Build and push
      uses: docker/build-push-action@v5
      with:
        context: .
        file: ./docker/agents/Dockerfile.MyDomainAgent
        push: true
        tags: |
          ghcr.io/${{ github.repository }}/my-domain-agent:latest
          ghcr.io/${{ github.repository }}/my-domain-agent:${{ github.sha }}
        cache-from: type=gha
        cache-to: type=gha,mode=max

  deploy:
    needs: build-and-push
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'

    steps:
    - name: Deploy to Kubernetes
      run: |
        # Update deployment with new image
        kubectl set image deployment/my-domain-agent \
          my-domain-agent=ghcr.io/${{ github.repository }}/my-domain-agent:${{ github.sha }}

        # Wait for rollout to complete
        kubectl rollout status deployment/my-domain-agent
```

## Performance Optimization

### Monitoring and Metrics

```csharp
// Custom metrics collection
public class AgentMetricsService
{
    private readonly IMetrics _metrics;
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _responseTimeHistogram;
    private readonly Gauge<int> _activeConnectionsGauge;

    public AgentMetricsService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("RAID.Platform.Agent");

        _requestCounter = meter.CreateCounter<long>(
            "agent_requests_total",
            description: "Total number of agent requests"
        );

        _responseTimeHistogram = meter.CreateHistogram<double>(
            "agent_response_time_seconds",
            description: "Agent response time in seconds"
        );

        _activeConnectionsGauge = meter.CreateGauge<int>(
            "agent_active_connections",
            description: "Number of active agent connections"
        );
    }

    public void RecordRequest(string requestType, string status, double duration)
    {
        var tags = new TagList
        {
            ["request_type"] = requestType,
            ["status"] = status
        };

        _requestCounter.Add(1, tags);
        _responseTimeHistogram.Record(duration, tags);
    }

    public void UpdateActiveConnections(int count)
    {
        _activeConnectionsGauge.Record(count);
    }
}

// Usage in agent
public class PerformantAgent
{
    private readonly AgentMetricsService _metrics;

    public async Task<AgentResponse> ProcessRequestAsync(AgentRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        string status = "success";

        try
        {
            // Process request...
            return await ProcessRequestInternalAsync(request);
        }
        catch (Exception ex)
        {
            status = "error";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordRequest(
                request.RequestType,
                status,
                stopwatch.Elapsed.TotalSeconds
            );
        }
    }
}
```

### Caching Strategies

```csharp
// Multi-level caching implementation
public class HybridCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<HybridCacheService> _logger;

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? memoryCacheExpiration = null,
        TimeSpan? distributedCacheExpiration = null)
    {
        // Level 1: Memory cache (fastest)
        if (_memoryCache.TryGetValue(key, out T cachedValue))
        {
            return cachedValue;
        }

        // Level 2: Distributed cache (Redis)
        var distributedValue = await _distributedCache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(distributedValue))
        {
            var deserializedValue = JsonSerializer.Deserialize<T>(distributedValue);

            // Store in memory cache for faster access
            var memoryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = memoryCacheExpiration ?? TimeSpan.FromMinutes(5)
            };
            _memoryCache.Set(key, deserializedValue, memoryOptions);

            return deserializedValue;
        }

        // Level 3: Generate value (slowest)
        var newValue = await factory();

        // Store in both caches
        var distributedOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = distributedCacheExpiration ?? TimeSpan.FromHours(1)
        };

        await _distributedCache.SetStringAsync(
            key,
            JsonSerializer.Serialize(newValue),
            distributedOptions
        );

        var memoryOptionsNew = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = memoryCacheExpiration ?? TimeSpan.FromMinutes(5)
        };
        _memoryCache.Set(key, newValue, memoryOptionsNew);

        return newValue;
    }
}
```

### Connection Pooling and Resource Management

```csharp
// Efficient resource management
public class ResourcePoolManager : IDisposable
{
    private readonly ObjectPool<HttpClient> _httpClientPool;
    private readonly ObjectPool<SqlConnection> _sqlConnectionPool;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores;

    public ResourcePoolManager(IServiceProvider serviceProvider)
    {
        _httpClientPool = serviceProvider.GetRequiredService<ObjectPool<HttpClient>>();
        _sqlConnectionPool = serviceProvider.GetRequiredService<ObjectPool<SqlConnection>>();
        _semaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
    }

    public async Task<T> ExecuteWithConnectionAsync<T>(
        string connectionKey,
        Func<SqlConnection, Task<T>> operation)
    {
        var semaphore = _semaphores.GetOrAdd(
            connectionKey,
            _ => new SemaphoreSlim(10, 10) // Max 10 concurrent connections
        );

        await semaphore.WaitAsync();

        try
        {
            var connection = _sqlConnectionPool.Get();
            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                return await operation(connection);
            }
            finally
            {
                _sqlConnectionPool.Return(connection);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void Dispose()
    {
        foreach (var semaphore in _semaphores.Values)
        {
            semaphore.Dispose();
        }
        _semaphores.Clear();
    }
}
```

## Troubleshooting

### Common Issues and Solutions

#### 1. Agent Discovery Issues

```csharp
// Debugging agent discovery problems
public class DiscoveryDiagnostics
{
    public async Task DiagnoseDiscoveryIssuesAsync()
    {
        var discovery = _serviceProvider.GetRequiredService<IAgentDiscovery>();

        try
        {
            // Check if agent registry is accessible
            var allAgents = await discovery.GetAvailableAgentsAsync();
            Console.WriteLine($"Found {allAgents.Count} registered agents:");

            foreach (var agent in allAgents)
            {
                Console.WriteLine($"  - {agent.Id} ({agent.Status}) - {string.Join(", ", agent.Capabilities)}");

                // Check agent health
                var isHealthy = await discovery.CheckAgentHealthAsync(agent.Id);
                Console.WriteLine($"    Health: {(isHealthy ? "OK" : "FAILED")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Discovery service error: {ex.Message}");

            // Check network connectivity
            await CheckNetworkConnectivity();

            // Check configuration
            CheckDiscoveryConfiguration();
        }
    }

    private async Task CheckNetworkConnectivity()
    {
        var httpClient = new HttpClient();
        var discoveryUrl = _configuration["A2A:Discovery:ServiceUrl"];

        try
        {
            var response = await httpClient.GetAsync($"{discoveryUrl}/health");
            Console.WriteLine($"Discovery service health check: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Network connectivity issue: {ex.Message}");
        }
    }
}
```

#### 2. Memory Agent Connection Issues

```csharp
// Memory agent connection diagnostics
public class MemoryAgentDiagnostics
{
    public async Task DiagnoseMemoryIssuesAsync()
    {
        // Check Redis connectivity
        await CheckRedisConnectivity();

        // Check SQL Server connectivity
        await CheckSqlServerConnectivity();

        // Check Memory Agent service
        await CheckMemoryAgentService();
    }

    private async Task CheckRedisConnectivity()
    {
        try
        {
            var redis = ConnectionMultiplexer.Connect(_configuration["Redis:ConnectionString"]);
            var db = redis.GetDatabase();

            await db.StringSetAsync("health-check", "OK", TimeSpan.FromSeconds(10));
            var result = await db.StringGetAsync("health-check");

            Console.WriteLine($"Redis connectivity: {(result == "OK" ? "OK" : "FAILED")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis connection error: {ex.Message}");
            Console.WriteLine("Check Redis configuration and ensure service is running");
        }
    }

    private async Task CheckSqlServerConnectivity()
    {
        try
        {
            using var connection = new SqlConnection(_configuration["SqlServer:ConnectionString"]);
            await connection.OpenAsync();

            using var command = new SqlCommand("SELECT @@VERSION", connection);
            var version = await command.ExecuteScalarAsync();

            Console.WriteLine($"SQL Server connectivity: OK (Version: {version})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SQL Server connection error: {ex.Message}");
            Console.WriteLine("Check connection string and ensure SQL Server is accessible");
        }
    }
}
```

#### 3. Performance Issues

```csharp
// Performance diagnostics and optimization
public class PerformanceDiagnostics
{
    public async Task DiagnosePerformanceIssuesAsync()
    {
        // Memory usage analysis
        var process = Process.GetCurrentProcess();
        Console.WriteLine($"Working Set: {process.WorkingSet64 / 1024 / 1024} MB");
        Console.WriteLine($"Private Memory: {process.PrivateMemorySize64 / 1024 / 1024} MB");

        // GC analysis
        Console.WriteLine($"Gen 0 Collections: {GC.CollectionCount(0)}");
        Console.WriteLine($"Gen 1 Collections: {GC.CollectionCount(1)}");
        Console.WriteLine($"Gen 2 Collections: {GC.CollectionCount(2)}");

        // Thread pool analysis
        ThreadPool.GetAvailableThreads(out int workerThreads, out int ioThreads);
        ThreadPool.GetMaxThreads(out int maxWorkerThreads, out int maxIoThreads);

        Console.WriteLine($"Available Worker Threads: {workerThreads}/{maxWorkerThreads}");
        Console.WriteLine($"Available I/O Threads: {ioThreads}/{maxIoThreads}");

        // Database performance
        await AnalyzeDatabasePerformance();

        // Cache performance
        await AnalyzeCachePerformance();
    }

    private async Task AnalyzeDatabasePerformance()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var connection = new SqlConnection(_configuration["SqlServer:ConnectionString"]);
            await connection.OpenAsync();

            using var command = new SqlCommand("SELECT COUNT(*) FROM sys.objects", connection);
            await command.ExecuteScalarAsync();

            stopwatch.Stop();
            Console.WriteLine($"Database query time: {stopwatch.ElapsedMilliseconds}ms");

            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                Console.WriteLine("WARNING: Database queries are slow. Check network latency and database performance.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database performance check failed: {ex.Message}");
        }
    }
}
```

### Logging and Debugging Best Practices

```csharp
// Structured logging implementation
public class AgentLogger
{
    private readonly ILogger _logger;

    public AgentLogger(ILogger<AgentLogger> logger)
    {
        _logger = logger;
    }

    public void LogAgentRequest(string agentId, string requestType, string sessionId, object content)
    {
        _logger.LogInformation(
            "Agent {AgentId} received request {RequestType} for session {SessionId} with content {Content}",
            agentId, requestType, sessionId, JsonSerializer.Serialize(content)
        );
    }

    public void LogAgentResponse(string agentId, string requestType, bool success, TimeSpan duration)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["AgentId"] = agentId,
            ["RequestType"] = requestType,
            ["Duration"] = duration.TotalMilliseconds
        });

        if (success)
        {
            _logger.LogInformation("Agent request completed successfully");
        }
        else
        {
            _logger.LogError("Agent request failed");
        }
    }

    public void LogPerformanceWarning(string operation, TimeSpan duration, TimeSpan threshold)
    {
        if (duration > threshold)
        {
            _logger.LogWarning(
                "Operation {Operation} took {Duration}ms, exceeding threshold of {Threshold}ms",
                operation, duration.TotalMilliseconds, threshold.TotalMilliseconds
            );
        }
    }
}
```

---

**Developer Guide Version**: 3.0 (RAID Platform)
**Last Updated**: December 2024
**Target Audience**: Platform developers, agent developers, system architects
**Platform Status**: Production-ready multi-agent ecosystem

*Complete development guide for building, deploying, and maintaining enterprise AI agent ecosystems with the RAID Platform*