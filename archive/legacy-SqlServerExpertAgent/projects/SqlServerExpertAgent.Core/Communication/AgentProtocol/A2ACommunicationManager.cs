using Microsoft.Extensions.Logging;
using SqlServerExpertAgent.Templates;

namespace SqlServerExpertAgent.Communication.AgentProtocol;

/// <summary>
/// High-level A2A communication manager for agent collaboration
/// Handles agent discovery, skill delegation, and multi-agent workflows
/// </summary>
public class A2ACommunicationManager
{
    private readonly A2ATransport _transport;
    private readonly ILogger<A2ACommunicationManager> _logger;
    private readonly A2AConfiguration _configuration;
    private readonly Dictionary<string, AgentIdentifier> _knownAgents = new();
    private readonly Dictionary<string, A2AConversation> _activeConversations = new();

    public A2ACommunicationManager(A2ATransport transport, A2AConfiguration configuration, ILogger<A2ACommunicationManager> logger)
    {
        _transport = transport;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Initialize A2A communication and advertise capabilities
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing A2A communication for agent {AgentName}", _configuration.LocalAgent.Name);
        
        // Advertise our capabilities to the network
        await _transport.AdvertiseCapabilitiesAsync();
        
        // Discover existing agents
        await DiscoverNetworkAgentsAsync();
        
        _logger.LogInformation("A2A communication initialized with {AgentCount} known agents", _knownAgents.Count);
    }

    /// <summary>
    /// Execute a skill on a remote agent
    /// </summary>
    public async Task<A2ASkillResponse> ExecuteRemoteSkillAsync(
        string targetAgentId, 
        A2ASkillRequest skillRequest, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_knownAgents.TryGetValue(targetAgentId, out var targetAgent))
            {
                // Try to discover the agent
                var agents = await DiscoverAgentsByIdAsync(targetAgentId);
                if (agents.Any())
                {
                    targetAgent = agents.First();
                    _knownAgents[targetAgentId] = targetAgent;
                }
                else
                {
                    return new A2ASkillResponse
                    {
                        Success = false,
                        Error = $"Agent {targetAgentId} not found in network"
                    };
                }
            }

            var message = new A2AMessage
            {
                Id = Guid.NewGuid().ToString(),
                Type = A2AMessageType.Request,
                From = _configuration.LocalAgent,
                To = targetAgent,
                Priority = A2APriority.Normal,
                Payload = new A2APayload
                {
                    ContentType = A2APayloadTypes.SkillRequest,
                    Content = skillRequest
                }
            };

            _logger.LogInformation("Executing skill {SkillName}.{Operation} on remote agent {AgentName}", 
                skillRequest.SkillName, skillRequest.Operation, targetAgent.Name);

            var response = await _transport.SendRequestAsync(message, cancellationToken);
            
            if (response?.Payload.Content is A2ASkillResponse skillResponse)
            {
                _logger.LogDebug("Remote skill execution completed: {Success}", skillResponse.Success);
                return skillResponse;
            }

            return new A2ASkillResponse
            {
                Success = false,
                Error = "Invalid or missing skill response"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing remote skill {SkillName} on agent {AgentId}", 
                skillRequest.SkillName, targetAgentId);
            
