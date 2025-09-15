# RAID Platform CI/CD Environment Requirements

## Overview

This document outlines the infrastructure and environment requirements for the RAID Platform Continuous Integration and Continuous Deployment (CI/CD) pipeline.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Development Environment](#development-environment)
- [CI Environment](#ci-environment)
- [Production Environment](#production-environment)
- [GitHub Actions Configuration](#github-actions-configuration)
- [Security Requirements](#security-requirements)
- [Monitoring and Observability](#monitoring-and-observability)
- [Disaster Recovery](#disaster-recovery)
- [Troubleshooting](#troubleshooting)

## Prerequisites

### Required Software

| Software | Minimum Version | Purpose |
|----------|----------------|---------|
| .NET SDK | 9.0 | Building and running applications |
| Docker | 24.0+ | Containerization and testing |
| Git | 2.40+ | Version control |
| Node.js | 18.0+ | Frontend tooling (if applicable) |

### Optional Software

| Software | Version | Purpose |
|----------|---------|---------|
| Docker Compose | 2.20+ | Multi-container orchestration |
| kubectl | 1.28+ | Kubernetes deployment |
| Azure CLI | 2.50+ | Azure resource management |

## Development Environment

### Local Development Setup

#### System Requirements
- **OS**: Windows 10/11, macOS 12+, or Ubuntu 20.04+
- **RAM**: Minimum 8GB, Recommended 16GB
- **Storage**: 20GB free space
- **CPU**: 4+ cores recommended

#### Required Services
```bash
# Redis (for Memory Agent)
docker run -d --name redis-dev -p 6379:6379 redis:alpine

# SQL Server (for Database Agent)
docker run -d --name sqlserver-dev \
  -e "SA_PASSWORD=YourStrong@Passw0rd" \
  -e "ACCEPT_EULA=Y" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

#### Environment Variables
```bash
# Development configuration
export ASPNETCORE_ENVIRONMENT=Development
export Redis__ConnectionString="localhost:6379"
export SqlServer__ConnectionString="Server=localhost;Database=RaidDev;Integrated Security=true;"
```

### Development Tools Configuration

#### IDE Settings
- **Visual Studio Code**: Recommended extensions in `.vscode/extensions.json`
- **Visual Studio**: Solution configuration in `RaidPlatform.sln`
- **JetBrains Rider**: Settings in `.idea/` directory

#### Code Quality Tools
- **EditorConfig**: `.editorconfig` for consistent formatting
- **Code Analysis**: `CodeAnalysis.ruleset` for static analysis
- **Build Props**: `Directory.Build.props` for common settings

## CI Environment

### GitHub Actions Requirements

#### Runner Specifications
- **OS**: `ubuntu-latest` (Ubuntu 22.04)
- **Memory**: 7GB available
- **CPU**: 2 cores
- **Storage**: 14GB SSD space

#### Required Actions and Marketplace Tools
```yaml
# Essential GitHub Actions
- actions/checkout@v4
- actions/setup-dotnet@v4
- docker/setup-buildx-action@v3
- docker/build-push-action@v5
- codecov/codecov-action@v4
```

#### Environment Variables
```yaml
env:
  DOTNET_VERSION: '9.0.x'
  BUILD_CONFIGURATION: 'Release'
  ASPNETCORE_ENVIRONMENT: 'CI'
```

### Service Dependencies

#### Test Infrastructure
```yaml
services:
  redis:
    image: redis:alpine
    ports:
      - 6379:6379
    options: >-
      --health-cmd "redis-cli ping"
      --health-interval 10s
      --health-timeout 5s
      --health-retries 5

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    env:
      SA_PASSWORD: YourStrong@Passw0rd
      ACCEPT_EULA: Y
    ports:
      - 1433:1433
    options: >-
      --health-cmd "/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourStrong@Passw0rd -Q 'SELECT 1'"
      --health-interval 30s
      --health-timeout 10s
      --health-retries 5
```

#### Test Execution Requirements
- **Testcontainers**: For integration testing with real databases
- **WireMock**: For mocking external HTTP services
- **xUnit**: Primary testing framework
- **Coverage**: Code coverage collection with Coverlet

### Build Pipeline Stages

#### 1. Code Quality Gate
```bash
# Static analysis and linting
dotnet build --configuration Release -p:RunAnalyzersDuringBuild=true
dotnet format --verify-no-changes
```

#### 2. Unit Testing
```bash
# Fast-running unit tests
dotnet test --configuration Release \
  --logger trx \
  --collect:"XPlat Code Coverage" \
  --results-directory TestResults
```

#### 3. Integration Testing
```bash
# Integration tests with external dependencies
docker-compose -f docker/docker-compose.ci.yml up -d
dotnet test tests/Integration/ --configuration Release
```

#### 4. Docker Build Verification
```bash
# Verify all Docker images build successfully
./scripts/ci/verify-docker-builds.sh
```

## Production Environment

### Infrastructure Requirements

#### Compute Resources
- **CPU**: 4+ cores per agent instance
- **Memory**: 8GB+ per agent instance
- **Storage**: 100GB+ for persistent data
- **Network**: 1Gbps+ bandwidth

#### Container Orchestration
```yaml
# Kubernetes resource requirements
resources:
  requests:
    memory: "512Mi"
    cpu: "250m"
  limits:
    memory: "2Gi"
    cpu: "1000m"
```

#### Database Requirements
- **SQL Server**: 2022 Enterprise or Standard
  - Memory: 16GB+
  - Storage: 500GB+ SSD
  - Backup: Automated daily backups
- **Redis**: 7.0+ Cluster or Standalone
  - Memory: 8GB+
  - Persistence: RDB snapshots + AOF

### External Dependencies

#### Azure Services
- **Azure OpenAI**: For AI model integration
- **Azure Key Vault**: For secret management
- **Azure Monitor**: For observability
- **Azure Container Registry**: For image storage

#### Required Environment Variables
```bash
# Production configuration
AZURE_OPENAI_ENDPOINT=${AZURE_OPENAI_ENDPOINT}
AZURE_OPENAI_API_KEY=${AZURE_OPENAI_API_KEY}
KEY_VAULT_URL=${KEY_VAULT_URL}
REDIS_CONNECTION_STRING=${REDIS_CONNECTION_STRING}
SQL_CONNECTION_STRING=${SQL_CONNECTION_STRING}
```

## GitHub Actions Configuration

### Repository Secrets

#### Required Secrets
```
AZURE_OPENAI_API_KEY          # Azure OpenAI service key
DOCKER_REGISTRY_USERNAME      # Container registry credentials
DOCKER_REGISTRY_PASSWORD      # Container registry credentials
CODECOV_TOKEN                 # Code coverage reporting
DEPLOYMENT_KEY                # SSH key for deployments
```

#### Optional Secrets
```
SLACK_WEBHOOK_URL            # Build notifications
TEAMS_WEBHOOK_URL            # Build notifications
SONAR_TOKEN                  # Code quality analysis
```

### Workflow Configuration

#### Branch Protection Rules
- **Main Branch**: Require PR reviews, status checks
- **Develop Branch**: Require status checks
- **Feature Branches**: No restrictions

#### Automated Workflows
1. **Pull Request**: Build, test, quality checks
2. **Main Branch**: Full pipeline + deployment to staging
3. **Release Tags**: Production deployment
4. **Scheduled**: Nightly security scans and dependency updates

### Performance Requirements

#### Build Time Targets
- **Unit Tests**: < 5 minutes
- **Integration Tests**: < 15 minutes
- **Full Pipeline**: < 30 minutes
- **Docker Builds**: < 10 minutes per image

#### Resource Limits
- **Concurrent Jobs**: Maximum 5
- **Artifact Retention**: 30 days
- **Log Retention**: 90 days

## Security Requirements

### Code Security

#### Dependency Management
- **Dependabot**: Automated dependency updates
- **Vulnerability Scanning**: GitHub security advisories
- **License Compliance**: Automated license checking

#### Secret Management
- **GitHub Secrets**: For CI/CD credentials
- **Azure Key Vault**: For production secrets
- **Secret Rotation**: Quarterly rotation schedule

### Container Security

#### Image Scanning
```bash
# Security scanning in CI pipeline
docker scout quickview image:tag
trivy image --severity HIGH,CRITICAL image:tag
```

#### Image Hardening
- Non-root user execution
- Minimal base images (distroless when possible)
- Multi-stage builds to reduce attack surface
- Regular base image updates

### Network Security

#### Access Control
- **GitHub**: Repository access via teams
- **Container Registry**: Role-based access
- **Production**: Network segmentation and firewalls

## Monitoring and Observability

### Application Monitoring

#### Metrics Collection
- **Application Insights**: Application performance monitoring
- **Prometheus**: Custom metrics and alerts
- **Health Checks**: Kubernetes liveness/readiness probes

#### Logging
- **Structured Logging**: JSON format with correlation IDs
- **Log Aggregation**: Azure Monitor or ELK stack
- **Log Retention**: 90 days minimum

### CI/CD Monitoring

#### Pipeline Metrics
- Build success/failure rates
- Test execution times
- Deployment frequency
- Lead time for changes

#### Alerting
- **Build Failures**: Immediate notification
- **Test Failures**: Immediate notification
- **Security Vulnerabilities**: High priority alerts
- **Performance Degradation**: Warning alerts

## Disaster Recovery

### Backup Strategy

#### Code and Configuration
- **Git Repositories**: Multiple remotes (GitHub + Azure DevOps)
- **Infrastructure as Code**: Versioned Terraform/ARM templates
- **Configuration**: Backed up configuration files

#### Data Backup
- **Database**: Automated daily backups with point-in-time recovery
- **Redis**: RDB snapshots and AOF persistence
- **Artifacts**: Mirror critical artifacts to multiple regions

### Recovery Procedures

#### CI/CD System Recovery
1. **GitHub Actions**: Use backup runner infrastructure
2. **Container Registry**: Switch to backup registry
3. **Secrets**: Retrieve from backup Key Vault

#### Production Recovery
1. **Database**: Restore from latest backup
2. **Application**: Deploy last known good version
3. **Traffic**: Route to healthy instances

### Business Continuity

#### RTO/RPO Targets
- **Recovery Time Objective (RTO)**: 4 hours
- **Recovery Point Objective (RPO)**: 1 hour
- **Mean Time to Recovery (MTTR)**: 2 hours

## Troubleshooting

### Common CI/CD Issues

#### Build Failures
```bash
# Check build logs
gh run view --log

# Local reproduction
./scripts/ci/build-and-test.sh
```

#### Test Failures
```bash
# Run specific test project
dotnet test tests/Raid.Memory.Tests/ --logger console

# Run with detailed output
dotnet test --verbosity diagnostic
```

#### Docker Issues
```bash
# Verify Docker configuration
./scripts/ci/verify-docker-builds.sh

# Check container logs
docker logs container-name
```

### Performance Issues

#### Slow Builds
- Check runner capacity and utilization
- Optimize dependency restoration with caching
- Parallelize test execution where possible

#### Resource Constraints
- Monitor runner memory and CPU usage
- Consider upgrading to larger runners
- Optimize Docker layer caching

### Support Contacts

#### Development Team
- **Primary**: Development Team Lead
- **Secondary**: DevOps Engineer
- **Escalation**: Engineering Manager

#### Infrastructure Team
- **Primary**: Platform Engineer
- **Secondary**: Site Reliability Engineer
- **Escalation**: Infrastructure Manager

---

## Document Information

**Version**: 1.0.0
**Last Updated**: December 2024
**Next Review**: March 2025
**Owner**: RAID Platform Team

For questions or updates to this document, please create an issue in the [RAID Platform repository](https://github.com/raid-platform/raid-platform/issues).