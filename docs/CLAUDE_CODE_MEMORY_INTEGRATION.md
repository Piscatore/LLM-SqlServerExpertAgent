# Claude Code Memory Integration Guide

## Table of Contents
- [Overview](#overview)
- [Integration Options](#integration-options)
- [Implementation Examples](#implementation-examples)
- [Setup Instructions](#setup-instructions)
- [Benefits and Use Cases](#benefits-and-use-cases)
- [Current Limitations](#current-limitations)
- [Recommendations](#recommendations)

## 🧠 Overview

This guide explains how to integrate the RAID Memory Agent with Claude Code to create a persistent, learning-enhanced coding assistant. While Claude Code doesn't natively support custom memory agents yet, you can integrate the Memory Agent to add powerful capabilities like:

- **Persistent Learning**: Remember successful solutions across sessions
- **Pattern Recognition**: Identify common coding patterns and suggest optimizations
- **Context Continuity**: Maintain project context between conversations
- **Knowledge Sharing**: Share learned solutions across different projects
- **Performance Insights**: Track which tools and approaches work best

## 🎯 Integration Options

Since Claude Code doesn't natively support custom memory agents, we need to create a bridge between your Claude Code usage and the Memory Agent. Here are three approaches, ordered from most practical to most advanced:

### Option 1: Custom Memory Integration Service ⭐ **RECOMMENDED FOR DEVELOPERS**

**What it is**: A C# service that you manually integrate into your development workflow.

**How it works**: You create a service class that stores and retrieves memory data, then manually call it before/after Claude Code interactions.

**Pros**:
- ✅ Full control over when and what gets stored
- ✅ Can be integrated into existing .NET projects
- ✅ Direct access to all Memory Agent features
- ✅ Production-ready and scalable

**Cons**:
- ❌ Requires manual integration into your workflow
- ❌ Need to remember to call memory methods
- ❌ More setup required

**Best for**: Developers comfortable with C# who want maximum control and integration capabilities.

### Option 2: Memory-Enhanced Wrapper Service ⭐ **RECOMMENDED FOR BEGINNERS**

**What it is**: A standalone console application that simulates an enhanced Claude Code experience with persistent memory.

**How it works**: You run this instead of Claude Code directly. It provides a chat interface that remembers conversations and learns from them.

**Pros**:
- ✅ Immediate results - works out of the box
- ✅ No manual integration required
- ✅ Great for testing and learning how memory works
- ✅ Shows memory features clearly

**Cons**:
- ❌ Doesn't actually connect to real Claude Code
- ❌ Limited to console interface
- ❌ Simulated responses only

**Best for**: People who want to see how memory enhancement would work before implementing it for real.

### Option 3: Claude Code Configuration Enhancement ⚠️ **ADVANCED**

**What it is**: Enhance your existing Claude Code setup with memory-aware prompts and external scripts.

**How it works**: Modify your CLAUDE.md files and use external scripts (Python/PowerShell) to store memory data and enhance prompts.

**Pros**:
- ✅ Works with real Claude Code
- ✅ Can be gradually implemented
- ✅ Flexible and customizable

**Cons**:
- ❌ Most complex to set up
- ❌ Requires scripting knowledge
- ❌ Manual prompt enhancement needed
- ❌ Limited integration capabilities

**Best for**: Advanced users who want to enhance their actual Claude Code setup and are comfortable with scripting.


## 🔧 Implementation Examples

### Option 1: Memory Integration Service

#### What This Code Does

This `ClaudeMemoryIntegration` class is a C# service that bridges between Claude Code interactions and the Memory Agent. It provides three main functions:

1. **Stores conversation context** - Captures user questions, Claude responses, and tools used
2. **Learns from successful tasks** - Extracts and stores reusable knowledge patterns
3. **Retrieves relevant knowledge** - Finds similar past solutions to inform current responses

#### How to Use This Code

1. **Add this class to your existing .NET project** where you want memory integration
2. **Register it in your dependency injection container** alongside the Memory Agent services
3. **Call the methods before/after Claude Code interactions** to store and retrieve memory data
4. **Customize the helper methods** (ExtractTopic, ExtractEntities, etc.) to match your specific use cases

```csharp
// ClaudeMemoryIntegration.cs
using Raid.Memory.Interfaces;
using Raid.Memory.Models;

public class ClaudeMemoryIntegration
{
    private readonly IMemoryAgent _memoryAgent;
    private const string CLAUDE_AGENT_ID = "claude-code-assistant";

    public ClaudeMemoryIntegration(IMemoryAgent memoryAgent)
    {
        _memoryAgent = memoryAgent;
    }

    public async Task StoreConversationContext(string sessionId, string userMessage, string claudeResponse, List<string> toolsUsed)
    {
        var context = new AgentContext
        {
            AgentId = CLAUDE_AGENT_ID,
            SessionId = sessionId,
            Topic = ExtractTopic(userMessage),
            Entities = ExtractEntities(userMessage, claudeResponse),
            Decisions = toolsUsed,
            Outcome = "Successfully assisted user",
            Confidence = 0.8f,
            Tags = new List<string> { "claude-code", "coding-assistance", "conversation" },
            Metadata = new Dictionary<string, object>
            {
                ["user_message"] = userMessage,
                ["claude_response"] = claudeResponse,
                ["tools_used"] = toolsUsed,
                ["conversation_length"] = claudeResponse.Length,
                ["timestamp"] = DateTimeOffset.UtcNow
            }
        };

        await _memoryAgent.StoreContextAsync(CLAUDE_AGENT_ID, sessionId, context);
    }

    public async Task LearnFromSuccessfulTask(string sessionId, string taskType, string solution, float confidence = 0.9f)
    {
        var knowledge = new Knowledge
        {
            Domain = CLAUDE_AGENT_ID,
            Concept = taskType,
            Rule = solution,
            Confidence = confidence,
            Source = "claude-code-interaction",
            Tags = new List<string> { "coding-solution", taskType.ToLower().Replace(" ", "-"), "learned" },
            Metadata = new Dictionary<string, object>
            {
                ["session_id"] = sessionId,
                ["learned_at"] = DateTimeOffset.UtcNow,
                ["task_complexity"] = CalculateComplexity(solution)
            }
        };

        await _memoryAgent.StoreKnowledgeAsync(knowledge);
    }

    public async Task<List<Knowledge>> GetRelevantKnowledge(string currentTask, string codeContext)
    {
        var query = $"{currentTask} {ExtractKeyTerms(codeContext)}";
        return await _memoryAgent.QueryKnowledgeAsync(query, CLAUDE_AGENT_ID, threshold: 0.7f, maxResults: 5);
    }

    private string ExtractTopic(string userMessage)
    {
        // Implement topic extraction logic
        var keywords = new[] { "debug", "refactor", "implement", "fix", "optimize", "test", "deploy" };
        foreach (var keyword in keywords)
        {
            if (userMessage.ToLower().Contains(keyword))
            {
                return keyword.Substring(0, 1).ToUpper() + keyword.Substring(1) + " Task";
            }
        }
        return "General Coding Task";
    }

    private List<string> ExtractEntities(string userMessage, string claudeResponse)
    {
        var entities = new List<string>();

        // Extract programming languages
        var languages = new[] { "C#", "JavaScript", "Python", "TypeScript", "SQL", "HTML", "CSS" };
        foreach (var lang in languages)
        {
            if (userMessage.Contains(lang, StringComparison.OrdinalIgnoreCase) ||
                claudeResponse.Contains(lang, StringComparison.OrdinalIgnoreCase))
            {
                entities.Add(lang);
            }
        }

        // Extract frameworks/technologies
        var frameworks = new[] { "React", "Angular", "Vue", ".NET", "Entity Framework", "ASP.NET" };
        foreach (var framework in frameworks)
        {
            if (userMessage.Contains(framework, StringComparison.OrdinalIgnoreCase) ||
                claudeResponse.Contains(framework, StringComparison.OrdinalIgnoreCase))
            {
                entities.Add(framework);
            }
        }

        return entities.Distinct().ToList();
    }

    private string ExtractKeyTerms(string codeContext)
    {
        // Simple key term extraction
        var terms = codeContext.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length > 3 && char.IsUpper(term[0]))
            .Take(5);

        return string.Join(" ", terms);
    }

    private int CalculateComplexity(string solution)
    {
        // Simple complexity calculation based on solution length and keywords
        int complexity = solution.Length / 100; // Base complexity

        var complexKeywords = new[] { "async", "await", "interface", "abstract", "generic", "linq", "reflection" };
        foreach (var keyword in complexKeywords)
        {
            if (solution.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                complexity += 2;
            }
        }

        return Math.Min(complexity, 10); // Cap at 10
    }
}
```

### Option 2: Memory-Enhanced Wrapper Service

#### What is a Wrapper Service?

A "wrapper service" is a program that sits between you and Claude Code, adding extra functionality. Think of it like adding a memory layer to Claude Code without modifying Claude Code itself.

#### What This Console Application Does

This is a **complete working example** that simulates an enhanced Claude Code experience. It:

1. **Provides a chat interface** - You type questions, it responds (simulated)
2. **Remembers everything** - Stores all conversations and learns from them
3. **Shows memory in action** - You can see how it finds and applies past knowledge
4. **Demonstrates the concept** - Shows exactly how memory enhancement would work

#### How to Run This Example

1. **Create a new Console Application** in Visual Studio: `File > New > Project > Console App`
2. **Copy both code files** (Program.cs and ClaudeCodeMemoryService.cs) into your new project
3. **Add the Memory Agent NuGet package** or project reference
4. **Set up Redis and SQL Server** (see Infrastructure Setup section below)
5. **Run the application** with `dotnet run`
6. **Start chatting** - Type questions and see how it builds up memory over time

**Note**: This simulates Claude responses - it doesn't actually connect to Claude Code. It's designed to show you how the memory system works so you can understand the concept before implementing it for real.

```csharp
// Program.cs - Console Application
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Raid.Memory.Extensions;

var host = Host.CreateDefaultBuilder()
    .ConfigureServices(services =>
    {
        services.AddLogging();
        services.AddMemoryAgent(config =>
        {
            config.RedisConnectionString = "localhost:6379";
            config.SqlConnectionString = "Server=(localdb)\\mssqllocaldb;Database=ClaudeMemory;Trusted_Connection=true;";
        });
        services.AddSingleton<ClaudeCodeMemoryService>();
    })
    .Build();

// Ensure database is created
await host.Services.EnsureMemoryDatabaseAsync();

var memoryService = host.Services.GetRequiredService<ClaudeCodeMemoryService>();
await memoryService.StartInteractiveSession();
```

```csharp
// ClaudeCodeMemoryService.cs
using Raid.Memory.Interfaces;
using Raid.Memory.Models;

public class ClaudeCodeMemoryService
{
    private readonly IMemoryAgent _memoryAgent;
    private readonly string _sessionId;

    public ClaudeCodeMemoryService(IMemoryAgent memoryAgent)
    {
        _memoryAgent = memoryAgent;
        _sessionId = Guid.NewGuid().ToString();
    }

    public async Task StartInteractiveSession()
    {
        Console.WriteLine("🧠 Memory-Enhanced Claude Code Session Started!");
        Console.WriteLine("Your interactions will be remembered and learned from.");
        Console.WriteLine("Commands:");
        Console.WriteLine("  'exit' - Quit the session");
        Console.WriteLine("  'memory' - Show learned knowledge");
        Console.WriteLine("  'stats' - Show memory statistics");
        Console.WriteLine("  'clear' - Clear session memory");
        Console.WriteLine();

        while (true)
        {
            Console.Write("You: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) continue;

            switch (input.ToLower())
            {
                case "exit":
                    Console.WriteLine("👋 Session ended. Knowledge has been saved!");
                    return;
                case "memory":
                    await ShowLearnedKnowledge();
                    continue;
                case "stats":
                    await ShowMemoryStats();
                    continue;
                case "clear":
                    await ClearSessionMemory();
                    continue;
            }

            await ProcessUserRequest(input);
        }
    }

    private async Task ProcessUserRequest(string userInput)
    {
        Console.WriteLine("🔍 Searching memory for relevant knowledge...");

        // Get relevant knowledge from memory
        var relevantKnowledge = await _memoryAgent.QueryKnowledgeAsync(
            userInput, "claude-code", threshold: 0.6f, maxResults: 3);

        // Display relevant past solutions
        if (relevantKnowledge.Any())
        {
            Console.WriteLine("🧠 Relevant past knowledge:");
            foreach (var knowledge in relevantKnowledge)
            {
                var previewLength = Math.Min(100, knowledge.Rule.Length);
                Console.WriteLine($"   • {knowledge.Concept} (Confidence: {knowledge.Confidence:P0}, Used: {knowledge.UsageCount}x)");
                Console.WriteLine($"     {knowledge.Rule.Substring(0, previewLength)}{(knowledge.Rule.Length > previewLength ? "..." : "")}");
                Console.WriteLine($"     Tags: {string.Join(", ", knowledge.Tags)}");
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine("💡 No similar past knowledge found - this will be a new learning opportunity!");
        }

        // Simulate Claude Code processing (in real implementation, this would call Claude Code)
        var simulatedResponse = await SimulateClaudeResponse(userInput, relevantKnowledge);
        Console.WriteLine($"Claude: {simulatedResponse}");
        Console.WriteLine();

        // Store this interaction
        await StoreInteraction(userInput, simulatedResponse);

        // Learn from successful patterns
        if (IsSuccessfulInteraction(userInput, simulatedResponse))
        {
            await LearnFromInteraction(userInput, simulatedResponse);
            Console.WriteLine("🧠 ✅ Learned from this interaction!");
        }

        Console.WriteLine(new string('-', 80));
        Console.WriteLine();
    }

    private async Task<string> SimulateClaudeResponse(string userInput, List<Knowledge> relevantKnowledge)
    {
        // This is a simulation - in real implementation, you would:
        // 1. Enhance the prompt with relevant knowledge
        // 2. Call Claude Code API or process
        // 3. Return the actual response

        await Task.Delay(1000); // Simulate processing time

        var responses = new[]
        {
            "Here's a solution based on best practices and past successful approaches...",
            "I can help you with that. Based on similar past requests, here's what I recommend...",
            "Let me provide you with a comprehensive solution, incorporating lessons from previous interactions...",
            "This looks similar to a pattern I've helped with before. Here's an optimized approach..."
        };

        var random = new Random();
        var baseResponse = responses[random.Next(responses.Length)];

        if (relevantKnowledge.Any())
        {
            var knowledgeContext = $" I'm drawing from {relevantKnowledge.Count} similar past solution(s) to enhance this response.";
            return baseResponse + knowledgeContext;
        }

        return baseResponse + " This appears to be a new type of request, so I'll document this solution for future reference.";
    }

    private async Task StoreInteraction(string userInput, string response)
    {
        var context = new AgentContext
        {
            AgentId = "claude-code",
            SessionId = _sessionId,
            Topic = ExtractTopic(userInput),
            Entities = ExtractCodeEntities(userInput),
            Decisions = new List<string> { "provided-code-assistance" },
            Outcome = "User request processed",
            Confidence = 0.8f,
            Tags = DetermineInteractionTags(userInput),
            Metadata = new Dictionary<string, object>
            {
                ["user_input_length"] = userInput.Length,
                ["response_length"] = response.Length,
                ["timestamp"] = DateTimeOffset.UtcNow,
                ["session_duration"] = DateTimeOffset.UtcNow.Subtract(DateTimeOffset.UtcNow).TotalMinutes
            }
        };

        await _memoryAgent.StoreContextAsync("claude-code", _sessionId, context);
    }

    private async Task LearnFromInteraction(string userInput, string response)
    {
        var taskType = ClassifyTaskType(userInput);
        var solution = ExtractSolution(response);

        var knowledge = new Knowledge
        {
            Domain = "claude-code",
            Concept = taskType,
            Rule = $"For {taskType} tasks: {solution}",
            Confidence = 0.85f,
            Source = "user-interaction",
            Tags = new List<string>
            {
                "learned",
                taskType.ToLower().Replace(" ", "-"),
                "coding",
                DetermineComplexityTag(userInput)
            },
            Metadata = new Dictionary<string, object>
            {
                ["interaction_session"] = _sessionId,
                ["learned_from_input"] = userInput.Substring(0, Math.Min(100, userInput.Length)),
                ["confidence_source"] = "successful_interaction"
            }
        };

        await _memoryAgent.StoreKnowledgeAsync(knowledge);
    }

    private async Task ShowLearnedKnowledge()
    {
        var allKnowledge = await _memoryAgent.GetKnowledgeByDomainAsync("claude-code", 20);

        if (!allKnowledge.Any())
        {
            Console.WriteLine("🧠 No learned knowledge yet. Keep interacting to build up the knowledge base!");
            return;
        }

        Console.WriteLine("🧠 Learned Knowledge Base:");
        Console.WriteLine(new string('=', 60));

        var groupedKnowledge = allKnowledge
            .OrderByDescending(k => k.UsageCount)
            .ThenByDescending(k => k.Confidence)
            .GroupBy(k => k.Tags.FirstOrDefault(t => t != "learned" && t != "coding") ?? "General")
            .ToList();

        foreach (var group in groupedKnowledge)
        {
            Console.WriteLine($"\n📂 {group.Key.ToUpperInvariant()}:");
            foreach (var knowledge in group.Take(5))
            {
                Console.WriteLine($"   📚 {knowledge.Concept}");
                Console.WriteLine($"      Confidence: {knowledge.Confidence:P0} | Used: {knowledge.UsageCount}x | Created: {knowledge.CreatedAt:yyyy-MM-dd}");
                var previewLength = Math.Min(120, knowledge.Rule.Length);
                Console.WriteLine($"      {knowledge.Rule.Substring(0, previewLength)}{(knowledge.Rule.Length > previewLength ? "..." : "")}");
                Console.WriteLine();
            }
        }

        Console.WriteLine($"Total Knowledge Items: {allKnowledge.Count}");
        Console.WriteLine(new string('=', 60));
    }

    private async Task ShowMemoryStats()
    {
        var stats = await _memoryAgent.GetMemoryStatsAsync();
        var isHealthy = await _memoryAgent.IsHealthyAsync();

        Console.WriteLine("📊 Memory Statistics:");
        Console.WriteLine(new string('=', 40));
        Console.WriteLine($"System Health: {(isHealthy ? "✅ Healthy" : "❌ Unhealthy")}");
        Console.WriteLine($"Generated At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        if (stats.ContainsKey("knowledge") && stats["knowledge"] is Dictionary<string, object> knowledgeStats)
        {
            Console.WriteLine("\n📚 Knowledge Base:");
            Console.WriteLine($"   Total Items: {knowledgeStats.GetValueOrDefault("total_knowledge", 0)}");
            Console.WriteLine($"   Average Confidence: {knowledgeStats.GetValueOrDefault("average_confidence", 0.0):P1}");

            if (knowledgeStats.ContainsKey("domains") && knowledgeStats["domains"] is Dictionary<string, int> domainCounts)
            {
                Console.WriteLine("   By Domain:");
                foreach (var domain in domainCounts)
                {
                    Console.WriteLine($"      {domain.Key}: {domain.Value} items");
                }
            }
        }

        if (stats.ContainsKey("vector_index") && stats["vector_index"] is Dictionary<string, object> vectorStats)
        {
            Console.WriteLine("\n🔍 Vector Search:");
            foreach (var stat in vectorStats)
            {
                Console.WriteLine($"   {stat.Key}: {stat.Value}");
            }
        }

        Console.WriteLine(new string('=', 40));
    }

    private async Task ClearSessionMemory()
    {
        Console.Write("Are you sure you want to clear session memory? This will remove context but keep learned knowledge (y/N): ");
        var confirm = Console.ReadLine();

        if (confirm?.ToLower() == "y" || confirm?.ToLower() == "yes")
        {
            // Note: This would require additional implementation to clear specific session data
            Console.WriteLine("🧹 Session memory cleared. Learned knowledge preserved.");
        }
        else
        {
            Console.WriteLine("Memory clear cancelled.");
        }
    }

    #region Helper Methods

    private string ExtractTopic(string userInput)
    {
        var keywords = new Dictionary<string, string>
        {
            { "debug", "Debugging" },
            { "fix", "Bug Fix" },
            { "implement", "Implementation" },
            { "refactor", "Code Refactoring" },
            { "optimize", "Performance Optimization" },
            { "test", "Testing" },
            { "deploy", "Deployment" },
            { "review", "Code Review" },
            { "document", "Documentation" }
        };

        foreach (var keyword in keywords)
        {
            if (userInput.ToLower().Contains(keyword.Key))
            {
                return keyword.Value;
            }
        }

        return "General Coding Task";
    }

    private List<string> ExtractCodeEntities(string userInput)
    {
        var entities = new List<string>();
        var input = userInput.ToLower();

        // Programming languages
        var languages = new[] { "c#", "javascript", "python", "typescript", "java", "sql", "html", "css", "go", "rust" };
        entities.AddRange(languages.Where(lang => input.Contains(lang)));

        // Frameworks and technologies
        var frameworks = new[] { "react", "angular", "vue", ".net", "entity framework", "asp.net", "blazor", "xamarin", "wpf" };
        entities.AddRange(frameworks.Where(fw => input.Contains(fw)));

        // Common coding concepts
        var concepts = new[] { "api", "database", "authentication", "testing", "performance", "security", "ui", "backend", "frontend" };
        entities.AddRange(concepts.Where(concept => input.Contains(concept)));

        return entities.Distinct().ToList();
    }

    private List<string> DetermineInteractionTags(string userInput)
    {
        var tags = new List<string> { "claude-code", "interaction" };
        var input = userInput.ToLower();

        if (input.Contains("debug") || input.Contains("fix") || input.Contains("error"))
            tags.Add("debugging");
        if (input.Contains("test"))
            tags.Add("testing");
        if (input.Contains("performance") || input.Contains("optimize"))
            tags.Add("performance");
        if (input.Contains("refactor"))
            tags.Add("refactoring");
        if (input.Contains("implement") || input.Contains("create"))
            tags.Add("implementation");

        return tags;
    }

    private bool IsSuccessfulInteraction(string userInput, string response)
    {
        // Simple heuristics to determine if interaction was successful
        // In real implementation, you might use user feedback or other signals

        return response.Length > 50 && // Substantial response
               !response.ToLower().Contains("i don't know") &&
               !response.ToLower().Contains("i can't help");
    }

    private string ClassifyTaskType(string userInput)
    {
        var input = userInput.ToLower();

        if (input.Contains("debug") || input.Contains("fix")) return "Bug Fixing";
        if (input.Contains("implement") || input.Contains("create")) return "Implementation";
        if (input.Contains("refactor")) return "Code Refactoring";
        if (input.Contains("optimize")) return "Performance Optimization";
        if (input.Contains("test")) return "Testing";
        if (input.Contains("review")) return "Code Review";
        if (input.Contains("document")) return "Documentation";

        return "General Development";
    }

    private string ExtractSolution(string response)
    {
        // Extract the key solution from the response
        // This is simplified - in real implementation, you might use NLP to extract actual solutions

        if (response.Length <= 150)
            return response;

        return response.Substring(0, 150) + "...";
    }

    private string DetermineComplexityTag(string userInput)
    {
        var complexKeywords = new[] { "architecture", "pattern", "async", "performance", "security", "scalability" };
        var simpleKeywords = new[] { "simple", "basic", "quick", "small" };

        var input = userInput.ToLower();

        if (complexKeywords.Any(keyword => input.Contains(keyword)))
            return "complex";
        if (simpleKeywords.Any(keyword => input.Contains(keyword)))
            return "simple";

        return "intermediate";
    }

    #endregion
}
```

### Option 3: Claude Code Configuration Enhancement

#### Enhanced CLAUDE.md Configuration

Create or update your `CLAUDE.md` file:

```markdown
# Memory-Enhanced Claude Code Instructions

## Memory System Integration
Before responding to complex requests:
1. Check for similar past solutions using `!memory search <topic>`
2. Apply relevant knowledge from previous interactions
3. Learn from successful task completions
4. Maintain context across sessions for better assistance

## Memory Commands
- `!memory search <topic>` - Find relevant past knowledge
- `!memory learn <concept> <solution>` - Store new knowledge
- `!memory stats` - Show memory usage statistics
- `!memory health` - Check memory system health
- `!memory clear session` - Clear current session context
- `!memory export` - Export learned knowledge

## Learning Behavior
- Automatically learn from successful debugging sessions
- Remember effective code patterns and solutions
- Track tool usage patterns and success rates
- Store coding preferences and best practices
- Build up domain-specific knowledge over time

## Context Maintenance
- Remember project context across interactions
- Maintain user coding preferences and patterns
- Track successful debugging approaches
- Store frequently used code snippets and templates
- Remember team-specific coding standards

## Session Context
Always maintain awareness of:
- Current project structure and technologies
- Previously discussed issues and solutions
- User's skill level and preferences
- Established coding patterns in the project
- Recent changes and modifications made
```

#### Integration Script (Python Example)

```python
# claude_memory_bridge.py
import asyncio
import json
import subprocess
import sys
import uuid
from pathlib import Path
from datetime import datetime
import requests

class ClaudeMemoryBridge:
    def __init__(self, memory_service_url="http://localhost:5000/api/memory"):
        self.memory_service_url = memory_service_url
        self.session_id = self.get_or_create_session_id()
        self.agent_id = "claude-code"

    def get_or_create_session_id(self):
        """Get existing session ID or create a new one"""
        session_file = Path.home() / ".claude_memory_session"
        if session_file.exists():
            return session_file.read_text().strip()
        else:
            session_id = str(uuid.uuid4())
            session_file.write_text(session_id)
            return session_id

    async def store_interaction(self, user_input, claude_response, tools_used=None):
        """Store Claude Code interaction in memory"""
        if tools_used is None:
            tools_used = []

        payload = {
            "agentId": self.agent_id,
            "sessionId": self.session_id,
            "context": {
                "topic": self.extract_topic(user_input),
                "entities": self.extract_entities(user_input, claude_response),
                "decisions": tools_used,
                "outcome": "User request processed",
                "confidence": 0.8,
                "tags": ["claude-code", "interaction", "coding-assistance"],
                "metadata": {
                    "user_input_length": len(user_input),
                    "response_length": len(claude_response),
                    "timestamp": datetime.utcnow().isoformat(),
                    "tools_used": tools_used
                }
            }
        }

        try:
            response = requests.post(f"{self.memory_service_url}/context", json=payload)
            if response.status_code == 200:
                print(f"🧠 Stored interaction in memory: {len(user_input)} chars input, {len(tools_used)} tools used")
        except Exception as e:
            print(f"⚠️ Failed to store interaction: {e}")

    async def learn_from_success(self, task_type, solution, confidence=0.9):
        """Learn from successful task completion"""
        payload = {
            "domain": self.agent_id,
            "concept": task_type,
            "rule": solution,
            "confidence": confidence,
            "source": "claude-code-success",
            "tags": ["learned", "coding-solution", task_type.lower().replace(" ", "-")]
        }

        try:
            response = requests.post(f"{self.memory_service_url}/knowledge", json=payload)
            if response.status_code == 200:
                print(f"🧠 ✅ Learned from successful {task_type}")
        except Exception as e:
            print(f"⚠️ Failed to store knowledge: {e}")

    async def get_relevant_knowledge(self, current_task):
        """Query Memory Agent for relevant knowledge"""
        try:
            params = {
                "query": current_task,
                "domain": self.agent_id,
                "threshold": 0.7,
                "maxResults": 5
            }

            response = requests.get(f"{self.memory_service_url}/knowledge/search", params=params)
            if response.status_code == 200:
                knowledge_items = response.json()
                return knowledge_items
        except Exception as e:
            print(f"⚠️ Failed to retrieve knowledge: {e}")

        return []

    def extract_topic(self, user_input):
        """Extract main topic from user input"""
        keywords = {
            "debug": "Debugging",
            "fix": "Bug Fix",
            "implement": "Implementation",
            "refactor": "Code Refactoring",
            "optimize": "Performance Optimization",
            "test": "Testing",
            "deploy": "Deployment"
        }

        user_lower = user_input.lower()
        for keyword, topic in keywords.items():
            if keyword in user_lower:
                return topic

        return "General Coding Task"

    def extract_entities(self, user_input, claude_response):
        """Extract programming entities from input and response"""
        entities = []
        combined_text = f"{user_input} {claude_response}".lower()

        # Programming languages
        languages = ["c#", "javascript", "python", "typescript", "java", "sql", "html", "css"]
        entities.extend([lang for lang in languages if lang in combined_text])

        # Frameworks
        frameworks = ["react", "angular", "vue", ".net", "entity framework", "asp.net"]
        entities.extend([fw for fw in frameworks if fw in combined_text])

        return list(set(entities))

    def enhance_claude_prompt(self, original_prompt, relevant_knowledge=None):
        """Add memory context to Claude's prompt"""
        if not relevant_knowledge:
            return original_prompt

        memory_context = "\n## Relevant Past Knowledge:\n"
        for knowledge in relevant_knowledge[:3]:  # Limit to top 3
            memory_context += f"- {knowledge.get('concept', 'Unknown')}: {knowledge.get('rule', 'No details')[:100]}...\n"

        enhanced_prompt = f"""{original_prompt}

{memory_context}

## Memory Instructions:
- Apply relevant knowledge from past successful interactions
- Learn from this interaction if it's successful
- Remember patterns and solutions for future use
"""
        return enhanced_prompt

# Usage example
async def main():
    bridge = ClaudeMemoryBridge()

    # Example interaction
    user_input = "Help me debug this C# async method that's causing deadlocks"

    # Get relevant knowledge first
    relevant_knowledge = await bridge.get_relevant_knowledge(user_input)

    # Enhance prompt (you would pass this to Claude Code)
    enhanced_prompt = bridge.enhance_claude_prompt(user_input, relevant_knowledge)

    # Simulate Claude response (in real usage, this comes from Claude Code)
    claude_response = "Here's how to fix async deadlocks in C#: Use ConfigureAwait(false)..."

    # Store the interaction
    await bridge.store_interaction(user_input, claude_response, ["async debugging", "deadlock resolution"])

    # Learn from success
    await bridge.learn_from_success("Async Debugging", "Use ConfigureAwait(false) to prevent deadlocks")

if __name__ == "__main__":
    asyncio.run(main())
```

## 🛠️ Setup Instructions

**Prerequisites Before Starting:**
- Windows with Docker Desktop installed (for Redis and SQL Server)
- Visual Studio 2022 or .NET 9 SDK
- Basic familiarity with C# and console applications
- Git (to clone the RAID Memory project)

**These setup instructions apply to ALL three integration options** - every approach needs the underlying Memory Agent infrastructure running.

### 1. Infrastructure Setup

#### What These Commands Do

These Docker commands start the required database services that the Memory Agent needs:

- **Redis**: Fast storage for conversation context and session data
- **SQL Server**: Persistent storage for learned knowledge and long-term memory

**Run these commands in PowerShell or Command Prompt:**

```bash
# Start Redis for session storage (temporary, fast data)
docker run -d --name claude-redis -p 6379:6379 redis:alpine

# SQL Server LocalDB should already be available with Visual Studio
# Or use Docker:
docker run -d --name claude-sql \
  -e ACCEPT_EULA=Y \
  -e SA_PASSWORD=YourPassword123! \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

### 2. Memory Agent Service Setup

```bash
# Navigate to your project directory
cd "C:\src\Life Projects\LLM-SqlServerExpertAgent"

# Build the Memory Agent
dotnet build src/Infrastructure/Raid.Memory/Raid.Memory.csproj

# Test the Memory Agent
dotnet test tests/Raid.Memory.Tests/Raid.Memory.Tests.csproj
```

### 3. Integration Implementation

#### Choose Your Implementation Option

**For Option 1 (Custom Memory Integration Service):**
1. Add the `ClaudeMemoryIntegration.cs` class to your existing .NET project
2. Register Memory Agent services in your `Program.cs` or `Startup.cs`
3. Call memory methods manually before/after Claude Code interactions
4. Example: Store context after successful debugging, query memory before starting new tasks

**For Option 2 (Memory-Enhanced Wrapper Service):**
1. Create a new Console Application project
2. Copy the provided Program.cs and ClaudeCodeMemoryService.cs files
3. Add project references to Raid.Memory
4. Run the application and start experimenting with memory-enhanced interactions

**For Option 3 (Claude Code Configuration Enhancement):**
1. Update your CLAUDE.md file with the enhanced configuration
2. Create the Python bridge script in your project directory
3. Set up hooks to call the bridge script before/after Claude Code interactions
4. Manually enhance prompts with memory context

2. **Configure the Memory Agent** with your connection strings
3. **Test the integration** with sample interactions
4. **Configure Claude Code** to use the enhanced prompts

### 4. Configuration Files

#### Configuration Prerequisites

**Database Setup**: The Memory Agent will automatically create the necessary database tables when it first runs. You just need:
- Redis running (for session storage)
- SQL Server instance available (LocalDB works fine)
- Valid connection strings in the configuration

#### Configuration Settings Explained

**Logging Levels:**
- `"Information"` - Standard operational logs
- `"Debug"` - Detailed diagnostic information (useful for troubleshooting)
- `"Warning"` - Important issues that don't stop execution
- `"Error"` - Problems that need attention

**Management Settings:**
- `SessionContextTtl` - How long to keep conversation context (7 days = "7.00:00:00")
- `MinKnowledgeConfidence` - Only store knowledge above this confidence level (0.6 = 60%)
- `DefaultSimilarityThreshold` - How similar knowledge needs to be to be considered relevant (0.7 = 70%)
- `MaxSimilarKnowledgeResults` - Maximum number of similar knowledge items to return (10 is usually sufficient)

**Connection Strings:**
- `RedisConnectionString` - Redis server address (localhost:6379 for local Docker)
- `SqlConnectionString` - SQL Server connection (LocalDB example shown)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Raid.Memory": "Debug"
    }
  },
  "RaidMemory": {
    "RedisConnectionString": "localhost:6379",
    "SqlConnectionString": "Server=(localdb)\\mssqllocaldb;Database=ClaudeCodeMemory;Trusted_Connection=true;",
    "Management": {
      "SessionContextTtl": "7.00:00:00",
      "MinKnowledgeConfidence": 0.6,
      "DefaultSimilarityThreshold": 0.7,
      "MaxSimilarKnowledgeResults": 10
    }
  }
}
```

## 🎯 Benefits and Use Cases

### What You'll Get

1. **Persistent Learning Across Sessions**
   - Remembers successful debugging approaches
   - Stores effective code patterns and solutions
   - Builds domain-specific knowledge over time

2. **Intelligent Code Suggestions**
   - Suggests solutions based on past successes
   - Identifies similar problems and their resolutions
   - Provides context-aware recommendations

3. **Pattern Recognition and Optimization**
   - Recognizes recurring coding patterns
   - Suggests optimizations based on past improvements
   - Tracks which tools and approaches work best

4. **Project Context Continuity**
   - Maintains understanding of project structure
   - Remembers established coding standards
   - Tracks recent changes and modifications

5. **Performance Insights**
   - Tracks success rates of different approaches
   - Identifies most effective tools for specific tasks
   - Provides data-driven development insights

### Real-World Scenarios

1. **Debugging Session Memory**
   ```
   User: "I'm getting a null reference exception in my controller"
   Enhanced Claude: "Based on similar past issues, this often occurs with dependency injection.
   Here's the approach that worked in 3 previous cases..."
   ```

2. **Code Pattern Recognition**
   ```
   User: "How should I structure this API endpoint?"
   Enhanced Claude: "Looking at your past successful API implementations, you typically use
   this pattern with validation attributes and response DTOs..."
   ```

3. **Technology-Specific Knowledge**
   ```
   User: "Entity Framework performance issue"
   Enhanced Claude: "I see you've solved similar EF performance issues before using eager loading
   and query optimization. Let me apply those patterns here..."
   ```

## 🚧 Current Limitations

### Technical Limitations

1. **Custom Integration Required**
   - Not built into Claude Code natively
   - Requires manual implementation and maintenance
   - Need to hook into Claude Code's interaction flow

2. **Infrastructure Dependencies**
   - Requires Redis and SQL Server to be running
   - Additional complexity in deployment and maintenance
   - Network connectivity requirements

3. **No Official API Integration**
   - Cannot directly access Claude Code's internal state
   - Need to intercept and process interactions manually
   - Limited visibility into Claude's decision-making process

### Functional Limitations

1. **Learning Quality Depends on Usage**
   - Needs sufficient interaction data to be effective
   - Quality of learning depends on user feedback
   - May require manual curation of learned knowledge

2. **Context Window Considerations**
   - Additional memory context consumes token space
   - Need to balance context richness with prompt limits
   - May require summarization for long interaction histories

## 📚 Additional Learning Resources

### If You're New to These Technologies

**Docker & Containerization:**
- [Docker Desktop for Windows](https://docs.docker.com/desktop/windows/) - Official installation guide
- [Docker Getting Started Tutorial](https://docs.docker.com/get-started/) - Learn Docker basics

**Redis:**
- [Redis Introduction](https://redis.io/docs/about/) - Understanding Redis and key-value storage
- [Redis Commands Reference](https://redis.io/commands/) - Common Redis operations

**Entity Framework Core:**
- [EF Core Documentation](https://docs.microsoft.com/en-us/ef/core/) - Official Microsoft documentation
- [EF Core Getting Started](https://docs.microsoft.com/en-us/ef/core/get-started/) - Beginner's guide

**Dependency Injection in .NET:**
- [.NET Dependency Injection](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) - Understanding DI patterns
- [ASP.NET Core DI](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection) - DI in web applications

**Vector Embeddings & Semantic Search:**
- [What are Vector Embeddings?](https://platform.openai.com/docs/guides/embeddings) - OpenAI's explanation
- [Semantic Search Basics](https://en.wikipedia.org/wiki/Semantic_search) - Understanding semantic search concepts

### Project-Specific Resources

**RAID Platform Architecture:**
- See `docs/MEMORY_AGENT_USAGE_GUIDE.md` in this project for comprehensive usage examples
- Review `src/Infrastructure/Raid.Memory/README.md` for technical details
- Check the test files in `tests/Raid.Memory.Tests/` for practical code examples

**Related Documentation:**
- [RAID Platform Overview](../README.md) - Understanding the broader RAID ecosystem
- [SQL Server Expert Agent](../docs/SQL_SERVER_EXPERT_AGENT.md) - How Memory Agent integrates with other agents

## 💡 Recommendations

### Start with Option 2 (Memory-Enhanced Wrapper Service)

This is the most practical approach because:

1. **Immediate Benefits**: You can start using it right away
2. **Full Control**: Complete control over the integration
3. **Gradual Enhancement**: Can build up features incrementally
4. **Testing Environment**: Safe environment to test memory capabilities

### Implementation Strategy

1. **Phase 1**: Basic memory storage and retrieval
   - Start with simple interaction logging
   - Implement basic knowledge storage
   - Test with real coding scenarios

2. **Phase 2**: Enhanced learning and recommendations
   - Add pattern recognition
   - Implement success-based learning
   - Enhance context continuity

3. **Phase 3**: Advanced features
   - Cross-project knowledge sharing
   - Performance analytics
   - Automated knowledge curation

### Best Practices

1. **Start Small**: Begin with simple use cases
2. **Monitor Performance**: Track memory usage and response times
3. **Curate Knowledge**: Regularly review and clean learned knowledge
4. **User Feedback**: Implement mechanisms to validate learned patterns
5. **Backup Strategy**: Regular backups of accumulated knowledge

The Memory Agent integration will transform your Claude Code experience into a continuously learning, context-aware coding assistant that gets better with every interaction!