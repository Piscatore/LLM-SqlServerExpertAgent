using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SqlServerExpertAgent.Communication.AgentProtocol;
using SqlServerExpertAgent.Configuration;
using System.ComponentModel;

namespace SqlServerExpertAgent.Templates;

/// <summary>
/// Enhanced generic agent with A2A protocol support
/// Enables multi-agent collaboration and skill delegation
/// </summary>
public class A2AEnabledAgent : GenericExpertAgent, IA2AAgent
{
    private readonly A2ACommunicationManager _communicationManager;
    private readonly ILogger<A2AEnabledAgent> _logger;

    public A2AEnabledAgent(
        AgentTemplate template, 
        AgentConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<A2AEnabledAgent> logger,
        SkillRegistry skillRegistry,
        A2ACommunicationManager communicationManager)
        : base(template, configuration, serviceProvider, logger, skillRegistry)
    {
        _communicationManager = communicationManager;
        _logger = logger;
    }

    /// <summary>
    /// Initialize agent with A2A communication capabilities
    /// </summary>
    public override async Task InitializeAsync()
    {
        // Initialize base agent first
        await base.InitializeAsync();

        // Initialize A2A communication
        await _communicationManager.InitializeAsync();

        // Register A2A-specific kernel functions
        RegisterA2AKernelFunctions();

        _logger.LogInformation("A2A-enabled agent {AgentName} initialized with multi-agent capabilities", Identity.Name);
    }

    /// <summary>
    /// Execute operation with potential multi-agent delegation
    /// </summary>
    public override async Task<AgentResponse> ExecuteAsync(AgentRequest request)
    {
        var requestId = Guid.NewGuid().ToString();
        _logger.LogInformation("Executing A2A request {RequestId}: {Operation}", requestId, request.Operation);

        try
        {
            // Check if we can handle this locally
            var localSkill = await SelectSkillForRequestAsync(request);
            if (localSkill != null)
            {
                _logger.LogDebug("Executing operation {Operation} locally with skill {SkillName}", 
                    request.Operation, localSkill.SkillInfo.Name);
                return await base.ExecuteAsync(request);
            }

            // Try to delegate to remote agent
            _logger.LogInformation("Local skills cannot handle {Operation}, searching for remote agents", request.Operation);
            
            var remoteAgents = await _communicationManager.FindAgentsWithSkillAsync(request.Operation);
            if (remoteAgents.Any())
            {
                var targetAgent = remoteAgents.First(); // Simple selection - could be enhanced with load balancing
                
                var skillRequest = new A2ASkillRequest
                {
                    SkillName = request.Operation,
                    Operation = request.Operation,
                    Parameters = request.Parameters,
                    Context = request.Context,
                    TimeoutSeconds = (int?)request.Timeout?.TotalSeconds
                };

                _logger.LogInformation("Delegating {Operation} to remote agent {AgentName}", 
                    request.Operation, targetAgent.Name);

                var skillResponse = await _communicationManager.ExecuteRemoteSkillAsync(
                    targetAgent.Id, skillRequest);

                return new AgentResponse
                {
                    RequestId = requestId,
                    Success = skillResponse.Success,
                    Data = skillResponse.Data,
                    ErrorMessage = skillResponse.Error,
                    Warnings = skillResponse.Warnings,
                    ExecutionTime = skillResponse.ExecutionTime,
                    Metadata = new Dictionary<string, object>
                    {
                        ["executedBy"] = "remote_agent",
                        ["remoteAgentId"] = targetAgent.Id,
                        ["remoteAgentName"] = targetAgent.Name
                    }
                };
            }

            return AgentResponse.CreateError(requestId, 
                $"No local or remote agents found to handle operation: {request.Operation}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing A2A request {RequestId}", requestId);
            return AgentResponse.CreateError(requestId, ex.Message);
        }
    }

    /// <summary>
    /// Execute multi-agent collaborative workflow
    /// </summary>
    [KernelFunction("execute_collaborative_workflow")]
    [Description("Execute a complex workflow involving multiple specialized agents")]
    public async Task<string> ExecuteCollaborativeWorkflow(
        [Description("Workflow definition in JSON format")] string workflowJson,
        [Description("Global context for the workflow")] string? globalContext = null)
    {
        try
        {
            var workflow = System.Text.Json.JsonSerializer.Deserialize<A2AWorkflow>(workflowJson);
            if (workflow == null)
            {
                return "Error: Invalid workflow definition";
            }

            if (!string.IsNullOrEmpty(globalContext))
            {
                var contextData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(globalContext);
                if (contextData != null)
                {
                    foreach (var (key, value) in contextData)
                    {
                        workflow.GlobalContext[key] = value;
                    }
                }
            }

            _logger.LogInformation("Starting collaborative workflow {WorkflowName} with {StepCount} steps", 
                workflow.Name, workflow.Steps.Count);

            var result = await _communicationManager.ExecuteMultiAgentWorkflowAsync(workflow);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                workflowId = result.WorkflowId,
                success = result.Success,
                completedSteps = result.StepResults.Count,
                totalSteps = workflow.Steps.Count,
                errors = result.Errors,
                completedAt = result.CompletedAt,
                summary = result.Success ? "Workflow completed successfully" : "Workflow completed with errors"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing collaborative workflow");
            return $"Error executing workflow: {ex.Message}";
        }
    }

