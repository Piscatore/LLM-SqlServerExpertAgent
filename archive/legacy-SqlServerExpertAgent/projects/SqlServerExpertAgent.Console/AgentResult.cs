namespace SqlServerExpertAgent.Console;

/// <summary>
/// Result of agent operations
/// </summary>
public class AgentResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? Data { get; set; }
    public TimeSpan ExecutionTime { get; set; }

    public static AgentResult CreateSuccess(string message, string? data = null) =>
        new() { Success = true, Message = message, Data = data };

    public static AgentResult CreateError(string message) =>
        new() { Success = false, Message = message };
}