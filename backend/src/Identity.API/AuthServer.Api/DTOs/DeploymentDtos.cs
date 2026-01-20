namespace AuthServer.Api.DTOs;

public sealed class DeployAppRequest
{
    public string? BundleUrl { get; set; }
    public string? BundleBase64 { get; set; }
    public string? RepoName { get; set; }
    public string? Branch { get; set; }
    public string? CommitMessage { get; set; }

    /// <summary>
    /// Required only for internal (service-to-service) calls authenticated by internal client-credentials JWTs.
    /// </summary>
    public string? UserId { get; set; }
}

public sealed record DeployAppAcceptedResponse(
    string JobId,
    string Status,
    int EstimatedCompletionSeconds);

public sealed record DeploymentJobStatusResponse(
    string JobId,
    string AppId,
    string Status,
    string? RepoUrl,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public sealed record AppDeploymentHistoryResponse(
    string AppId,
    long TotalDeployments,
    IReadOnlyList<AppDeploymentHistoryItem> Deployments);

public sealed record AppDeploymentHistoryItem(
    string DeploymentId,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int DurationSeconds,
    string Trigger,
    string TriggeredBy,
    string? CommitHash,
    string? CommitMessage,
    string? RepoUrl,
    string? Error,
    IReadOnlyList<DeploymentLogEntry> Logs);

public sealed record DeploymentLogEntry(
    DateTime Timestamp,
    string Message);