    /// <summary>
    /// Discover and connect to other agents in the network
    /// </summary>
    [KernelFunction("discover_network_agents")]
    [Description("Discover other agents in the A2A network by capability or type")]
    public async Task<string> DiscoverNetworkAgents(
        [Description("Type of discovery: capability, type, name, organization, or all")] string discoveryType,
        [Description("Search criteria (e.g., skill name for capability search)")] string? criteria = null)
    {
        try
        {
            var discoveryTypeEnum = Enum.Parse<A2ADiscoveryType>(discoveryType, true);
            
            var discoveryRequest = new A2ADiscoveryRequest
            {
                Type = discoveryTypeEnum,
                IncludeCapabilities = true
            };

            if (!string.IsNullOrEmpty(criteria))
            {
                discoveryRequest.Criteria["search"] = criteria;
            }

            var agents = await _communicationManager.FindAgentsWithSkillAsync(criteria ?? "");
            
            var agentSummaries = agents.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                type = a.Type,
                version = a.Version,
                capabilities = a.Capabilities,
                endpoint = a.Endpoint
            });

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                discoveryType = discoveryType,
                criteria = criteria ?? "none",
                agentCount = agents.Count,
                agents = agentSummaries
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering network agents");
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Delegate a specific skill execution to another agent
    /// </summary>
    [KernelFunction("delegate_skill_execution")]
    [Description("Delegate a skill execution to a specific remote agent")]
    public async Task<string> DelegateSkillExecution(
        [Description("Target agent ID to delegate to")] string targetAgentId,
        [Description("Skill name to execute")] string skillName,
        [Description("Operation within the skill")] string operation,
        [Description("Parameters for the skill execution in JSON format")] string parametersJson,
        [Description("Additional context in JSON format")] string? contextJson = null,
        [Description("Timeout in seconds")] int timeoutSeconds = 30)
    {
        try
        {
            var parameters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson) 
                ?? new Dictionary<string, object>();

            var context = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(contextJson))
            {
                context = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(contextJson) 
                    ?? new Dictionary<string, object>();
            }

            var skillRequest = new A2ASkillRequest
            {
                SkillName = skillName,
                Operation = operation,
                Parameters = parameters,
                Context = context,
                TimeoutSeconds = timeoutSeconds
            };

            _logger.LogInformation("Delegating skill {SkillName}.{Operation} to agent {AgentId}", 
                skillName, operation, targetAgentId);

            var result = await _communicationManager.ExecuteRemoteSkillAsync(targetAgentId, skillRequest);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = result.Success,
                data = result.Data,
                error = result.Error,
                warnings = result.Warnings,
                executionTime = result.ExecutionTime,
                metadata = result.Metadata
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delegating skill execution");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Start a conversation with another agent for complex collaboration
    /// </summary>
    [KernelFunction("start_agent_conversation")]
    [Description("Start a conversation with another agent for complex multi-turn collaboration")]
    public async Task<string> StartAgentConversation(
        [Description("Target agent ID to start conversation with")] string targetAgentId,
        [Description("Topic or purpose of the conversation")] string topic)
    {
        try
        {
            var conversationId = await _communicationManager.StartConversationAsync(targetAgentId, topic);
            
            _logger.LogInformation("Started conversation {ConversationId} with agent {AgentId} on topic: {Topic}", 
                conversationId, targetAgentId, topic);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                conversationId = conversationId,
                targetAgentId = targetAgentId,
                topic = topic,
                status = "started",
                startedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting agent conversation");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                error = ex.Message,
                success = false
            });
        }
    }

    private void RegisterA2AKernelFunctions()
    {
        // A2A functions are automatically registered through the [KernelFunction] attributes
        // Additional setup could be done here if needed
        _logger.LogDebug("Registered A2A kernel functions for multi-agent collaboration");
    }

    // Expose the SelectSkillForRequestAsync method for A2A usage
    internal async Task<ISkillPlugin?> SelectSkillForRequestAsync(AgentRequest request)
    {
        return await base.SelectSkillForRequestAsync(request);
    }
}

/// <summary>
/// Interface for A2A-enabled agents
/// </summary>
public interface IA2AAgent : IExpertAgent
{
    Task<string> ExecuteCollaborativeWorkflow(string workflowJson, string? globalContext = null);
    Task<string> DiscoverNetworkAgents(string discoveryType, string? criteria = null);
    Task<string> DelegateSkillExecution(string targetAgentId, string skillName, string operation, string parametersJson, string? contextJson = null, int timeoutSeconds = 30);
    Task<string> StartAgentConversation(string targetAgentId, string topic);
}