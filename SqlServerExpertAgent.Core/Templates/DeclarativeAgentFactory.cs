using Microsoft.Extensions.Logging;
using SqlServerExpertAgent.Configuration;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SqlServerExpertAgent.Templates;

/// <summary>
/// Factory for creating agents from declarative templates (YAML/JSON)
/// Enables complete agent creation through configuration
/// </summary>
public class DeclarativeAgentFactory
{
    private readonly Dictionary<string, AgentTemplate> _templates = new();
    private readonly SkillRegistry _skillRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeclarativeAgentFactory> _logger;
    private readonly IDeserializer _yamlDeserializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public DeclarativeAgentFactory(
        SkillRegistry skillRegistry,
        IServiceProvider serviceProvider,
        ILogger<DeclarativeAgentFactory> logger)
    {
        _skillRegistry = skillRegistry;
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    /// <summary>
    /// Load templates from specified directories
    /// Supports both YAML and JSON template files
    /// </summary>
    public async Task LoadTemplatesFromDirectoriesAsync(IEnumerable<string> directories)
    {
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                _logger.LogWarning("Template directory not found: {Directory}", directory);
                continue;
            }

            // Load YAML templates
            var yamlFiles = Directory.GetFiles(directory, "*.agent.yaml", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(directory, "*.agent.yml", SearchOption.AllDirectories));

            foreach (var yamlFile in yamlFiles)
            {
                try
                {
                    await LoadTemplateFromYamlAsync(yamlFile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load YAML template: {File}", yamlFile);
                }
            }

            // Load JSON templates
            var jsonFiles = Directory.GetFiles(directory, "*.agent.json", SearchOption.AllDirectories);
            
            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    await LoadTemplateFromJsonAsync(jsonFile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load JSON template: {File}", jsonFile);
                }
            }
        }
    }

    /// <summary>
    /// Create agent from template name
    /// </summary>
    public async Task<IExpertAgent> CreateAgentAsync(string templateName, AgentConfiguration configuration)
    {
        if (!_templates.TryGetValue(templateName, out var template))
        {
            throw new ArgumentException($"Template '{templateName}' not found", nameof(templateName));
        }

        return await CreateAgentFromTemplateAsync(template, configuration);
    }

    /// <summary>
    /// Create agent from inline template declaration
    /// </summary>
    public async Task<IExpertAgent> CreateAgentAsync(AgentDeclaration declaration, AgentConfiguration configuration)
    {
        // Resolve base template if specified
        AgentTemplate? baseTemplate = null;
        if (!string.IsNullOrEmpty(declaration.Extends))
        {
            if (!_templates.TryGetValue(declaration.Extends, out baseTemplate))
            {
                throw new ArgumentException($"Base template '{declaration.Extends}' not found", nameof(declaration));
            }
        }

        // Convert declaration to template
        var template = await ConvertDeclarationToTemplateAsync(declaration, baseTemplate);
        
        return await CreateAgentFromTemplateAsync(template, configuration);
    }

    /// <summary>
    /// Get available template names
    /// </summary>
    public IEnumerable<string> GetAvailableTemplates()
    {
        return _templates.Keys;
    }

    /// <summary>
    /// Get template by name
    /// </summary>
    public AgentTemplate? GetTemplate(string name)
    {
        return _templates.TryGetValue(name, out var template) ? template : null;
    }

    /// <summary>
    /// Validate template and its requirements
    /// </summary>
    public async Task<TemplateValidationResult> ValidateTemplateAsync(string templateName)
    {
        if (!_templates.TryGetValue(templateName, out var template))
        {
            return new TemplateValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Template '{templateName}' not found" }
            };
        }

        return await ValidateTemplateAsync(template);
    }

    private async Task<IExpertAgent> CreateAgentFromTemplateAsync(AgentTemplate template, AgentConfiguration configuration)
    {
        _logger.LogInformation("Creating agent from template: {TemplateName}", template.Name);

        // Validate template requirements
        var validation = await ValidateTemplateAsync(template);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Template validation failed: {string.Join(", ", validation.Errors)}");
        }

        // Merge template configuration with provided configuration
        var mergedConfiguration = MergeConfigurations(configuration, template.DefaultConfiguration);

        // Create generic agent instance
        var logger = _serviceProvider.GetService(typeof(ILogger<GenericExpertAgent>)) as ILogger<GenericExpertAgent>;
        var agent = new GenericExpertAgent(template, mergedConfiguration, _serviceProvider, logger!, _skillRegistry);

        // Initialize the agent
        await agent.InitializeAsync();

        _logger.LogInformation("Successfully created agent: {AgentName}", template.Name);
        return agent;
    }

    private async Task LoadTemplateFromYamlAsync(string filePath)
    {
        var yaml = await File.ReadAllTextAsync(filePath);
        var template = _yamlDeserializer.Deserialize<AgentTemplate>(yaml);
        
        _templates[template.Name] = template;
        _logger.LogDebug("Loaded YAML template: {TemplateName} from {File}", template.Name, filePath);
    }

    private async Task LoadTemplateFromJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var template = JsonSerializer.Deserialize<AgentTemplate>(json, _jsonOptions)!;
        
        _templates[template.Name] = template;
        _logger.LogDebug("Loaded JSON template: {TemplateName} from {File}", template.Name, filePath);
    }

    private async Task<AgentTemplate> ConvertDeclarationToTemplateAsync(AgentDeclaration declaration, AgentTemplate? baseTemplate)
    {
        var template = new AgentTemplate
        {
            Name = declaration.Name,
            Version = declaration.Version ?? "1.0.0",
            Description = declaration.Description ?? $"Agent created from declaration: {declaration.Name}",
            BaseTemplate = baseTemplate?.Name,
            Personality = new TemplatePersonality
            {
                ResponseStyle = declaration.Personality?.ResponseStyle ?? baseTemplate?.Personality.ResponseStyle ?? "expert_concise",
                ExpertiseLevel = declaration.Personality?.ExpertiseLevel ?? baseTemplate?.Personality.ExpertiseLevel ?? "senior",
                Authoritative = declaration.Personality?.Authoritative ?? baseTemplate?.Personality.Authoritative ?? true,
                ProactiveOptimization = declaration.Personality?.ProactiveOptimization ?? baseTemplate?.Personality.ProactiveOptimization ?? true
            },
            RequiredSkills = declaration.Skills
                .Where(s => s.Required)
                .Select(s => new SkillRequirement 
                { 
                    Name = s.Name, 
                    MinVersion = s.Version ?? "1.0.0",
                    Configuration = s.Configuration ?? new Dictionary<string, object>()
                })
                .ToList(),
            OptionalSkills = declaration.Skills
                .Where(s => !s.Required)
                .Select(s => new SkillRequirement 
                { 
                    Name = s.Name, 
                    MinVersion = s.Version ?? "1.0.0",
                    Configuration = s.Configuration ?? new Dictionary<string, object>()
                })
                .ToList(),
            DefaultConfiguration = declaration.Configuration ?? new Dictionary<string, object>(),
            Infrastructure = declaration.Infrastructure ?? new InfrastructureRequirements()
        };

        // Inherit from base template if specified
        if (baseTemplate != null)
        {
            template = await InheritFromBaseTemplateAsync(template, baseTemplate);
        }

        return template;
    }

    private async Task<AgentTemplate> InheritFromBaseTemplateAsync(AgentTemplate template, AgentTemplate baseTemplate)
    {
        // Merge skills (template skills override base)
        var allRequiredSkills = baseTemplate.RequiredSkills.Concat(template.RequiredSkills)
            .GroupBy(s => s.Name)
            .Select(g => g.Last()) // Last one wins (template overrides base)
            .ToList();

        var allOptionalSkills = baseTemplate.OptionalSkills.Concat(template.OptionalSkills)
            .GroupBy(s => s.Name)
            .Select(g => g.Last())
            .ToList();

        template.RequiredSkills = allRequiredSkills;
        template.OptionalSkills = allOptionalSkills;

        // Merge configurations (template overrides base)
        foreach (var (key, value) in baseTemplate.DefaultConfiguration)
        {
            if (!template.DefaultConfiguration.ContainsKey(key))
            {
                template.DefaultConfiguration[key] = value;
            }
        }

        return template;
    }

    private async Task<TemplateValidationResult> ValidateTemplateAsync(AgentTemplate template)
    {
        var result = new TemplateValidationResult();

        // Validate skill dependencies
        var allSkillNames = template.RequiredSkills.Concat(template.OptionalSkills).Select(s => s.Name);
        var dependencyValidation = await _skillRegistry.ValidateDependenciesAsync(allSkillNames);
        
        result.Errors.AddRange(dependencyValidation.Errors);
        result.Warnings.AddRange(dependencyValidation.Warnings);

        // Validate template-specific rules
        foreach (var rule in template.ValidationRules)
        {
            var ruleResult = await ValidateTemplateRuleAsync(template, rule);
            if (!ruleResult.IsValid)
            {
                if (rule.IsCritical)
                {
                    result.Errors.Add($"Validation rule '{rule.Name}' failed: {rule.ErrorMessage}");
                }
                else
                {
                    result.Warnings.Add($"Validation rule '{rule.Name}' failed: {rule.ErrorMessage}");
                }
            }
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private async Task<ValidationRuleResult> ValidateTemplateRuleAsync(AgentTemplate template, TemplateValidationRule rule)
    {
        // Simple validation - in a real implementation, you'd have a more sophisticated expression evaluator
        // For now, just return success
        return new ValidationRuleResult { IsValid = true };
    }

    private AgentConfiguration MergeConfigurations(AgentConfiguration baseConfig, Dictionary<string, object> overrides)
    {
        // Simple merge - in real implementation, you'd do deep merge with proper type handling
        // For now, return the base configuration
        return baseConfig;
    }
}

/// <summary>
/// Agent declaration for inline creation
/// </summary>
public class AgentDeclaration
{
    public required string Name { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Extends { get; set; } // Base template name
    public DeclarationPersonality? Personality { get; set; }
    public List<DeclarationSkill> Skills { get; set; } = new();
    public Dictionary<string, object>? Configuration { get; set; }
    public InfrastructureRequirements? Infrastructure { get; set; }
}

/// <summary>
/// Personality declaration
/// </summary>
public class DeclarationPersonality
{
    public string? ResponseStyle { get; set; }
    public string? ExpertiseLevel { get; set; }
    public bool? Authoritative { get; set; }
    public bool? ProactiveOptimization { get; set; }
}

/// <summary>
/// Skill declaration
/// </summary>
public class DeclarationSkill
{
    public required string Name { get; set; }
    public string? Version { get; set; }
    public bool Required { get; set; } = true;
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// Template validation result
/// </summary>
public class TemplateValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Info { get; set; } = new();
}

/// <summary>
/// Validation rule result
/// </summary>
public class ValidationRuleResult
{
    public bool IsValid { get; set; }
    public string? Message { get; set; }
}