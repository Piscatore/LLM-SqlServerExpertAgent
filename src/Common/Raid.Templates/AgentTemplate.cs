namespace Raid.Templates;

/// <summary>
/// Template for creating agents declaratively
/// </summary>
public class AgentTemplate
{
    /// <summary>
    /// Template name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Template version
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Template description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Agent configuration
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Required capabilities
    /// </summary>
    public List<string> RequiredCapabilities { get; set; } = new();
}