            return new A2ASkillResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Delegate a complex task to multiple agents and coordinate the workflow
    /// </summary>
    public async Task<A2AWorkflowResult> ExecuteMultiAgentWorkflowAsync(A2AWorkflow workflow, CancellationToken cancellationToken = default)
    {
        var workflowId = Guid.NewGuid().ToString();
        var results = new Dictionary<string, A2ASkillResponse>();
        var errors = new List<string>();

        _logger.LogInformation("Starting multi-agent workflow {WorkflowId} with {StepCount} steps", 
            workflowId, workflow.Steps.Count);

        try
        {
            foreach (var step in workflow.Steps.OrderBy(s => s.Order))
            {
                _logger.LogDebug("Executing workflow step {StepId}: {SkillName} on {AgentId}", 
                    step.Id, step.SkillRequest.SkillName, step.TargetAgentId);

                // Check if this step depends on previous steps
                if (step.DependsOn.Any())
                {
                    var missingDependencies = step.DependsOn.Where(dep => !results.ContainsKey(dep)).ToList();
                    if (missingDependencies.Any())
                    {
                        var error = $"Step {step.Id} missing dependencies: {string.Join(", ", missingDependencies)}";
                        errors.Add(error);
                        continue;
                    }

                    // Merge dependency results into step context
                    foreach (var dependency in step.DependsOn)
                    {
                        if (results.TryGetValue(dependency, out var depResult) && depResult.Success)
                        {
                            foreach (var (key, value) in depResult.Data)
                            {
                                step.SkillRequest.Context[$"dep_{dependency}_{key}"] = value;
                            }
                        }
                    }
                }

                var stepResult = await ExecuteRemoteSkillAsync(step.TargetAgentId, step.SkillRequest, cancellationToken);
                results[step.Id] = stepResult;

                if (!stepResult.Success && step.IsRequired)
                {
                    var error = $"Required step {step.Id} failed: {stepResult.Error}";
                    errors.Add(error);
                    _logger.LogError("Workflow {WorkflowId} failed at required step {StepId}", workflowId, step.Id);
                    break;
                }
            }

            var success = errors.Count == 0 && results.Values.Where(r => workflow.Steps.First(s => results.ContainsKey(s.Id)).IsRequired).All(r => r.Success);
            
            _logger.LogInformation("Multi-agent workflow {WorkflowId} completed: {Success}", workflowId, success);

            return new A2AWorkflowResult
            {
                WorkflowId = workflowId,
                Success = success,
                StepResults = results,
                Errors = errors,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing multi-agent workflow {WorkflowId}", workflowId);
            
            return new A2AWorkflowResult
            {
                WorkflowId = workflowId,
                Success = false,
                StepResults = results,
                Errors = new List<string> { ex.Message },
                CompletedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Find agents capable of executing a specific skill
    /// </summary>
    public async Task<List<AgentIdentifier>> FindAgentsWithSkillAsync(string skillName, CancellationToken cancellationToken = default)
    {
        var discoveryRequest = new A2ADiscoveryRequest
        {
            Type = A2ADiscoveryType.ByCapability,
            Criteria = new Dictionary<string, object> { ["skill"] = skillName },
            IncludeCapabilities = true
        };

        var agents = await _transport.DiscoverAgentsAsync(discoveryRequest, cancellationToken);
        var capableAgents = agents.Where(a => a.Capabilities.Contains(skillName)).ToList();

        _logger.LogDebug("Found {AgentCount} agents capable of skill {SkillName}", capableAgents.Count, skillName);
        return capableAgents;
    }

    /// <summary>
    /// Start a conversation with another agent
    /// </summary>
    public async Task<string> StartConversationAsync(string targetAgentId, string topic, CancellationToken cancellationToken = default)
    {
        var conversationId = Guid.NewGuid().ToString();
        
        if (!_knownAgents.TryGetValue(targetAgentId, out var targetAgent))
        {
            throw new ArgumentException($"Agent {targetAgentId} not found");
        }

        var conversation = new A2AConversation
        {
            Id = conversationId,
            ParticipantIds = new List<string> { _configuration.LocalAgent.Id, targetAgentId },
            Topic = topic,
            StartedAt = DateTime.UtcNow,
            Status = A2AConversationStatus.Active
        };

        _activeConversations[conversationId] = conversation;

        // Send conversation invitation
        var inviteMessage = new A2AMessage
        {
            Id = Guid.NewGuid().ToString(),
            Type = A2AMessageType.Request,
            From = _configuration.LocalAgent,
            To = targetAgent,
            ConversationId = conversationId,
            Priority = A2APriority.Normal,
            Payload = new A2APayload
            {
                ContentType = A2APayloadTypes.CollaborationInvite,
                Content = new { topic, conversationId }
            }
        };

        await _transport.SendNotificationAsync(inviteMessage, cancellationToken);
        
        _logger.LogInformation("Started conversation {ConversationId} with agent {AgentName} on topic: {Topic}", 
            conversationId, targetAgent.Name, topic);

        return conversationId;
    }

    private async Task DiscoverNetworkAgentsAsync()
    {
        var discoveryRequest = new A2ADiscoveryRequest
        {
            Type = A2ADiscoveryType.All,
            IncludeCapabilities = true
        };

        var agents = await _transport.DiscoverAgentsAsync(discoveryRequest);
        
        foreach (var agent in agents)
        {
            _knownAgents[agent.Id] = agent;
        }

        _logger.LogDebug("Discovered {AgentCount} agents in the network", agents.Count);
    }

    private async Task<List<AgentIdentifier>> DiscoverAgentsByIdAsync(string agentId)
    {
        var discoveryRequest = new A2ADiscoveryRequest
        {
            Type = A2ADiscoveryType.ByName,
            Criteria = new Dictionary<string, object> { ["id"] = agentId }
        };

        return await _transport.DiscoverAgentsAsync(discoveryRequest);
    }
}

/// <summary>
/// Multi-agent workflow definition
/// </summary>
public class A2AWorkflow
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<A2AWorkflowStep> Steps { get; set; } = new();
    public Dictionary<string, object> GlobalContext { get; set; } = new();
    public TimeSpan? Timeout { get; set; }
}

/// <summary>
/// Individual step in a multi-agent workflow
/// </summary>
public class A2AWorkflowStep
{
    public required string Id { get; set; }
    public required string TargetAgentId { get; set; }
    public required A2ASkillRequest SkillRequest { get; set; }
    public int Order { get; set; }
    public bool IsRequired { get; set; } = true;
    public List<string> DependsOn { get; set; } = new();
    public TimeSpan? StepTimeout { get; set; }
}

/// <summary>
/// Result of a multi-agent workflow execution
/// </summary>
public class A2AWorkflowResult
{
    public required string WorkflowId { get; set; }
    public required bool Success { get; set; }
    public Dictionary<string, A2ASkillResponse> StepResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime CompletedAt { get; set; }
    public TimeSpan TotalDuration => CompletedAt - DateTime.UtcNow; // This would be calculated properly
}

/// <summary>
/// A2A conversation tracking
/// </summary>
public class A2AConversation
{
    public required string Id { get; set; }
    public List<string> ParticipantIds { get; set; } = new();
    public required string Topic { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public A2AConversationStatus Status { get; set; }
    public List<A2AMessage> Messages { get; set; } = new();
}

public enum A2AConversationStatus
{
    Pending,
    Active,
    Completed,
    Failed,
    Cancelled
}