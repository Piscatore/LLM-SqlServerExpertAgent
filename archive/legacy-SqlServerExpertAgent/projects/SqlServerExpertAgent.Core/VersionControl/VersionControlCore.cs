using System.Diagnostics;
using System.Text;

namespace SqlServerExpertAgent.VersionControl;

/// <summary>
/// Shared Git operations library for all version control needs
/// Provides consistent Git functionality across database schemas, files, and other versioned content
/// </summary>
public class VersionControlCore
{
    private readonly string _workingDirectory;

    public VersionControlCore(string workingDirectory)
    {
        _workingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
    }

    #region Repository Management

    /// <summary>
    /// Initialize a new Git repository in the working directory
    /// </summary>
    public async Task<VersionControlResult> InitializeRepositoryAsync()
    {
        try
        {
            await RunGitCommandAsync("init");
            await RunGitCommandAsync("config user.email \"sqlexpert@agent.local\"");
            await RunGitCommandAsync("config user.name \"SQL Server Expert Agent\"");
            
            return VersionControlResult.CreateSuccess("Git repository initialized successfully");
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Failed to initialize repository: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if the current directory is inside a Git repository
    /// </summary>
    public async Task<bool> IsGitRepositoryAsync()
    {
        try
        {
            var result = await RunGitCommandAsync("rev-parse --is-inside-work-tree");
            return result.Output.Trim() == "true";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the current Git repository status
    /// </summary>
    public async Task<VersionControlResult> GetStatusAsync()
    {
        try
        {
            var result = await RunGitCommandAsync("status --porcelain");
            var hasChanges = !string.IsNullOrWhiteSpace(result.Output);
            
            return VersionControlResult.CreateSuccess(
                hasChanges ? "Repository has uncommitted changes" : "Working tree clean", 
                new { HasChanges = hasChanges, Changes = result.Output });
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Failed to get status: {ex.Message}");
        }
    }

    #endregion

    #region File Operations

    /// <summary>
    /// Add files to Git staging area
    /// </summary>
    public async Task<VersionControlResult> AddFilesAsync(params string[] filePaths)
    {
        try
        {
            foreach (var filePath in filePaths)
            {
                await RunGitCommandAsync($"add \"{filePath}\"");
            }
            
            return VersionControlResult.CreateSuccess($"Added {filePaths.Length} file(s) to staging area");
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Failed to add files: {ex.Message}");
        }
    }

    /// <summary>
    /// Add all files in a directory to Git staging area
    /// </summary>
    public async Task<VersionControlResult> AddDirectoryAsync(string directoryPath)
    {
        try
        {
            await RunGitCommandAsync($"add \"{directoryPath}\"");
            return VersionControlResult.CreateSuccess($"Added directory {directoryPath} to staging area");
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Failed to add directory: {ex.Message}");
        }
    }

    #endregion

    #region Commit Operations

    /// <summary>
    /// Create a Git commit with the specified message
    /// </summary>
    public async Task<VersionControlResult> CommitChangesAsync(string message, string? author = null)
    {
        try
        {
            var commitCommand = $"commit -m \"{EscapeCommitMessage(message)}\"";
            if (!string.IsNullOrEmpty(author))
            {
                commitCommand += $" --author=\"{author}\"";
            }
            
            var result = await RunGitCommandAsync(commitCommand);
            
            // Extract commit hash from output
            var commitHash = await GetCurrentCommitHashAsync();
            
            return VersionControlResult.CreateSuccess(
                $"Commit created successfully: {commitHash}",
                new { CommitHash = commitHash, Message = message });
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Failed to commit changes: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a commit with staged files
    /// </summary>
    public async Task<VersionControlResult> CommitStagedChangesAsync(string message, string? author = null)
    {
        return await CommitChangesAsync(message, author);
    }

    /// <summary>
    /// Add files and commit in a single operation
    /// </summary>
    public async Task<VersionControlResult> AddAndCommitAsync(string message, params string[] filePaths)
    {
        try
        {
            var addResult = await AddFilesAsync(filePaths);
            if (!addResult.Success)
                return addResult;
                
            return await CommitChangesAsync(message);
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Failed to add and commit: {ex.Message}");
        }
    }

    #endregion

    #region Branch Operations

    /// <summary>
    /// Create a new branch
    /// </summary>
    public async Task<VersionControlResult> CreateBranchAsync(string branchName)
    {
        try
        {
            await RunGitCommandAsync($"checkout -b {branchName}");
            return VersionControlResult.CreateSuccess($"Created and switched to branch: {branchName}");
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Failed to create branch: {ex.Message}");
        }
    }

    /// <summary>
    /// Switch to an existing branch
    /// </summary>
    public async Task<VersionControlResult> SwitchBranchAsync(string branchName)
    {
        try
        {
            await RunGitCommandAsync($"checkout {branchName}");
            return VersionControlResult.CreateSuccess($"Switched to branch: {branchName}");
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Failed to switch branch: {ex.Message}");
        }
    }

    /// <summary>
    /// Get current branch name
    /// </summary>
    public async Task<string> GetCurrentBranchAsync()
    {
        try
        {
            var result = await RunGitCommandAsync("branch --show-current");
            return result.Output.Trim();
        }
        catch
        {
            return "unknown";
        }
    }

    #endregion

    #region History and Diff Operations

    /// <summary>
    /// Get Git log with specified format and limit
    /// </summary>
    public async Task<VersionControlResult> GetHistoryAsync(int limit = 10, string? filePath = null)
    {
        try
        {
            var command = $"log --oneline -n {limit}";
            if (!string.IsNullOrEmpty(filePath))
            {
                command += $" -- \"{filePath}\"";
            }
            
            var result = await RunGitCommandAsync(command);
            
            return VersionControlResult.CreateSuccess("History retrieved successfully", 
                new { History = result.Output, Limit = limit, FilePath = filePath });
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Failed to get history: {ex.Message}");
        }
    }

    /// <summary>
    /// Get diff between two Git references (commits, branches, tags)
    /// </summary>
    public async Task<VersionControlResult> GetDiffBetweenRefsAsync(string fromRef, string toRef, string? filePath = null)
    {
        try
        {
            var command = $"diff {fromRef}..{toRef}";
            if (!string.IsNullOrEmpty(filePath))
            {
                command += $" -- \"{filePath}\"";
            }
            
            var result = await RunGitCommandAsync(command);
            
            return VersionControlResult.CreateSuccess("Diff retrieved successfully",
                new { FromRef = fromRef, ToRef = toRef, Diff = result.Output, FilePath = filePath });
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Failed to get diff: {ex.Message}");
        }
    }

    /// <summary>
    /// Get changes in working directory compared to HEAD
    /// </summary>
    public async Task<VersionControlResult> GetWorkingDirectoryDiffAsync(string? filePath = null)
    {
        return await GetDiffBetweenRefsAsync("HEAD", "HEAD", filePath);
    }

    #endregion

    #region Reference Operations

    /// <summary>
    /// Get current commit hash
    /// </summary>
    public async Task<string> GetCurrentCommitHashAsync()
    {
        try
        {
            var result = await RunGitCommandAsync("rev-parse HEAD");
            return result.Output.Trim();
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Get short commit hash
    /// </summary>
    public async Task<string> GetShortCommitHashAsync()
    {
        try
        {
            var result = await RunGitCommandAsync("rev-parse --short HEAD");
            return result.Output.Trim();
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Check if a reference (commit, branch, tag) exists
    /// </summary>
    public async Task<bool> RefExistsAsync(string reference)
    {
        try
        {
            await RunGitCommandAsync($"rev-parse --verify {reference}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Health and Diagnostics

    /// <summary>
    /// Check if Git is available and working
    /// </summary>
    public async Task<VersionControlResult> HealthCheckAsync()
    {
        try
        {
            var versionResult = await RunGitCommandAsync("--version");
            var isRepo = await IsGitRepositoryAsync();
            
            return VersionControlResult.CreateSuccess("Git is available and working",
                new { 
                    GitVersion = versionResult.Output.Trim(), 
                    IsRepository = isRepo,
                    WorkingDirectory = _workingDirectory 
                });
        }
        catch (Exception ex)
        {
            return VersionControlResult.CreateError($"Git health check failed: {ex.Message}");
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Execute a Git command and return the result
    /// </summary>
    private async Task<GitCommandResult> RunGitCommandAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start Git process");
        }

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        
        process.ErrorDataReceived += (_, e) => {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git command failed: {error}");
        }

        return new GitCommandResult
        {
            ExitCode = process.ExitCode,
            Output = output,
            Error = error
        };
    }

    /// <summary>
    /// Escape commit message to prevent injection issues
    /// </summary>
    private static string EscapeCommitMessage(string message)
    {
        return message.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    #endregion
}

/// <summary>
/// Result of a version control operation
/// </summary>
public record VersionControlResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public object? Data { get; init; }
    public string? ErrorDetails { get; init; }

    public static VersionControlResult CreateSuccess(string message, object? data = null)
        => new() { Success = true, Message = message, Data = data };

    public static VersionControlResult CreateError(string message, string? errorDetails = null)
        => new() { Success = false, Message = message, ErrorDetails = errorDetails };
}

/// <summary>
/// Result of a Git command execution
/// </summary>
internal record GitCommandResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
}