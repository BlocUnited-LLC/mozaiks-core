using AuthServer.Api.DTOs;

namespace AuthServer.Api.Services
{
    public interface IGitHubRepoExportService
    {
        Task<(string repoFullName, RepoManifestResponse manifest)> BuildManifestAsync(
            string repoUrlOrFullName,
            CancellationToken cancellationToken);

        Task<(string repoFullName, string prUrl)> CreatePullRequestAsync(
            string repoUrlOrFullName,
            CreatePullRequestRequest request,
            CancellationToken cancellationToken);

        bool TryParseRepoFullName(string repoUrlOrFullName, out string repoFullName);
        
        /// <summary>
        /// Set GitHub repository secrets required for deployment workflows.
        /// Uses configured secrets from GitHubSecretsOptions plus app-specific secrets
        /// (database URI, API key) fetched from the app record.
        /// </summary>
        Task<SetRepositorySecretsResponse> SetRepositorySecretsAsync(
            string appId,
            string repoFullName,
            SetRepositorySecretsRequest request,
            CancellationToken cancellationToken);
        
        /// <summary>
        /// Set GitHub repository secrets with explicit secrets dictionary.
        /// Uses NaCl sealed box encryption as required by GitHub API.
        /// </summary>
        Task<SetRepositorySecretsResponse> SetRepositorySecretsAsync(
            string repoFullName,
            Dictionary<string, string> secrets,
            CancellationToken cancellationToken);
        
        /// <summary>
        /// Get the status of the latest deployment workflow run.
        /// </summary>
        Task<DeploymentStatusResponse> GetDeploymentStatusAsync(
            string repoFullName,
            GetDeploymentStatusRequest request,
            CancellationToken cancellationToken);
        
        /// <summary>
        /// Download files from a GitHub repository.
        /// </summary>
        Task<IReadOnlyDictionary<string, byte[]>> DownloadRepositoryFilesAsync(
            string repoFullName,
            string? branch = null,
            string? subdirectory = null,
            CancellationToken cancellationToken = default);
    }
}

