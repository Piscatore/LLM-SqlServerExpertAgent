using SqlServerExpertAgent.Plugins;

namespace SqlServerExpertAgent.Templates;

/// <summary>
/// Skill plugin interface that extends IAgentPlugin for template-based agent composition
/// Skills are reusable capabilities that can be combined declaratively
/// </summary>
public interface ISkillPlugin : IAgentPlugin
{
    /// <summary>
    /// Skill-specific metadata including capabilities and requirements
    /// </summary>
    SkillMetadata SkillInfo { get; }
    
    /// <summary>
    /// Execute a skill-specific operation
    /// </summary>
    Task<SkillResult> ExecuteSkillAsync(SkillRequest request);
    
    /// <summary>
    /// Check if this skill can handle a specific request type
    /// Used for dynamic skill selection
    /// </summary>
    bool CanHandle(SkillRequest request);
    
    /// <summary>
    /// Get skill configuration schema for validation
    /// </summary>
    SkillConfigurationSchema GetConfigurationSchema();
}

/// <summary>
/// Skill-specific metadata
/// </summary>
public class SkillMetadata
{
    public required string Name { get; set; }
    public required Version Version { get; set; }
    public required string Description { get; set; }
    public required SkillCategory Category { get; set; }
    public required List<string> Capabilities { get; set; }
    public required List<string> RequiredInfrastructure { get; set; }
    public List<string> OptionalInfrastructure { get; set; } = new();
    public List<SkillCompatibility> Compatibility { get; set; } = new();
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}

/// <summary>
/// Skill execution request
/// </summary>
public class SkillRequest
{
    public required string RequestId { get; set; }
    public required string Operation { get; set; }
    public required Dictionary<string, object> Parameters { get; set; }
    public required string RequestingAgent { get; set; }
    public SkillPriority Priority { get; set; } = SkillPriority.Normal;
    public TimeSpan? Timeout { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Skill execution result
/// </summary>
public class SkillResult
{
    public required string RequestId { get; set; }
    public required bool Success { get; set; }
    public required Dictionary<string, object> Data { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>
    /// Create successful result
    /// </summary>
    public static SkillResult CreateSuccess(string requestId, Dictionary<string, object> data) =>
        new() { RequestId = requestId, Success = true, Data = data };
    
    /// <summary>
    /// Create error result
    /// </summary>
    public static SkillResult CreateError(string requestId, string errorMessage) =>
        new() { RequestId = requestId, Success = false, Data = new(), ErrorMessage = errorMessage };
}

/// <summary>
/// Skill configuration schema for validation
/// </summary>
public class SkillConfigurationSchema
{
    public required Dictionary<string, ConfigurationProperty> Properties { get; set; }
    public List<string> RequiredProperties { get; set; } = new();
    public Dictionary<string, List<string>> ConditionalRequirements { get; set; } = new();
}

/// <summary>
/// Configuration property definition
/// </summary>
public class ConfigurationProperty
{
    public required string Type { get; set; } // "string", "integer", "boolean", "array", "object"
    public required string Description { get; set; }
    public object? DefaultValue { get; set; }
    public List<object>? AllowedValues { get; set; }
    public string? ValidationPattern { get; set; }
    public bool Required { get; set; } = false;
    public Dictionary<string, object>? NestedProperties { get; set; } // For object types
}

/// <summary>
/// Skill compatibility information
/// </summary>
public class SkillCompatibility
{
    public required string WithSkill { get; set; }
    public required CompatibilityType Type { get; set; }
    public string? Description { get; set; }
    public List<string> Requirements { get; set; } = new();
}

public enum SkillCategory
{
    Database,
    VersionControl,
    Security,
    Performance,
    Integration,
    Communication,
    Infrastructure,
    Analysis,
    Monitoring,
    Custom
}

public enum CompatibilityType
{
    Required,    // Must be used together
    Enhances,    // Work better together
    Conflicts,   // Cannot be used together
    Replaces     // This skill replaces the other
}