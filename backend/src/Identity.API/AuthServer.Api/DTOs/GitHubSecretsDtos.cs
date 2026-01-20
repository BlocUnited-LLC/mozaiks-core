namespace AuthServer.Api.DTOs;

/// <summary>
/// Request to set GitHub repository secrets for deployment.
/// </summary>
public sealed class SetRepositorySecretsRequest
{
    /// <summary>
    /// User ID for audit trail (optional, defaults to "system" for S2S).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Repository full name (e.g., "org/repo-name"). 
    /// Optional - if not provided, uses the app's linked repository.
    /// </summary>
    public string? RepoFullName { get; set; }

    /// <summary>
    /// Override secrets (optional). If not provided, uses configured defaults.
    /// Key is secret name (e.g., "DOCKERHUB_TOKEN"), value is the secret value.
    /// </summary>
    public Dictionary<string, string>? SecretOverrides { get; set; }

    /// <summary>
    /// Whether to include database connection string as a secret.
    /// Defaults to true.
    /// </summary>
    public bool IncludeDatabaseUri { get; set; } = true;

    /// <summary>
    /// Whether to include the app's API key as a secret.
    /// Defaults to true.
    /// </summary>
    public bool IncludeAppApiKey { get; set; } = true;
}

/// <summary>
/// Response from setting GitHub repository secrets.
/// </summary>
public sealed class SetRepositorySecretsResponse
{
    public bool Success { get; set; }
    public string RepoFullName { get; set; } = string.Empty;
    public List<string> SecretsSet { get; set; } = new();
    public List<string> SecretsFailed { get; set; } = new();
    public string? Error { get; set; }
}

/// <summary>
/// Request to get deployment status (workflow run status).
/// </summary>
public sealed class GetDeploymentStatusRequest
{
    /// <summary>
    /// App ID to check deployment status for.
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Repository full name (e.g., "org/repo-name").
    /// </summary>
    public string RepoFullName { get; set; } = string.Empty;

    /// <summary>
    /// User ID for audit trail.
    /// </summary>
    public string? UserId { get; set; }
    
    /// <summary>
    /// Specific workflow run ID to check (optional).
    /// If not provided, checks the latest run.
    /// </summary>
    public long? WorkflowRunId { get; set; }
    
    /// <summary>
    /// Workflow name to filter by.
    /// Defaults to "Deploy to Azure Container Apps".
    /// </summary>
    public string WorkflowName { get; set; } = "Deploy to Azure Container Apps";
}

/// <summary>
/// Response with deployment status and URLs.
/// </summary>
public sealed class DeploymentStatusResponse
{
    public bool Success { get; set; }
    public string AppId { get; set; } = string.Empty;
    public string RepoFullName { get; set; } = string.Empty;
    public string Status { get; set; } = "unknown"; // queued, in_progress, completed, failure
    public string? Conclusion { get; set; } // success, failure, cancelled, etc.
    public long? WorkflowRunId { get; set; }
    public string? WorkflowRunUrl { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Deployment URLs extracted from workflow artifacts.
    /// </summary>
    public DeploymentUrls? DeploymentUrls { get; set; }
    
    public string? Error { get; set; }
}

/// <summary>
/// Deployment URLs for frontend and backend.
/// </summary>
public sealed class DeploymentUrls
{
    public string? FrontendUrl { get; set; }
    public string? BackendUrl { get; set; }
    public string? CombinedUrl { get; set; }
}

/// <summary>
/// Internal GitHub API response for public key.
/// </summary>
internal sealed class GitHubPublicKeyResponse
{
    public string KeyId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Internal GitHub API response for workflow runs.
/// </summary>
internal sealed class GitHubWorkflowRunsResponse
{
    public int TotalCount { get; set; }
    public List<GitHubWorkflowRun> WorkflowRuns { get; set; } = new();
}

internal sealed class GitHubWorkflowRun
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Conclusion { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

internal sealed class GitHubArtifactsResponse
{
    public int TotalCount { get; set; }
    public List<GitHubArtifact> Artifacts { get; set; } = new();
}

internal sealed class GitHubArtifact
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ArchiveDownloadUrl { get; set; } = string.Empty;
}
