# CLAUDE.md - SQL Server Expert Agent

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LLM-SqlServerExpertAgent is a specialized AI agent built with Microsoft Semantic Kernel that provides expert SQL Server assistance using SMO (SQL Server Management Objects). It features a highly configurable architecture with plugin separation for extensibility.

## Key Architecture Components

### Core Technologies
- **Microsoft Semantic Kernel 1.65.0**: AI orchestration and function calling
- **SQL Server Management Objects (SMO 172.76.0)**: Headless SSMS functionality  
- **Microsoft.Data.SqlClient 6.1.1**: Database connectivity
- **Plugin Architecture**: Assembly-separated extensions with hot-reload capability

### Project Structure
- `SqlServerExpertAgent.Core/`: Core agent implementation
  - `Configuration/`: Comprehensive configuration system with environment support
  - `Plugins/`: Plugin interfaces and core SQL Server plugin
- `SqlServerExpertAgent.Tests/`: Test-first development with xUnit, Moq, FluentAssertions

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

### Agent Collaboration Architecture *(New Design)*
- **Hybrid Architecture**: Shared core with specialized domain agents
- **VersionControlCore**: Centralized Git operations library for all version control needs
- **Domain Agents**: Specialized agents (DatabaseVersion, FileVersion, etc.) with domain expertise
- **Clean Boundaries**: Core handles "how to Git", agents handle "what to version and why"
- **No Duplication**: Single implementation of Git functionality shared across all agents
- **Atomic Operations**: Each domain agent owns complete workflows in their area
- **Future Ready**: Easy extension for LLM memory versioning, configuration versioning, etc.

## Development Commands

### Build and Test
```bash
dotnet build
dotnet test
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
- **Plugin Tests**: Loading, dependency resolution, hot-reload
- **Integration Tests**: End-to-end scenarios with real SQL Server

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