using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SqlServerExpertAgent.Configuration;
using SqlServerExpertAgent.Plugins;

namespace SqlServerExpertAgent.Templates;

/// <summary>
/// Generic expert agent that can be configured declaratively through templates
/// Composes functionality through skill plugins rather than hardcoded implementation
/// </summary>
public class GenericExpertAgent : IExpertAgent, IDisposable
{
    private readonly AgentTemplate _template;
    private readonly AgentConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GenericExpertAgent> _logger;
    private readonly Dictionary<string, ISkillPlugin> _skills = new();
    private readonly SkillRegistry _skillRegistry;
    private Kernel? _kernel;
    private bool _initialized = false;

    public GenericExpertAgent(
        AgentTemplate template, 
        AgentConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<GenericExpertAgent> logger,
        SkillRegistry skillRegistry)
    {
        _template = template;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _skillRegistry = skillRegistry;
    }

    /// <summary>
    /// Agent identity configured from template
    /// </summary>
    public AgentIdentity Identity => new()
    {
        Name = _template.Name,
        Version = Version.Parse(_template.Version),
        Description = _template.Description,
        Personality = new AgentPersonalityConfiguration
        {
            ResponseStyle = _template.Personality.ResponseStyle,
            Authoritative = _template.Personality.Authoritative,
            ProactiveOptimization = _template.Personality.ProactiveOptimization,
            CustomAttributes = _template.Personality.CustomAttributes
        }
    };

    /// <summary>
    /// Available skills loaded from template
    /// </summary>
    public IReadOnlyDictionary<string, ISkillPlugin> Skills => _skills;

    /// <summary>
    /// Initialize agent with template-defined skills and configuration
    /// </summary>
    public virtual async Task InitializeAsync()
    {
        if (_initialized)
            throw new InvalidOperationException("Agent already initialized");

        _logger.LogInformation("Initializing agent {AgentName} from template {TemplateName}", 
            _template.Name, _template.Name);

        // Create Semantic Kernel
        var builder = Kernel.CreateBuilder();
        _kernel = builder.Build();

        // Load and validate skills
        await LoadRequiredSkillsAsync();
        await LoadOptionalSkillsAsync();

        // Validate skill compatibility
        await ValidateSkillCompatibilityAsync();

        // Register kernel functions from skills
        foreach (var skill in _skills.Values)
        {
            await skill.InitializeAsync(_configuration, _serviceProvider);
            skill.RegisterKernelFunctions(_kernel);
            _logger.LogDebug("Registered skill: {SkillName}", skill.SkillInfo.Name);
        }

        // Validate infrastructure requirements
        await ValidateInfrastructureAsync();

        _initialized = true;
        _logger.LogInformation("Agent {AgentName} initialized successfully with {SkillCount} skills", 
            _template.Name, _skills.Count);
    }

