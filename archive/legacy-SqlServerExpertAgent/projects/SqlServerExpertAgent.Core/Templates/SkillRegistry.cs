using Microsoft.Extensions.Logging;
using System.Reflection;

namespace SqlServerExpertAgent.Templates;

/// <summary>
/// Registry for managing skill plugins and their lifecycle
/// Supports dynamic skill discovery, loading, and version management
/// </summary>
public class SkillRegistry
{
    private readonly Dictionary<string, List<ISkillPlugin>> _skills = new();
    private readonly Dictionary<string, Assembly> _skillAssemblies = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SkillRegistry> _logger;

    public SkillRegistry(IServiceProvider serviceProvider, ILogger<SkillRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Discover and load skills from specified directories
    /// </summary>
    public async Task<SkillLoadResult> LoadSkillsFromDirectoriesAsync(IEnumerable<string> directories)
    {
        var results = new List<SkillLoadResult.SkillResult>();

        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                _logger.LogWarning("Skill directory not found: {Directory}", directory);
                continue;
            }

            var skillFiles = Directory.GetFiles(directory, "*Skill.dll", SearchOption.TopDirectoryOnly);
            
            foreach (var skillFile in skillFiles)
            {
                try
                {
                    var result = await LoadSkillFromAssemblyAsync(skillFile);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load skill from {File}", skillFile);
                    results.Add(new SkillLoadResult.SkillResult(
                        Path.GetFileNameWithoutExtension(skillFile),
                        false,
                        ex.Message
                    ));
                }
            }
        }

        return new SkillLoadResult(results, DateTime.UtcNow);
    }

    /// <summary>
    /// Register a skill plugin instance
    /// </summary>
    public void RegisterSkill(ISkillPlugin skill)
    {
        var skillName = skill.SkillInfo.Name;
        
        if (!_skills.ContainsKey(skillName))
        {
            _skills[skillName] = new List<ISkillPlugin>();
        }

        _skills[skillName].Add(skill);
        _logger.LogInformation("Registered skill: {SkillName} v{Version}", 
            skillName, skill.SkillInfo.Version);
    }

    /// <summary>
    /// Get skill by name and minimum version
    /// </summary>
    public async Task<ISkillPlugin?> GetSkillAsync(string name, string? minVersion = null)
    {
        if (!_skills.ContainsKey(name))
            return null;

        var availableSkills = _skills[name];
        
        if (string.IsNullOrEmpty(minVersion))
        {
            // Return latest version
            return availableSkills.OrderByDescending(s => s.SkillInfo.Version).FirstOrDefault();
        }

        var minVersionParsed = Version.Parse(minVersion);
        var compatibleSkills = availableSkills
            .Where(s => s.SkillInfo.Version >= minVersionParsed)
            .OrderByDescending(s => s.SkillInfo.Version)
            .ToList();

        return compatibleSkills.FirstOrDefault();
    }

    /// <summary>
    /// Get all available skills
    /// </summary>
    public IEnumerable<ISkillPlugin> GetAllSkills()
    {
        return _skills.Values.SelectMany(skillList => skillList);
    }

    /// <summary>
    /// Get skills by category
    /// </summary>
    public IEnumerable<ISkillPlugin> GetSkillsByCategory(SkillCategory category)
    {
        return GetAllSkills().Where(skill => skill.SkillInfo.Category == category);
    }

    /// <summary>
    /// Find skills that provide specific capabilities
    /// </summary>
    public IEnumerable<ISkillPlugin> FindSkillsByCapability(string capability)
    {
        return GetAllSkills().Where(skill => skill.SkillInfo.Capabilities.Contains(capability));
    }

    /// <summary>
    /// Validate skill dependencies
    /// </summary>
    public async Task<SkillDependencyValidationResult> ValidateDependenciesAsync(IEnumerable<string> skillNames)
    {
        var validationResult = new SkillDependencyValidationResult();
        var resolvedSkills = new Dictionary<string, ISkillPlugin>();

        foreach (var skillName in skillNames)
        {
            var skill = await GetSkillAsync(skillName);
            if (skill == null)
            {
                validationResult.Errors.Add($"Skill '{skillName}' not found");
                continue;
            }

            resolvedSkills[skillName] = skill;
        }

        // Check compatibility between resolved skills
        foreach (var (name, skill) in resolvedSkills)
        {
            foreach (var compatibility in skill.SkillInfo.Compatibility)
            {
                switch (compatibility.Type)
                {
                    case CompatibilityType.Required:
                        if (!resolvedSkills.ContainsKey(compatibility.WithSkill))
                        {
                            validationResult.Errors.Add(
                                $"Skill '{name}' requires '{compatibility.WithSkill}' but it's not loaded");
                        }
                        break;

                    case CompatibilityType.Conflicts:
                        if (resolvedSkills.ContainsKey(compatibility.WithSkill))
                        {
                            validationResult.Errors.Add(
                                $"Skill '{name}' conflicts with '{compatibility.WithSkill}'");
                        }
                        break;

                    case CompatibilityType.Enhances:
                        if (!resolvedSkills.ContainsKey(compatibility.WithSkill))
                        {
                            validationResult.Warnings.Add(
                                $"Skill '{name}' works better with '{compatibility.WithSkill}' but it's not loaded");
                        }
                        break;
                }
            }
        }

        validationResult.IsValid = !validationResult.Errors.Any();
        return validationResult;
    }

    private async Task<SkillLoadResult.SkillResult> LoadSkillFromAssemblyAsync(string assemblyPath)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        
        var assembly = Assembly.LoadFrom(assemblyPath);
        _skillAssemblies[assemblyName] = assembly;

        // Find skill implementations
        var skillTypes = assembly.GetTypes()
            .Where(t => typeof(ISkillPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToArray();

        if (!skillTypes.Any())
        {
            return new SkillLoadResult.SkillResult(assemblyName, false, "No skill implementations found");
        }

        var loadedSkills = 0;
        foreach (var skillType in skillTypes)
        {
            try
            {
                var skill = (ISkillPlugin)Activator.CreateInstance(skillType)!;
                RegisterSkill(skill);
                loadedSkills++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to instantiate skill type: {SkillType}", skillType.Name);
            }
        }

        return new SkillLoadResult.SkillResult(
            assemblyName, 
            loadedSkills > 0, 
            $"Loaded {loadedSkills} of {skillTypes.Length} skills");
    }
}

/// <summary>
/// Result of skill loading operation
/// </summary>
public record SkillLoadResult(List<SkillLoadResult.SkillResult> Results, DateTime LoadTime)
{
    public record SkillResult(string Name, bool Success, string Message);
    
    public bool AllSuccessful => Results.All(r => r.Success);
    public int SuccessCount => Results.Count(r => r.Success);
    public int FailureCount => Results.Count(r => !r.Success);
}

/// <summary>
/// Result of skill dependency validation
/// </summary>
public class SkillDependencyValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Info { get; set; } = new();
}