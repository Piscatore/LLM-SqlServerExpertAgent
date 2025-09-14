using System.Text.Json.Serialization;

namespace SqlServerExpertAgent.Communication.AgentProtocol;

/// <summary>
/// Agent-to-Agent (A2A) protocol message structure
/// Based on the A2A standard specification for agent interoperability
/// </summary>
public class A2AMessage
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("type")]
    public required A2AMessageType Type { get; set; }

    [JsonPropertyName("from")]
    public required AgentIdentifier From { get; set; }

    [JsonPropertyName("to")]
    public required AgentIdentifier To { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }

    [JsonPropertyName("replyToMessageId")]
    public string? ReplyToMessageId { get; set; }

    [JsonPropertyName("priority")]
    public A2APriority Priority { get; set; } = A2APriority.Normal;

    [JsonPropertyName("ttl")]
    public int? TimeToLiveSeconds { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    [JsonPropertyName("payload")]
    public required A2APayload Payload { get; set; }

    [JsonPropertyName("security")]
    public A2ASecurityContext? Security { get; set; }
}

/// <summary>
/// Agent identifier in the A2A network
/// </summary>
public class AgentIdentifier
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }

    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; set; } = new();

    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    [JsonPropertyName("organizationId")]
    public string? OrganizationId { get; set; }
}

/// <summary>
/// Message payload containing the actual content
/// </summary>
public class A2APayload
{
    [JsonPropertyName("contentType")]
    public required string ContentType { get; set; } // "application/json", "text/plain", "application/x-skill-request"

    [JsonPropertyName("content")]
    public required object Content { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    [JsonPropertyName("schema")]
    public string? Schema { get; set; } // JSON Schema for validation

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "utf-8";
}

/// <summary>
/// Security context for A2A messages
/// </summary>
public class A2ASecurityContext
{
    [JsonPropertyName("authMethod")]
    public string? AuthMethod { get; set; } // "oauth2", "jwt", "mtls"

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("encryptionMethod")]
    public string? EncryptionMethod { get; set; }

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// A2A message types as per specification
/// </summary>
public enum A2AMessageType
{
    Request,
    Response,
    Event,
    Notification,
    Heartbeat,
    Discovery,
    Capability,
    Error,
    Acknowledgment
}

/// <summary>
/// Message priority levels
/// </summary>
public enum A2APriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4,
    Emergency = 5
}

/// <summary>
/// Specific payload types for different operations
/// </summary>
public static class A2APayloadTypes
{
    public const string SkillRequest = "application/x-skill-request";
    public const string SkillResponse = "application/x-skill-response";
    public const string AgentDiscovery = "application/x-agent-discovery";
    public const string CapabilityAdvertisement = "application/x-capability-advertisement";
    public const string TaskDelegation = "application/x-task-delegation";
    public const string CollaborationInvite = "application/x-collaboration-invite";
    public const string StatusUpdate = "application/x-status-update";
    public const string ErrorReport = "application/x-error-report";
}

/// <summary>
/// Skill request payload for A2A skill execution
/// </summary>
public class A2ASkillRequest
{
    [JsonPropertyName("skillName")]
    public required string SkillName { get; set; }

    [JsonPropertyName("operation")]
    public required string Operation { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();

    [JsonPropertyName("context")]
    public Dictionary<string, object> Context { get; set; } = new();

    [JsonPropertyName("timeout")]
    public int? TimeoutSeconds { get; set; }

    [JsonPropertyName("async")]
    public bool IsAsync { get; set; } = false;
}

/// <summary>
/// Skill response payload
/// </summary>
public class A2ASkillResponse
{
    [JsonPropertyName("success")]
    public required bool Success { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("executionTime")]
    public TimeSpan ExecutionTime { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Agent discovery payload
/// </summary>
public class A2ADiscoveryRequest
{
    [JsonPropertyName("discoveryType")]
    public required A2ADiscoveryType Type { get; set; }

    [JsonPropertyName("criteria")]
    public Dictionary<string, object> Criteria { get; set; } = new();

    [JsonPropertyName("includeCapabilities")]
    public bool IncludeCapabilities { get; set; } = true;
}

/// <summary>
/// Discovery response payload
/// </summary>
public class A2ADiscoveryResponse
{
    [JsonPropertyName("agents")]
    public List<AgentIdentifier> Agents { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }

    [JsonPropertyName("nextToken")]
    public string? NextToken { get; set; }
}

/// <summary>
/// Discovery types
/// </summary>
public enum A2ADiscoveryType
{
    ByCapability,
    ByType,
    ByName,
    ByOrganization,
    All
}