    /// <summary>
    /// Execute agent operation using appropriate skill
    /// </summary>
    public virtual async Task<AgentResponse> ExecuteAsync(AgentRequest request)
    {
        if (!_initialized)
            throw new InvalidOperationException("Agent not initialized");

        var requestId = Guid.NewGuid().ToString();
        _logger.LogInformation("Executing request {RequestId}: {Operation}", requestId, request.Operation);

        try
        {
            // Find appropriate skill for request
            var skill = await SelectSkillForRequestAsync(request);
            if (skill == null)
            {
                return AgentResponse.CreateError(requestId, 
                    $"No skill available to handle operation: {request.Operation}");
            }

            // Execute skill operation
            var skillRequest = new SkillRequest
            {
                RequestId = requestId,
                Operation = request.Operation,
                Parameters = request.Parameters,
                RequestingAgent = _template.Name,
                Priority = request.Priority,
                Timeout = request.Timeout,
                Context = request.Context
            };

            var skillResult = await skill.ExecuteSkillAsync(skillRequest);

            return new AgentResponse
            {
                RequestId = requestId,
                Success = skillResult.Success,
                Data = skillResult.Data,
                ErrorMessage = skillResult.ErrorMessage,
                Warnings = skillResult.Warnings,
                ExecutionTime = skillResult.ExecutionTime,
                Metadata = skillResult.Metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing request {RequestId}", requestId);
            return AgentResponse.CreateError(requestId, ex.Message);
        }
    }

    /// <summary>
    /// Get agent health status including skill health
    /// </summary>
    public async Task<AgentHealthStatus> GetHealthStatusAsync()
    {
        var healthStatus = new AgentHealthStatus
        {
            IsHealthy = true,
            Message = "Agent operational",
            Details = new Dictionary<string, object>(),
            Warnings = new List<string>(),
            Errors = new List<string>()
        };

        // Check skill health
        foreach (var (name, skill) in _skills)
        {
            try
            {
                var skillHealth = await skill.GetHealthStatusAsync();
                healthStatus.Details[$"skill_{name}"] = skillHealth;
                
                if (!skillHealth.IsHealthy)
                {
                    healthStatus.IsHealthy = false;
                    healthStatus.Errors.AddRange(skillHealth.Errors);
                }
                
                healthStatus.Warnings.AddRange(skillHealth.Warnings);
            }
            catch (Exception ex)
            {
                healthStatus.IsHealthy = false;
                healthStatus.Errors.Add($"Skill {name} health check failed: {ex.Message}");
            }
        }

        return healthStatus;
    }

    private async Task LoadRequiredSkillsAsync()
    {
        foreach (var skillReq in _template.RequiredSkills)
        {
            var skill = await _skillRegistry.GetSkillAsync(skillReq.Name, skillReq.MinVersion);
            if (skill == null)
            {
                throw new InvalidOperationException(
                    $"Required skill '{skillReq.Name}' (min version {skillReq.MinVersion}) not available");
            }

            _skills[skillReq.Name] = skill;
            _logger.LogDebug("Loaded required skill: {SkillName} v{Version}", 
                skillReq.Name, skill.SkillInfo.Version);
        }
    }

    private async Task LoadOptionalSkillsAsync()
    {
        foreach (var skillReq in _template.OptionalSkills)
        {
            var skill = await _skillRegistry.GetSkillAsync(skillReq.Name, skillReq.MinVersion);
            if (skill != null)
            {
                _skills[skillReq.Name] = skill;
                _logger.LogDebug("Loaded optional skill: {SkillName} v{Version}", 
                    skillReq.Name, skill.SkillInfo.Version);
            }
            else
            {
                _logger.LogInformation("Optional skill '{SkillName}' not available - continuing without it", 
                    skillReq.Name);
            }
        }
    }

    private async Task ValidateSkillCompatibilityAsync()
    {
        foreach (var skill in _skills.Values)
        {
            foreach (var compatibility in skill.SkillInfo.Compatibility)
            {
                if (compatibility.Type == CompatibilityType.Required && 
                    !_skills.ContainsKey(compatibility.WithSkill))
                {
                    throw new InvalidOperationException(
                        $"Skill '{skill.SkillInfo.Name}' requires '{compatibility.WithSkill}' but it's not loaded");
                }

                if (compatibility.Type == CompatibilityType.Conflicts && 
                    _skills.ContainsKey(compatibility.WithSkill))
                {
                    throw new InvalidOperationException(
                        $"Skill '{skill.SkillInfo.Name}' conflicts with '{compatibility.WithSkill}'");
                }
            }
        }

        await Task.CompletedTask;
    }

    private async Task ValidateInfrastructureAsync()
    {
        // Validate database requirements
        foreach (var dbReq in _template.Infrastructure.Databases.Where(d => d.Required))
        {
            // Check if database connection is available in configuration
            if (!_configuration.SqlServer.ConnectionStrings.ContainsKey(dbReq.Name))
            {
                throw new InvalidOperationException(
                    $"Required database '{dbReq.Name}' connection not configured");
            }
        }

        await Task.CompletedTask;
    }

    protected virtual async Task<ISkillPlugin?> SelectSkillForRequestAsync(AgentRequest request)
    {
        // Find skills that can handle this request
        var capableSkills = _skills.Values
            .Where(skill => skill.CanHandle(new SkillRequest
            {
                RequestId = "",
                Operation = request.Operation,
                Parameters = request.Parameters,
                RequestingAgent = _template.Name
            }))
            .ToList();

        if (!capableSkills.Any())
            return null;

        // For now, return first capable skill
        // Future: implement skill selection strategy (priority, load balancing, etc.)
        return capableSkills.First();
    }

    public void Dispose()
    {
        foreach (var skill in _skills.Values)
        {
            try
            {
                skill.DisposeAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing skill: {SkillName}", skill.SkillInfo.Name);
            }
        }

        // Kernel in Semantic Kernel 1.65.0 doesn't implement IDisposable
    }
}

/// <summary>
/// Interface for expert agents
/// </summary>
public interface IExpertAgent
{
    AgentIdentity Identity { get; }
    Task InitializeAsync();
    Task<AgentResponse> ExecuteAsync(AgentRequest request);
    Task<AgentHealthStatus> GetHealthStatusAsync();
}

/// <summary>
/// Agent identity information
/// </summary>
public class AgentIdentity
{
    public required string Name { get; set; }
    public required Version Version { get; set; }
    public required string Description { get; set; }
    public required AgentPersonalityConfiguration Personality { get; set; }
}

/// <summary>
/// Agent request
/// </summary>
public class AgentRequest
{
    public required string Operation { get; set; }
    public required Dictionary<string, object> Parameters { get; set; }
    public SkillPriority Priority { get; set; } = SkillPriority.Normal;
    public TimeSpan? Timeout { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Agent response
/// </summary>
public class AgentResponse
{
    public required string RequestId { get; set; }
    public required bool Success { get; set; }
    public required Dictionary<string, object> Data { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static AgentResponse CreateError(string requestId, string errorMessage) =>
        new() { RequestId = requestId, Success = false, Data = new(), ErrorMessage = errorMessage };
}

/// <summary>
/// Agent health status
/// </summary>
public class AgentHealthStatus
{
    public required bool IsHealthy { get; set; }
    public required string Message { get; set; }
    public required Dictionary<string, object> Details { get; set; }
    public required List<string> Warnings { get; set; }
    public required List<string> Errors { get; set; }
}

/// <summary>
/// Agent personality configuration
/// </summary>
public class AgentPersonalityConfiguration
{
    public required string ResponseStyle { get; set; }
    public required bool Authoritative { get; set; }
    public required bool ProactiveOptimization { get; set; }
    public required Dictionary<string, object> CustomAttributes { get; set; }
}