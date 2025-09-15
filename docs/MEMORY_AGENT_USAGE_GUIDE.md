# RAID Memory Agent Usage Guide

## Table of Contents
- [Quick Start Guide](#quick-start-guide)
- [Configuration Options](#configuration-options)
- [Common Use Cases](#common-use-cases)
- [Prerequisites](#prerequisites)
- [Monitoring and Health Checks](#monitoring-and-health-checks)
- [Next Steps](#next-steps)

## 🚀 Quick Start Guide

The RAID Memory Agent provides persistent memory, learning capabilities, and cross-agent coordination for your applications. Here's how to start using it:

### 1. Basic Setup in a Console Application

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Raid.Memory.Extensions;
using Raid.Memory.Interfaces;
using Raid.Memory.Models;

// Create a host with Memory Agent
var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddLogging();

        // Add Memory Agent with configuration
        services.AddMemoryAgent(config =>
        {
            config.RedisConnectionString = "localhost:6379";
            config.SqlConnectionString = "Server=(localdb)\\mssqllocaldb;Database=MyAppMemory;Trusted_Connection=true;";
            config.Management.MinKnowledgeConfidence = 0.5f;
        });
    })
    .Build();

// Get the Memory Agent
var memoryAgent = host.Services.GetRequiredService<IMemoryAgent>();

// Basic usage example
await BasicUsageExample(memoryAgent);

static async Task BasicUsageExample(IMemoryAgent memoryAgent)
{
    var agentId = "my-application";
    var sessionId = "user-session-123";

    // Store context about current user interaction
    var context = new AgentContext
    {
        AgentId = agentId,
        SessionId = sessionId,
        Topic = "Customer Support Query",
        Entities = new List<string> { "billing", "subscription", "refund" },
        Decisions = new List<string> { "check billing history", "verify subscription status" },
        Confidence = 0.8f,
        Tags = new List<string> { "support", "billing" }
    };

    await memoryAgent.StoreContextAsync(agentId, sessionId, context);

    // Store knowledge for future use
    var knowledge = new Knowledge
    {
        Domain = agentId,
        Concept = "Billing Issue Resolution",
        Rule = "For billing disputes, first check payment history, then verify subscription details, and offer prorated refunds when appropriate",
        Confidence = 0.9f,
        Source = "customer-service-expert",
        Tags = new List<string> { "billing", "support", "refund-policy" }
    };

    await memoryAgent.StoreKnowledgeAsync(knowledge);

    // Query similar knowledge for new situations
    var similarKnowledge = await memoryAgent.QueryKnowledgeAsync(
        "customer wants refund for billing error",
        agentId,
        threshold: 0.7f,
        maxResults: 5);

    Console.WriteLine($"Found {similarKnowledge.Count} relevant knowledge items");
}
```

### 2. ASP.NET Core Web API Integration

#### Program.cs
```csharp
using Raid.Memory.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Add Memory Agent
builder.Services.AddMemoryAgent(builder.Configuration);

var app = builder.Build();

// Ensure database is created
await app.Services.EnsureMemoryDatabaseAsync();

app.MapControllers();
app.Run();
```

#### Controllers/ChatController.cs
```csharp
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IMemoryAgent _memoryAgent;

    public ChatController(IMemoryAgent memoryAgent)
    {
        _memoryAgent = memoryAgent;
    }

    [HttpPost("message")]
    public async Task<IActionResult> ProcessMessage([FromBody] ChatRequest request)
    {
        var agentId = "chat-assistant";
        var sessionId = request.SessionId;

        // Get previous context
        var previousContext = await _memoryAgent.GetContextAsync(agentId, sessionId);

        // Get recommendations based on current topic
        var currentContext = new AgentContext
        {
            AgentId = agentId,
            SessionId = sessionId,
            Topic = request.Message,
            Entities = ExtractEntities(request.Message), // Your NLP logic
            Confidence = 0.8f,
            Tags = new List<string> { "chat", "user-query" }
        };

        var recommendations = await _memoryAgent.GetRecommendationsAsync(
            agentId, currentContext, maxResults: 3);

        // Process the message using recommendations...
        var response = ProcessWithRecommendations(request.Message, recommendations);

        // Learn from this interaction
        await _memoryAgent.LearnFromInteractionAsync(
            agentId, currentContext, response, confidence: 0.8f);

        return Ok(new { Response = response, Recommendations = recommendations });
    }
}
```

## 🔧 Configuration Options

### appsettings.json Configuration
```json
{
  "RaidMemory": {
    "RedisConnectionString": "localhost:6379",
    "SqlConnectionString": "Server=(localdb)\\mssqllocaldb;Database=MyAppMemory;Trusted_Connection=true;",
    "OpenAI": {
      "Endpoint": "https://your-openai.openai.azure.com/",
      "ApiKey": "your-api-key-here",
      "EmbeddingDeploymentName": "text-embedding-ada-002"
    },
    "VectorDatabase": {
      "Provider": "InMemory",
      "VectorDimension": 1536,
      "DistanceMetric": "cosine"
    },
    "Management": {
      "SessionContextTtl": "1.00:00:00",
      "MinKnowledgeConfidence": 0.5,
      "DefaultSimilarityThreshold": 0.7,
      "MaxSimilarKnowledgeResults": 20
    }
  }
}
```

### Programmatic Configuration
```csharp
services.AddMemoryAgent(config =>
{
    config.RedisConnectionString = "localhost:6379";
    config.SqlConnectionString = "Server=(localdb)\\mssqllocaldb;Database=MyAppMemory;Trusted_Connection=true;";
    config.Management.MinKnowledgeConfidence = 0.3f;
    config.Management.DefaultSimilarityThreshold = 0.7f;
    config.Management.SessionContextTtl = TimeSpan.FromHours(24);
    config.VectorDatabase.Provider = "InMemory";
    config.VectorDatabase.VectorDimension = 1536;
});
```

## 🎯 Common Use Cases

### 1. Chatbot with Memory

```csharp
public class ChatbotService
{
    private readonly IMemoryAgent _memory;

    public ChatbotService(IMemoryAgent memory)
    {
        _memory = memory;
    }

    public async Task<string> ProcessUserMessage(string userId, string message)
    {
        // Remember conversation context
        var context = new AgentContext
        {
            AgentId = "chatbot",
            SessionId = userId,
            Topic = message,
            Entities = ExtractEntities(message),
            Tags = new List<string> { "conversation" }
        };

        await _memory.StoreContextAsync("chatbot", userId, context);

        // Get relevant knowledge
        var knowledge = await _memory.QueryKnowledgeAsync(message, "chatbot");

        return GenerateResponse(message, knowledge);
    }

    private List<string> ExtractEntities(string message)
    {
        // Implement your entity extraction logic
        return new List<string>();
    }

    private string GenerateResponse(string message, List<Knowledge> knowledge)
    {
        // Implement your response generation logic
        return "Response based on message and knowledge";
    }
}
```

### 2. Cross-Team Knowledge Sharing

```csharp
public class KnowledgeService
{
    private readonly IMemoryAgent _memory;

    public KnowledgeService(IMemoryAgent memory)
    {
        _memory = memory;
    }

    public async Task ShareTeamKnowledge(string fromTeam, string toTeam, string sessionId)
    {
        // Share context between teams
        await _memory.ShareContextAsync(fromTeam, toTeam, sessionId);

        // Get shared knowledge across teams
        var teams = new List<string> { fromTeam, toTeam };
        var sharedKnowledge = await _memory.GetSharedKnowledgeAsync(teams);

        // Process shared insights...
        foreach (var knowledge in sharedKnowledge)
        {
            Console.WriteLine($"Shared Knowledge: {knowledge.Concept} from {knowledge.Domain}");
        }
    }
}
```

### 3. Learning from User Interactions

```csharp
public class LearningService
{
    private readonly IMemoryAgent _memory;

    public LearningService(IMemoryAgent memory)
    {
        _memory = memory;
    }

    public async Task ProcessSuccessfulInteraction(string agentId, AgentContext context, string outcome)
    {
        // Learn from successful outcomes
        await _memory.LearnFromInteractionAsync(agentId, context, outcome, confidence: 0.9f);

        // Get updated recommendations
        var recommendations = await _memory.GetRecommendationsAsync(agentId, context);

        Console.WriteLine($"Learned from interaction. Got {recommendations.Count} new recommendations.");
    }

    public async Task<List<Knowledge>> GetPersonalizedRecommendations(string userId, string currentTask)
    {
        var context = new AgentContext
        {
            AgentId = "personalization-engine",
            SessionId = userId,
            Topic = currentTask,
            Confidence = 0.8f,
            Tags = new List<string> { "personalization", "recommendations" }
        };

        return await _memory.GetRecommendationsAsync("personalization-engine", context, maxResults: 10);
    }
}
```

### 4. Multi-Agent Coordination

```csharp
public class MultiAgentOrchestrator
{
    private readonly IMemoryAgent _memory;

    public MultiAgentOrchestrator(IMemoryAgent memory)
    {
        _memory = memory;
    }

    public async Task CoordinateAgents(string taskId, List<string> agentIds)
    {
        // Share context across all agents
        var primaryAgent = agentIds.First();
        var secondaryAgents = agentIds.Skip(1);

        foreach (var secondaryAgent in secondaryAgents)
        {
            await _memory.ShareContextAsync(primaryAgent, secondaryAgent, taskId);
        }

        // Get shared knowledge for coordination
        var sharedKnowledge = await _memory.GetSharedKnowledgeAsync(agentIds, topic: "coordination");

        // Distribute coordination knowledge to all agents
        foreach (var agentId in agentIds)
        {
            var recommendations = await _memory.GetRecommendationsAsync(agentId, new AgentContext
            {
                AgentId = agentId,
                SessionId = taskId,
                Topic = "multi-agent-task",
                Tags = new List<string> { "coordination", "multi-agent" }
            });

            Console.WriteLine($"Agent {agentId} received {recommendations.Count} coordination recommendations");
        }
    }
}
```

## 🔧 Prerequisites

### Required Infrastructure

#### 1. Redis (for session storage)
```bash
# Using Docker (Recommended)
docker run -d --name redis -p 6379:6379 redis:alpine

# Or install locally on Windows
# Download from: https://github.com/microsoftarchive/redis/releases
```

#### 2. SQL Server (for persistent storage)
```bash
# SQL Server LocalDB (recommended for development)
# Already included with Visual Studio

# Or SQL Server in Docker
docker run -d --name sqlserver \
  -e ACCEPT_EULA=Y \
  -e SA_PASSWORD=YourPassword123! \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

### Minimal Setup (No External Dependencies)

If you want to start without Redis/SQL Server for testing:

```csharp
// Use in-memory implementations for testing
services.AddMemoryAgent(config =>
{
    // These will fallback to in-memory implementations
    config.RedisConnectionString = ""; // Empty = in-memory context storage
    config.SqlConnectionString = ""; // Empty = in-memory knowledge storage
});
```

**Note**: In-memory implementations don't persist data between application restarts.

## 📊 Monitoring and Health Checks

### Health Check Implementation
```csharp
// Check Memory Agent health
var memoryAgent = serviceProvider.GetRequiredService<IMemoryAgent>();
var isHealthy = await memoryAgent.IsHealthyAsync();

if (!isHealthy)
{
    Console.WriteLine("⚠️ Memory Agent is not healthy!");
}
else
{
    Console.WriteLine("✅ Memory Agent is healthy");
}
```

### Memory Statistics
```csharp
// Get comprehensive memory statistics
var stats = await memoryAgent.GetMemoryStatsAsync();

Console.WriteLine("📊 Memory Statistics:");
Console.WriteLine($"Knowledge Items: {stats["knowledge"]}");
Console.WriteLine($"Vector Index: {stats["vector_index"]}");
Console.WriteLine($"Configuration: {stats["configuration"]}");
Console.WriteLine($"Timestamp: {stats["timestamp"]}");

// Access detailed knowledge stats
if (stats["knowledge"] is Dictionary<string, object> knowledgeStats)
{
    Console.WriteLine($"Total Knowledge: {knowledgeStats["total_knowledge"]}");
    Console.WriteLine($"Average Confidence: {knowledgeStats["average_confidence"]:P1}");

    if (knowledgeStats["domains"] is Dictionary<string, int> domainCounts)
    {
        Console.WriteLine("Knowledge by Domain:");
        foreach (var domain in domainCounts)
        {
            Console.WriteLine($"  {domain.Key}: {domain.Value} items");
        }
    }
}
```

### Performance Monitoring
```csharp
public class MemoryPerformanceMonitor
{
    private readonly IMemoryAgent _memoryAgent;

    public async Task<PerformanceMetrics> MeasurePerformance()
    {
        var stopwatch = Stopwatch.StartNew();

        // Test knowledge storage
        var knowledge = new Knowledge
        {
            Domain = "performance-test",
            Concept = "Test Concept",
            Rule = "Test rule for performance measurement",
            Confidence = 0.8f,
            Source = "performance-monitor"
        };

        var storeTime = await MeasureOperation(() => _memoryAgent.StoreKnowledgeAsync(knowledge));
        var queryTime = await MeasureOperation(() => _memoryAgent.QueryKnowledgeAsync("test", "performance-test"));
        var healthTime = await MeasureOperation(() => _memoryAgent.IsHealthyAsync());

        return new PerformanceMetrics
        {
            StoreOperationMs = storeTime,
            QueryOperationMs = queryTime,
            HealthCheckMs = healthTime
        };
    }

    private async Task<double> MeasureOperation(Func<Task> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        await operation();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }
}

public class PerformanceMetrics
{
    public double StoreOperationMs { get; set; }
    public double QueryOperationMs { get; set; }
    public double HealthCheckMs { get; set; }
}
```

## 🚀 Next Steps

### 1. Start Simple
Begin with the console application example to understand the basics:
- Set up basic dependency injection
- Test knowledge storage and retrieval
- Experiment with context management

### 2. Add Configuration
Set up your infrastructure:
- Install and configure Redis
- Set up SQL Server connection
- Test with real data persistence

### 3. Integrate Gradually
Add memory capabilities to your existing application:
- Start with simple knowledge storage
- Add context tracking for user sessions
- Implement learning from successful interactions

### 4. Monitor Performance
Use the built-in statistics and health checks:
- Monitor memory usage and performance
- Track knowledge growth over time
- Optimize configuration based on usage patterns

### 5. Scale Up
Configure production-ready instances:
- Use Redis Cluster for high availability
- Set up SQL Server with proper indexing
- Consider Azure OpenAI for production embeddings
- Implement proper backup and recovery strategies

## 🎯 Best Practices

### Knowledge Storage
- Use meaningful domain names that reflect your application structure
- Set appropriate confidence levels (0.0-1.0) based on source reliability
- Tag knowledge consistently for better retrieval
- Regular cleanup of low-confidence or unused knowledge

### Context Management
- Use session IDs that are meaningful to your application
- Set appropriate TTL values for different types of contexts
- Store relevant entities and decisions for better recommendations
- Update context confidence as interactions succeed or fail

### Performance Optimization
- Use appropriate similarity thresholds (0.7 is a good starting point)
- Limit query results to what you actually need
- Monitor memory usage and clean up old data regularly
- Consider caching frequently accessed knowledge

### Security Considerations
- Don't store sensitive information in knowledge or context
- Use secure connections for Redis and SQL Server
- Implement proper access controls for the Memory Agent
- Regular security audits of stored data

The Memory Agent is designed to be **plug-and-play** - you can start using it immediately and scale up as needed!