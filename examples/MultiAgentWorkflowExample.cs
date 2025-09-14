using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SqlServerExpertAgent.Communication.AgentProtocol;
using SqlServerExpertAgent.Configuration;
using SqlServerExpertAgent.Templates;
using System.Text.Json;

namespace SqlServerExpertAgent.Examples;

/// <summary>
/// Example demonstrating multi-agent collaboration workflows
/// Shows how to implement your vision of "virtual planning agents, Architects, UI/UX experts"
/// </summary>
public class MultiAgentWorkflowExample
{
    public static async Task RunExampleAsync()
    {
        // Setup dependency injection with A2A support
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<SkillRegistry>();
        services.AddSingleton<DeclarativeAgentFactory>();
        services.AddSingleton<A2ATransport>();
        services.AddSingleton<A2ACommunicationManager>();
        
        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<MultiAgentWorkflowExample>>();

        try
        {
            logger.LogInformation("Starting Multi-Agent Workflow Example");

            // Initialize A2A configuration
            var a2aConfig = CreateA2AConfiguration();
            
            // Create Project Architect Agent (coordinator)
            var architectAgent = await CreateProjectArchitectAgentAsync(serviceProvider, a2aConfig);
            
            // Demonstrate multi-agent project planning workflow
            await DemonstrateProjectPlanningWorkflowAsync(architectAgent, logger);
            
            // Demonstrate agent discovery and delegation
            await DemonstrateAgentDiscoveryAsync(architectAgent, logger);
            
            // Demonstrate complex collaborative workflow
            await DemonstrateComplexCollaborationAsync(architectAgent, logger);

            logger.LogInformation("Multi-Agent Workflow Example completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Multi-Agent Workflow Example");
            throw;
        }
    }

    /// <summary>
    /// Demonstrate a complete project planning workflow with multiple specialist agents
    /// This implements your vision of coordinated virtual teams
    /// </summary>
    private static async Task DemonstrateProjectPlanningWorkflowAsync(A2AEnabledAgent architectAgent, ILogger logger)
    {
        logger.LogInformation("=== Demonstrating Project Planning Workflow ===");

        // Define a complex project planning workflow
        var projectPlanningWorkflow = new
        {
            id = "project-planning-2025",
            name = "E-Commerce Platform Planning",
            description = "Complete planning phase for new e-commerce platform",
            steps = new[]
            {
                new
                {
                    id = "requirements-analysis",
                    order = 1,
                    targetAgentId = "business-analyst-001",
                    isRequired = true,
                    skillRequest = new
                    {
                        skillName = "RequirementsAnalysisSkill",
                        operation = "analyze_business_requirements",
                        parameters = new Dictionary<string, object>
                        {
                            ["projectType"] = "e-commerce",
                            ["expectedUsers"] = 100000,
                            ["regions"] = new[] { "US", "EU", "APAC" }
                        }
                    },
                    dependsOn = Array.Empty<string>()
                },
                new
                {
                    id = "database-architecture",
                    order = 2,
                    targetAgentId = "sqlserver-expert-001",
                    isRequired = true,
                    skillRequest = new
                    {
                        skillName = "DatabaseArchitectureSkill",
                        operation = "design_database_schema",
                        parameters = new Dictionary<string, object>
                        {
                            ["scalabilityTarget"] = "high",
                            ["dataVolume"] = "large",
                            ["performanceRequirements"] = "sub-100ms"
                        }
                    },
                    dependsOn = new[] { "requirements-analysis" }
                },
                new
                {
                    id = "uiux-design",
                    order = 2, // Can run parallel with database design
                    targetAgentId = "uiux-expert-001", 
                    isRequired = true,
                    skillRequest = new
                    {
                        skillName = "UIUXDesignSkill",
                        operation = "create_user_experience_design",
                        parameters = new Dictionary<string, object>
                        {
                            ["designSystem"] = "modern",
                            ["accessibility"] = "WCAG-AA",
                            ["responsive"] = true
                        }
                    },
                    dependsOn = new[] { "requirements-analysis" }
                },
                new
                {
                    id = "security-assessment",
                    order = 3,
                    targetAgentId = "security-expert-001",
                    isRequired = true,
                    skillRequest = new
                    {
                        skillName = "SecurityAssessmentSkill",
                        operation = "analyze_security_requirements",
                        parameters = new Dictionary<string, object>
                        {
                            ["complianceStandards"] = new[] { "PCI-DSS", "GDPR" },
                            ["threatModel"] = "e-commerce"
                        }
                    },
                    dependsOn = new[] { "database-architecture", "uiux-design" }
                },
                new
                {
                    id = "financial-analysis",
                    order = 4,
                    targetAgentId = "financial-expert-001",
                    isRequired = false, // Optional but valuable
                    skillRequest = new
                    {
                        skillName = "FinancialAnalysisSkill",
                        operation = "calculate_project_costs",
                        parameters = new Dictionary<string, object>
                        {
                            ["timeline"] = "18-months",
                            ["teamSize"] = 12,
                            ["infrastructureType"] = "cloud"
                        }
                    },
                    dependsOn = new[] { "requirements-analysis", "database-architecture", "uiux-design" }
                },
                new
                {
                    id = "project-plan-synthesis",
                    order = 5,
                    targetAgentId = "project-manager-001",
                    isRequired = true,
                    skillRequest = new
                    {
                        skillName = "ProjectManagementSkill",
                        operation = "create_master_project_plan",
                        parameters = new Dictionary<string, object>
                        {
                            ["methodology"] = "agile-scrum",
                            ["riskTolerance"] = "medium",
                            ["qualityGates"] = true
                        }
                    },
                    dependsOn = new[] { "database-architecture", "uiux-design", "security-assessment", "financial-analysis" }
                }
            },
            globalContext = new Dictionary<string, object>
            {
                ["projectName"] = "NextGen E-Commerce Platform",
                ["budget"] = 2500000,
                ["timeline"] = "18-months",
                ["stakeholders"] = new[] { "CEO", "CTO", "Head of Product", "Head of Engineering" }
            }
        };

        var workflowJson = JsonSerializer.Serialize(projectPlanningWorkflow);
        var globalContextJson = JsonSerializer.Serialize(projectPlanningWorkflow.globalContext);

        logger.LogInformation("Executing project planning workflow with {StepCount} steps", projectPlanningWorkflow.steps.Length);

        var result = await architectAgent.ExecuteCollaborativeWorkflow(workflowJson, globalContextJson);
        
        logger.LogInformation("Project Planning Workflow Result: {Result}", result);

        // Parse and display results
        var resultData = JsonSerializer.Deserialize<Dictionary<string, object>>(result);
        if (resultData != null && resultData.TryGetValue("success", out var success) && (bool)success)
        {
            logger.LogInformation("✅ Project planning completed successfully!");
            logger.LogInformation("   - Requirements analyzed by Business Analyst");
            logger.LogInformation("   - Database architecture designed by SQL Server Expert");  
            logger.LogInformation("   - UI/UX design created by Design Expert");
            logger.LogInformation("   - Security assessment completed by Security Expert");
            logger.LogInformation("   - Financial analysis provided by Financial Expert");
            logger.LogInformation("   - Master project plan synthesized by Project Manager");
        }
        else
        {
            logger.LogWarning("⚠️ Project planning completed with some issues");
        }
    }

