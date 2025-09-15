# CLAUDE.md - RAID Platform Database Agent

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

The RAID Platform Database Agent is a specialized AI agent built with Microsoft Semantic Kernel that provides expert SQL Server assistance using SMO (SQL Server Management Objects). It's part of the larger RAID Platform multi-agent ecosystem and features a highly configurable architecture with plugin separation for extensibility.

## Key Architecture Components

### Core Technologies
- **Microsoft Semantic Kernel 1.65.0**: AI orchestration and function calling
- **SQL Server Management Objects (SMO 172.76.0)**: Headless SSMS functionality  
- **Microsoft.Data.SqlClient 6.1.1**: Database connectivity
- **Plugin Architecture**: Assembly-separated extensions with hot-reload capability

### Project Structure
- `SqlServerExpertAgent.Console/`: Interactive CLI application with professional command-line interface
  - `Commands/`: Individual command implementations (validate, query, schema, etc.)
  - `AgentConsoleService.cs`: Central orchestration service
  - `InteractiveShell.cs`: Professional interactive shell implementation
- `SqlServerExpertAgent.Core/`: Core agent implementation and shared library
  - `Configuration/`: Comprehensive configuration system with environment support
  - `Plugins/`: Plugin interfaces and core SQL Server plugin
  - `Communication/`: Multi-agent communication (A2A protocol)
  - `Templates/`: Declarative agent templates and factories
- `SqlServerExpertAgent.Tests/`: Test-first development with xUnit, Moq, FluentAssertions (54+ tests)

## Development Philosophy

### Test-First Approach
This project follows "almost test-first" development:
1. Create comprehensive tests defining expected behavior
2. Implement functionality to make tests pass
3. Focus on validation, error handling, and edge cases

### High Configurability
The agent supports extensive configuration through:
- Multi-source loading (files, environment variables, dynamic detection)
- Environment-specific overrides (Development, Testing, Staging, Production)
- Project-specific contexts and business rules
- Plugin-specific settings and capabilities

### Performance Optimization
Designed for large knowledge volumes with:
- Multi-tiered caching (in-memory, file-based, vector search)
- Performance targets per operation type
- Resource-aware dynamic configuration
- Background tasks for maintenance

## Key Configuration Examples

### Agent Personality
```json
{
  "identity": {
    "personality": {
      "response_style": "expert_concise",  // vs "friendly_expert"
      "authoritative": true,
      "proactive_optimization": true
    }
  }
}
```

### Performance Targets
```json
{
  "performance": {
    "responseTargets": {
      "syntax_validation": 100,      // ms
      "schema_introspection": 500,
      "query_optimization": 1000,
      "complex_analysis": 3000
    }
  }
}
```

### Plugin Configuration
```json
{
  "plugins": {
    "pluginDirectories": ["plugins", "extensions"],
    "enableHotReload": true,
    "pluginSettings": {
      "SqlServerPlugin": {
        "enabled": true,
        "priority": 100
      }
    }
  }
}
```

## Core Capabilities

### SQL Server Operations (via SMO)
- **Syntax Validation**: Parse SQL without execution using SMO
- **Schema Introspection**: Comprehensive database metadata access
- **Query Analysis**: Execution plan analysis and optimization suggestions
- **Security Validation**: SQL injection detection and safety checks

### Plugin Architecture
- **Assembly Isolation**: Separate AppDomains for plugin loading
- **Hot-Reload**: Development-time plugin updates without restart
- **Dependency Resolution**: Topological sorting for initialization order
- **Health Monitoring**: Plugin diagnostics and error tracking

### Hybrid Console + Core Architecture *(Current Implementation)*
- **Console Application**: Interactive CLI with System.CommandLine framework
- **Core Library**: Shared agent engine with plugin architecture
- **Built-in Plugins**: SqlServerPlugin with comprehensive SMO integration
- **Plugin System**: Hot-reloadable plugins with assembly isolation
- **Configuration System**: Multi-environment support with JSON and environment variables
- **A2A Protocol**: Multi-agent communication for future expansion
- **Declarative Templates**: YAML/JSON configuration-driven agent creation

## Development Commands

### Build and Test
```bash
dotnet build                    # Build entire solution
dotnet test                     # Run all 54+ tests
cd SqlServerExpertAgent.Console
dotnet run -- --interactive     # Start interactive shell
dotnet run -- query "SELECT 1"   # Direct command execution
```

### Plugin Development
Plugins must implement `IAgentPlugin` and provide:
- Metadata with capabilities and dependencies
- Kernel function registration
- Health monitoring
- Async disposal

## Security and Safety

### Query Execution Safety
- **Default**: No data modification allowed
- **Security Scanning**: SQL injection pattern detection
- **Row Limits**: Maximum results returned per query
- **Timeout Protection**: Maximum execution time limits

### API Key Management
Following Exner project patterns:
- File-based keys: `claude-api-key.txt`
- Environment variables: `CLAUDE_API_KEY`
- Secure storage with masked logging

## Testing Framework

### Test Categories
- **Configuration Tests**: Loading, validation, environment overrides
- **SQL Validation Tests**: SMO integration, syntax checking, performance targets
- **Plugin Tests**: Loading, dependency resolution, hot-reload, built-in plugin functionality
- **Integration Tests**: End-to-end scenarios with real SQL Server connectivity
- **Console Tests**: Interactive shell, command parsing, error handling
- **Performance Tests**: Response time validation (100ms target for syntax validation)

### Key Test Patterns
- Bracket escaping validation: `[Column Name [with brackets]]`
- CTE scope validation
- Table alias requirements
- Performance target compliance (100ms for syntax validation)

## Related Projects
- **OrfPIM2**: ETL and PIM system providing real-world SQL Server scenarios
- **LLM-Collaboration-Improvement**: Research foundation for agent design patterns
- **Exner**: API key management and configuration patterns

## Important Instructions
- Follow test-first development approach
- Maintain high configurability for different use cases
- Use SMO for all SQL Server operations (headless SSMS)
- Implement proper plugin isolation for extensibility
- Performance targets must be met for production readiness