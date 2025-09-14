using SqlServerExpertAgent.Configuration;
using SqlServerExpertAgent.Plugins;

namespace SqlServerExpertAgent.Templates;

/// <summary>
/// Declarative agent template definition for creating specialized agents
/// Enables configuration-driven agent creation with skill composition
/// </summary>
public class AgentTemplate
{
    public required string Name { get; set; }
    public required string Version { get; set; }
    public required string Description { get; set; }
    
    /// <summary>
    /// Base template to inherit from (optional)
    /// Enables template inheritance and composition
    /// </summary>
    public string? BaseTemplate { get; set; }
    
    /// <summary>
    /// Agent personality and behavior configuration
    /// </summary>
    public required TemplatePersonality Personality { get; set; }
    
    /// <summary>
    /// Required skills that must be present
    /// </summary>
    public required List<SkillRequirement> RequiredSkills { get; set; }
    
    /// <summary>
    /// Optional skills that enhance capabilities
    /// </summary>
    public List<SkillRequirement> OptionalSkills { get; set; } = new();
    
    /// <summary>
    /// Default configuration overrides
    /// Merged with base configuration at runtime
    /// </summary>
    public Dictionary<string, object> DefaultConfiguration { get; set; } = new();
    
    /// <summary>
    /// Infrastructure requirements (databases, services, etc.)
    /// </summary>
    public InfrastructureRequirements Infrastructure { get; set; } = new();
    
    /// <summary>
    /// Validation rules for template instantiation
    /// </summary>
    public List<TemplateValidationRule> ValidationRules { get; set; } = new();
}

/// <summary>
/// Agent personality configuration for template
/// </summary>
public class TemplatePersonality
{
    public required string ResponseStyle { get; set; } // "expert_concise", "friendly_expert", "enterprise_formal"
    public required string ExpertiseLevel { get; set; } // "junior", "senior", "architect", "specialist"
    public bool Authoritative { get; set; } = true;
    public bool ProactiveOptimization { get; set; } = true;
    public string ToneProfile { get; set; } = "professional";
    public Dictionary<string, object> CustomAttributes { get; set; } = new();
}

/// <summary>
/// Skill requirement with version and configuration constraints
/// </summary>
public class SkillRequirement
{
    public required string Name { get; set; }
    public string MinVersion { get; set; } = "1.0.0";
    public string? MaxVersion { get; set; }
    public SkillPriority Priority { get; set; } = SkillPriority.Normal;
    public Dictionary<string, object> Configuration { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Infrastructure requirements for agent operation
/// </summary>
public class InfrastructureRequirements
{
    public List<DatabaseRequirement> Databases { get; set; } = new();
    public List<ServiceRequirement> Services { get; set; } = new();
    public List<string> ContainerImages { get; set; } = new();
    public ResourceRequirements Resources { get; set; } = new();
}

/// <summary>
/// Database infrastructure requirement
/// </summary>
public class DatabaseRequirement
{
    public required string Name { get; set; }
    public required string Type { get; set; } // "SqlServer", "PostgreSQL", "MongoDB"
    public required string Purpose { get; set; } // "primary", "logging", "cache"
    public string MinVersion { get; set; } = "1.0";
    public bool Required { get; set; } = true;
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// External service requirement
/// </summary>
public class ServiceRequirement
{
    public required string Name { get; set; }
    public required string Type { get; set; } // "REST", "GraphQL", "gRPC", "MessageQueue"
    public required string Purpose { get; set; }
    public bool Required { get; set; } = true;
    public string? HealthCheckUrl { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Resource requirements for agent operation
/// </summary>
public class ResourceRequirements
{
    public int MinMemoryMB { get; set; } = 512;
    public int MaxMemoryMB { get; set; } = 2048;
    public int MinCpuCores { get; set; } = 1;
    public int MaxCpuCores { get; set; } = 4;
    public int DiskSpaceMB { get; set; } = 1024;
}

/// <summary>
/// Template validation rule
/// </summary>
public class TemplateValidationRule
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string ValidationExpression { get; set; } // C# expression or custom validator
    public string ErrorMessage { get; set; } = "";
    public bool IsCritical { get; set; } = true;
}

public enum SkillPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}