    /// <summary>
    /// Demonstrate agent discovery and dynamic skill delegation
    /// </summary>
    private static async Task DemonstrateAgentDiscoveryAsync(A2AEnabledAgent architectAgent, ILogger logger)
    {
        logger.LogInformation("=== Demonstrating Agent Discovery ===");

        // Discover all agents in the network
        var allAgentsResult = await architectAgent.DiscoverNetworkAgents("all");
        logger.LogInformation("Network Discovery Result: {Result}", allAgentsResult);

        // Discover agents with specific database skills
        var dbAgentsResult = await architectAgent.DiscoverNetworkAgents("capability", "SqlServerSkill");
        logger.LogInformation("Database Expert Discovery: {Result}", dbAgentsResult);

        // Discover UI/UX experts
        var uiuxAgentsResult = await architectAgent.DiscoverNetworkAgents("capability", "UIUXDesignSkill");
        logger.LogInformation("UI/UX Expert Discovery: {Result}", uiuxAgentsResult);
    }

    /// <summary>
    /// Demonstrate complex multi-step collaboration with error handling
    /// </summary>
    private static async Task DemonstrateComplexCollaborationAsync(A2AEnabledAgent architectAgent, ILogger logger)
    {
        logger.LogInformation("=== Demonstrating Complex Collaboration ===");

        // Start a conversation with a database expert for detailed schema design
        var conversationResult = await architectAgent.StartAgentConversation(
            "sqlserver-expert-001", 
            "E-commerce database schema optimization and performance planning");
        
        logger.LogInformation("Started collaboration conversation: {Result}", conversationResult);

        // Delegate a specific complex analysis to a specialist
        var delegationParameters = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["analysisType"] = "performance_bottleneck_analysis",
            ["expectedLoad"] = 10000,
            ["peakTrafficMultiplier"] = 5,
            ["regionDistribution"] = new[] { "us-east", "eu-west", "ap-southeast" }
        });

        var delegationResult = await architectAgent.DelegateSkillExecution(
            "sqlserver-expert-001",
            "SqlServerSkill", 
            "analyze_query_performance",
            delegationParameters,
            null,
            60
        );

        logger.LogInformation("Skill delegation result: {Result}", delegationResult);
    }

    private static A2AConfiguration CreateA2AConfiguration()
    {
        return new A2AConfiguration
        {
            LocalAgent = new AgentIdentifier
            {
                Id = "project-architect-001",
                Name = "ProjectArchitectAgent",
                Type = "ProjectArchitect",
                Version = "1.0.0",
                Capabilities = new List<string>
                {
                    "project_planning",
                    "architectural_design", 
                    "multi_agent_coordination",
                    "workflow_orchestration"
                },
                Endpoint = "https://localhost:5001"
            },
            DiscoveryService = new AgentIdentifier
            {
                Id = "discovery-service",
                Name = "A2ADiscoveryService",
                Type = "DiscoveryService",
                Version = "1.0.0",
                Capabilities = new List<string> { "agent_discovery", "capability_matching" },
                Endpoint = "https://discovery.a2a.local"
            },
            KnownAgents = new Dictionary<string, string>
            {
                ["sqlserver-expert-001"] = "https://sql-expert-001.agents.local",
                ["uiux-expert-001"] = "https://uiux-expert-001.agents.local",
                ["project-manager-001"] = "https://pm-001.agents.local",
                ["security-expert-001"] = "https://security-001.agents.local",
                ["financial-expert-001"] = "https://finance-001.agents.local",
                ["business-analyst-001"] = "https://ba-001.agents.local"
            },
            DefaultTimeoutSeconds = 60,
            SecurityMode = "Bearer",
            EnableMessageLogging = true
        };
    }

    private static async Task<A2AEnabledAgent> CreateProjectArchitectAgentAsync(
        IServiceProvider serviceProvider, 
        A2AConfiguration a2aConfig)
    {
        var configuration = CreateAgentConfiguration();
        var skillRegistry = serviceProvider.GetRequiredService<SkillRegistry>();
        var logger = serviceProvider.GetRequiredService<ILogger<A2AEnabledAgent>>();
        
        // Create A2A transport and communication manager
        var transport = new A2ATransport(a2aConfig, serviceProvider.GetRequiredService<ILogger<A2ATransport>>());
        var communicationManager = new A2ACommunicationManager(
            transport, 
            a2aConfig, 
            serviceProvider.GetRequiredService<ILogger<A2ACommunicationManager>>());

        // Load the Project Architect template
        var template = new AgentTemplate
        {
            Name = "ProjectArchitectAgent",
            Version = "1.0.0", 
            Description = "Senior architect agent for multi-agent project coordination",
            Personality = new TemplatePersonality
            {
                ResponseStyle = "enterprise_formal",
                ExpertiseLevel = "architect",
                Authoritative = true,
                ProactiveOptimization = true
            },
            RequiredSkills = new List<SkillRequirement>
            {
                new() { Name = "ProjectPlanningSkill", MinVersion = "1.0.0" },
                new() { Name = "ArchitecturalDesignSkill", MinVersion = "1.0.0" },
                new() { Name = "A2ACommunicationSkill", MinVersion = "1.0.0" }
            },
            OptionalSkills = new List<SkillRequirement>
            {
                new() { Name = "DocumentationSkill", MinVersion = "1.0.0" },
                new() { Name = "StakeholderCommunicationSkill", MinVersion = "1.0.0" }
            }
        };

        var agent = new A2AEnabledAgent(template, configuration, serviceProvider, logger, skillRegistry, communicationManager);
        await agent.InitializeAsync();
        
        return agent;
    }

    private static AgentConfiguration CreateAgentConfiguration()
    {
        return new AgentConfiguration
        {
            SqlServer = new SqlServerConfiguration
            {
                ConnectionStrings = new Dictionary<string, string>
                {
                    ["default"] = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;"
                }
            },
            Performance = new PerformanceSettings
            {
                ResponseTargets = new Dictionary<string, int>
                {
                    ["project_planning"] = 5000,
                    ["agent_coordination"] = 2000,
                    ["workflow_execution"] = 30000
                }
            },
            Plugins = new PluginConfiguration
            {
                PluginDirectories = new List<string> { "plugins", "skills" },
                EnableHotReload = true
            }
        };
    }
}