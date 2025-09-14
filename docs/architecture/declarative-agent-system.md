# Declarative Agent System Architecture

## Overview

The SqlServerExpertAgent project now includes a complete **Declarative Agent Template System** that enables creating specialized agents entirely through YAML/JSON configuration. This represents a major architectural evolution from hardcoded agents to configurable, template-based agent composition.

## Architecture Components

```mermaid
graph TB
    subgraph "Template System"
        AT[AgentTemplate] --> AF[DeclarativeAgentFactory]
        AF --> GA[GenericExpertAgent]
        SR[SkillRegistry] --> AF
        YT[YAML Templates] --> AF
        JT[JSON Templates] --> AF
    end
    
    subgraph "Skill System"
        ISkill[ISkillPlugin] --> SS[SqlServerSkill]
        ISkill --> GS[GitVersioningSkill]
        ISkill --> PS[PostgreSqlSkill]
        SR --> ISkill
    end
    
    subgraph "Agent Runtime"
        GA --> SK[Semantic Kernel]
        GA --> ISkill
        SK --> Functions[Kernel Functions]
    end
    
    subgraph "Configuration"
        BaseTemplate[database-expert-base.agent.yaml]
        SqlTemplate[sqlserver-expert.agent.yaml]
        PgTemplate[postgresql-expert.agent.yaml]
        BaseTemplate --> SqlTemplate
        BaseTemplate --> PgTemplate
    end
```

## Key Features

### 1. **Complete Declarative Creation**
```yaml
name: "PostgreSqlExpertAgent"
version: "1.0.0"
baseTemplate: "DatabaseExpertAgent"
personality:
  responseStyle: "expert_concise"
  expertiseLevel: "senior"
requiredSkills:
  - name: "PostgreSqlSkill"
    minVersion: "1.0.0"
```

### 2. **Template Inheritance**
- Base templates provide common functionality
- Specialized templates inherit and extend capabilities
- Configuration merging with override support
- Skill composition through inheritance

### 3. **Skill-Based Architecture**
- Skills are reusable capabilities
- Version management and compatibility checking
- Dynamic skill discovery and loading
- Cross-skill dependencies and conflict resolution

### 4. **Infrastructure Requirements**
- Declarative database requirements
- Service dependencies specification
- Resource requirements (CPU, memory, disk)
- Validation rules for template instantiation

## Template Examples

### Base Database Expert Template
```yaml
# database-expert-base.agent.yaml
name: "DatabaseExpertAgent"
version: "1.0.0"
personality:
  responseStyle: "expert_concise"
  expertiseLevel: "senior"
requiredSkills:
  - name: "DatabaseConnectionSkill"
    minVersion: "1.0.0"
infrastructure:
  databases:
    - name: "primary"
      type: "Database"
      required: true
  resources:
    minMemoryMB: 512
    maxMemoryMB: 2048
```

### SQL Server Specialist
```yaml  
# sqlserver-expert.agent.yaml
name: "SqlServerExpertAgent"
baseTemplate: "DatabaseExpertAgent"
requiredSkills:
  - name: "SqlServerSkill"
    configuration:
      useSMO: true
      enableSqlCommand: true
infrastructure:
  databases:
    - name: "sqlserver_primary"
      type: "SqlServer"
      minVersion: "2017"
```

### PostgreSQL Specialist
```yaml
# postgresql-expert.agent.yaml  
name: "PostgreSqlExpertAgent"
baseTemplate: "DatabaseExpertAgent"
requiredSkills:
  - name: "PostgreSqlSkill"
    configuration:
      useNpgsql: true
      enablePlPgSql: true
```

## Skill Plugin Interface

Skills extend the plugin system with template-specific capabilities:

```csharp
public interface ISkillPlugin : IAgentPlugin
{
    SkillMetadata SkillInfo { get; }
    Task<SkillResult> ExecuteSkillAsync(SkillRequest request);
    bool CanHandle(SkillRequest request);
    SkillConfigurationSchema GetConfigurationSchema();
}
```

### Skill Features
- **Capabilities Declaration**: What the skill can do
- **Infrastructure Requirements**: Dependencies on databases, services
- **Compatibility Rules**: Required, enhances, conflicts, replaces
- **Configuration Schema**: Validation for skill settings
- **Version Management**: Semantic versioning with compatibility

## Agent Creation Patterns

### 1. **Template-Based Creation**
```csharp
var factory = new DeclarativeAgentFactory(skillRegistry, serviceProvider, logger);
await factory.LoadTemplatesFromDirectoriesAsync(new[] { "templates" });
var agent = await factory.CreateAgentAsync("SqlServerExpertAgent", configuration);
```

### 2. **Inline Declaration**
```csharp
var declaration = new AgentDeclaration
{
    Name = "CustomSqlAgent",
    Extends = "DatabaseExpertAgent",
    Skills = new List<DeclarationSkill>
    {
        new() { Name = "SqlServerSkill", Required = true },
        new() { Name = "GitVersioningSkill", Required = false }
    }
};
var agent = await factory.CreateAgentAsync(declaration, configuration);
```

## Skill Registry System

The SkillRegistry manages skill lifecycle:

- **Dynamic Discovery**: Load skills from directories
- **Version Resolution**: Find compatible skill versions  
- **Dependency Validation**: Check skill compatibility
- **Category Filtering**: Group skills by functionality
- **Capability Matching**: Find skills for specific operations

## Benefits

### 1. **Rapid Agent Creation**
- New database expert agents in hours, not weeks
- Configuration-driven specialization
- No code changes for new database types

### 2. **Consistency and Reusability**
- Common patterns shared across agents
- Skill reuse across different agent types
- Template inheritance reduces duplication

### 3. **Maintainability**
- Updates to base templates benefit all derived agents
- Centralized skill management
- Clear separation of concerns

### 4. **Extensibility**
- Easy addition of new skills
- Mix and match capabilities
- Support for complex dependency scenarios

## Integration with Existing Architecture

The declarative system builds on our established foundation:

- **VersionControlCore**: Git operations shared across all agents
- **Plugin Architecture**: Extended with skill-based composition
- **Configuration System**: Enhanced with template merging
- **Semantic Kernel**: Functions registered from skill plugins

## Future Enhancements

### Agent-to-Agent Communication
- Template-based A2A protocol integration
- Skill-level communication capabilities
- Cross-agent skill sharing

### Dynamic Reconfiguration
- Runtime skill loading/unloading
- Template hot-reload
- Configuration updates without restart

### AI-Assisted Template Creation
- Generate templates from natural language descriptions
- Suggest optimal skill combinations
- Automatic dependency resolution

## Implementation Status

✅ **Completed**:
- Core template system architecture
- Skill plugin interface and registry
- YAML/JSON template loading
- Agent factory with inheritance
- SqlServerSkill conversion example
- Template validation system

🔄 **In Progress**:
- Additional skill implementations
- Advanced validation rules
- Performance optimization

📋 **Planned**:
- A2A protocol integration
- Template IDE tooling
- Skill marketplace concept

This declarative agent system represents a paradigm shift toward configuration-driven AI agent creation, enabling rapid specialization and deployment of expert agents across diverse technical domains.