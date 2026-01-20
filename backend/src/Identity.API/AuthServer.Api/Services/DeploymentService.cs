using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AuthServer.Api.Services;

public interface IDeploymentService
{
    Task<DeploymentJob> QueueDeploymentAsync(string appId, string userId, string appName, string correlationId, DeployAppRequest request, CancellationToken cancellationToken);
    Task<DeploymentJob?> GetJobAsync(string jobId, CancellationToken cancellationToken);
    Task ProcessJobAsync(string jobId, CancellationToken cancellationToken);
}

public sealed class DeploymentService : IDeploymentService
{
    private readonly IDeploymentJobRepository _jobs;
    private readonly MozaiksAppService _apps;
    private readonly IGitHubIntegrationService _github;
    private readonly IDatabaseProvisioningService _databaseProvisioning;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiKeyOptions _apiKeyOptions;
    private readonly GitHubOptions _gitHubOptions;
    private readonly DeploymentOptions _deploymentOptions;
    private readonly StructuredLogEmitter _logs;
    private readonly ILogger<DeploymentService> _logger;

    public DeploymentService(
        IDeploymentJobRepository jobs,
        MozaiksAppService apps,
        IGitHubIntegrationService github,
        IDatabaseProvisioningService databaseProvisioning,
        IHttpClientFactory httpClientFactory,
        IOptions<ApiKeyOptions> apiKeyOptions,
        IOptions<GitHubOptions> gitHubOptions,
        IOptions<DeploymentOptions> deploymentOptions,
        StructuredLogEmitter logs,
        ILogger<DeploymentService> logger)
    {
        _jobs = jobs;
        _apps = apps;
        _github = github;
        _databaseProvisioning = databaseProvisioning;
        _httpClientFactory = httpClientFactory;
        _apiKeyOptions = apiKeyOptions.Value;
        _gitHubOptions = gitHubOptions.Value;
        _deploymentOptions = deploymentOptions.Value;
        _logs = logs;
        _logger = logger;
    }

    public async Task<DeploymentJob> QueueDeploymentAsync(
        string appId,
        string userId,
        string appName,
        string correlationId,
        DeployAppRequest request,
        CancellationToken cancellationToken)
    {
        var bundleBytes = await FetchBundleAsync(request, cancellationToken);
        if (bundleBytes.Length == 0)
        {
            throw new InvalidOperationException("Bundle was empty");
        }

        if (_deploymentOptions.MaxBundleSizeBytes > 0 && bundleBytes.Length > _deploymentOptions.MaxBundleSizeBytes)
        {
            throw new InvalidOperationException("Bundle exceeds MaxBundleSizeBytes");
        }

        var job = new DeploymentJob
        {
            AppId = appId,
            UserId = userId,
            AppName = appName,
            RepoName = string.IsNullOrWhiteSpace(request.RepoName) ? appName : request.RepoName.Trim(),
            Branch = string.IsNullOrWhiteSpace(request.Branch) ? "main" : request.Branch.Trim(),
            CommitMessage = string.IsNullOrWhiteSpace(request.CommitMessage) ? "Initial app generation from Mozaiks" : request.CommitMessage.Trim(),
            BundleUrl = request.BundleUrl,
            Status = DeploymentStatus.Queued,
            CorrelationId = correlationId
        };

        var tempDir = ResolveBundleTempDirectory();
        Directory.CreateDirectory(tempDir);

        var bundlePath = Path.Combine(tempDir, $"{job.Id}.zip");
        await File.WriteAllBytesAsync(bundlePath, bundleBytes, cancellationToken);
        job.BundleStoragePath = bundlePath;

        await _jobs.CreateAsync(job, cancellationToken);
        return job;
    }

    public Task<DeploymentJob?> GetJobAsync(string jobId, CancellationToken cancellationToken)
        => _jobs.GetByIdAsync(jobId, cancellationToken);

    public async Task ProcessJobAsync(string jobId, CancellationToken cancellationToken)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        if (job.Status is DeploymentStatus.Completed or DeploymentStatus.Failed)
        {
            return;
        }

        if (job.Status == DeploymentStatus.Queued)
        {
            job.Status = DeploymentStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            await _jobs.UpdateAsync(job, cancellationToken);
        }

        var context = new StructuredLogContext
        {
            CorrelationId = job.CorrelationId,
            UserId = job.UserId,
            AppId = job.AppId
        };

        _logs.Info("Deploy.Job.Started", context, new { jobId = job.Id, repoName = job.RepoName, branch = job.Branch });

        try
        {
            var app = await _apps.GetByIdAsync(job.AppId);
            if (app is null)
            {
                throw new InvalidOperationException("App not found");
            }

            var bundleBytes = await File.ReadAllBytesAsync(job.BundleStoragePath, cancellationToken);
            var bundleFiles = ZipBundleExtractor.ExtractFiles(bundleBytes);

            var coreFiles = await TryDownloadMozaiksCoreFilesAsync(cancellationToken);

            var merged = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in coreFiles)
            {
                merged[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in bundleFiles)
            {
                merged[kvp.Key] = kvp.Value;
            }

            string? mongoDbUri = null;
            var schemaPath = bundleFiles.Keys.FirstOrDefault(p => p.EndsWith("schema.json", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(schemaPath) && bundleFiles.TryGetValue(schemaPath, out var schemaBytes))
            {
                _logs.Info("Deploy.Database.Schema.Found", context, new { jobId = job.Id, path = schemaPath });

                var schemaJson = System.Text.Encoding.UTF8.GetString(schemaBytes);
                string? seedJson = null;
                var seedPath = bundleFiles.Keys.FirstOrDefault(p => p.EndsWith("seed.json", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(seedPath) && bundleFiles.TryGetValue(seedPath, out var seedBytes))
                {
                    seedJson = System.Text.Encoding.UTF8.GetString(seedBytes);
                }

                var provision = await _databaseProvisioning.ProvisionDatabaseAsync(job.AppId, job.AppName, schemaJson, seedJson, cancellationToken);
                if (provision.Success)
                {
                    mongoDbUri = provision.ConnectionString ?? await _databaseProvisioning.GetConnectionStringAsync(job.AppId, cancellationToken);

                    if (!string.IsNullOrWhiteSpace(provision.DatabaseName))
                    {
                        if (string.IsNullOrWhiteSpace(app.DatabaseName) || app.DatabaseProvisionedAt is null)
                        {
                            var provisionedAt = app.DatabaseProvisionedAt ?? DateTime.UtcNow;
                            await _apps.SetDatabaseProvisioningAsync(job.AppId, provision.DatabaseName, provisionedAt);
                        }
                    }
                }
                else
                {
                    _logs.Warn("Deploy.Database.ProvisionFailed", context, new { jobId = job.Id, error = provision.ErrorMessage });
                }
            }

            var apiKey = await GetOrRotateApiKeyForDeploymentAsync(app.Id ?? job.AppId, cancellationToken);
            InjectMozaiksConfig(merged, job.AppId, apiKey, _deploymentOptions.PlatformApiUrl, mongoDbUri);

            var repoResult = await _github.CreateRepositoryAsync(new CreateRepoRequest
            {
                Name = job.RepoName,
                Description = $"Generated app: {job.AppName}",
                IsPrivate = true,
                OwnerId = job.UserId,
                AppId = job.AppId
            }, cancellationToken);

            if (!repoResult.Success)
            {
                throw new InvalidOperationException(repoResult.ErrorMessage ?? "Failed to create GitHub repo");
            }

            var pushOk = await _github.PushFilesAsync(
                repoResult.RepoFullName,
                merged.Select(kvp => new GitHubFile { Path = kvp.Key, Content = kvp.Value }),
                job.CommitMessage,
                job.Branch,
                cancellationToken);

            if (!pushOk)
            {
                throw new InvalidOperationException("Failed to push files to GitHub");
            }

            var deployedAt = DateTime.UtcNow;
            await _apps.SetGitHubDeploymentAsync(job.AppId, repoResult.RepoUrl, repoResult.RepoFullName, deployedAt);
            await _apps.SetStatusAsync(job.AppId, AppStatus.Running, deployedAt);

            job.Status = DeploymentStatus.Completed;
            job.RepoUrl = repoResult.RepoUrl;
            job.RepoFullName = repoResult.RepoFullName;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = null;

            await _jobs.UpdateAsync(job, cancellationToken);

            TryDeleteFile(job.BundleStoragePath);

            _logs.Info("Deploy.Job.Completed", context, new { jobId = job.Id, repoUrl = job.RepoUrl, repoFullName = job.RepoFullName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment failed for job {JobId}", job.Id);

            job.Status = DeploymentStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;

            try
            {
                await _apps.SetStatusAsync(job.AppId, AppStatus.Failed, DateTime.UtcNow);
            }
            catch (Exception statusEx)
            {
                _logger.LogWarning(statusEx, "Failed to update app status for {AppId}", job.AppId);
            }
            await _jobs.UpdateAsync(job, cancellationToken);

            _logs.Error("Deploy.Job.Failed", context, new { jobId = job.Id, error = ex.Message, type = ex.GetType().Name });
        }
    }

    private async Task<byte[]> FetchBundleAsync(DeployAppRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.BundleBase64))
        {
            return Convert.FromBase64String(request.BundleBase64);
        }

        if (!string.IsNullOrWhiteSpace(request.BundleUrl))
        {
            var client = _httpClientFactory.CreateClient();
            var bytes = await client.GetByteArrayAsync(request.BundleUrl, cancellationToken);
            return bytes;
        }

        throw new InvalidOperationException("Either bundleUrl or bundleBase64 is required");
    }

    private string ResolveBundleTempDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_deploymentOptions.BundleTempPath))
        {
            return _deploymentOptions.BundleTempPath;
        }

        return Path.Combine(Path.GetTempPath(), "mozaiks-bundles");
    }

    private async Task<IReadOnlyDictionary<string, byte[]>> TryDownloadMozaiksCoreFilesAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_gitHubOptions.MozaiksCoreRepoUrl))
        {
            return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var (owner, repo) = ParseGitHubOwnerRepo(_gitHubOptions.MozaiksCoreRepoUrl);
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            {
                return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            }

            var client = _httpClientFactory.CreateClient("GitHub");
            var response = await client.GetAsync($"repos/{owner}/{repo}/zipball", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ZipBundleExtractor.ExtractFiles(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download MozaiksCore");
            return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<string> GetOrRotateApiKeyForDeploymentAsync(string appId, CancellationToken cancellationToken)
    {
        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            throw new InvalidOperationException("App not found");
        }

        var now = DateTime.UtcNow;
        var material = ApiKeyCrypto.Generate(_apiKeyOptions);

        if (string.IsNullOrWhiteSpace(app.ApiKeyHash))
        {
            var created = await _apps.TryGenerateApiKeyAsync(appId, material.Hash, material.Prefix, now);
            if (!created)
            {
                var refreshed = await _apps.GetByIdAsync(appId);
                if (refreshed is null || string.IsNullOrWhiteSpace(refreshed.ApiKeyHash))
                {
                    throw new InvalidOperationException("Failed to generate API key");
                }
            }

            return material.ApiKey;
        }

        var regenerated = await _apps.RegenerateApiKeyAsync(appId, material.Hash, material.Prefix, now);
        if (!regenerated)
        {
            throw new InvalidOperationException("Failed to regenerate API key");
        }

        return material.ApiKey;
    }

    private static void InjectMozaiksConfig(Dictionary<string, byte[]> files, string appId, string apiKey, string platformApiUrl, string? mongoDbUri)
    {
        var platform = (platformApiUrl ?? string.Empty).TrimEnd('/');
        var envContent =
$@"# Mozaiks Configuration (auto-generated)
# DO NOT COMMIT THIS FILE TO PUBLIC REPOS

ENV=production
MOZAIKS_MANAGED=true
MOZAIKS_APP_ID={appId}
MOZAIKS_API_KEY={apiKey}

INSIGHTS_PUSH_ENABLED=1
INSIGHTS_API_BASE_URL={platform}

MOZAIKS_PLATFORM_JWKS_URL={platform}/api/auth/.well-known/jwks.json
";

        if (!string.IsNullOrWhiteSpace(mongoDbUri))
        {
            envContent +=
$@"

# Database (auto-provisioned)
MONGODB_URI={mongoDbUri}
";
        }

        files[".env.mozaiks"] = System.Text.Encoding.UTF8.GetBytes(envContent);

        var gitignore = files.TryGetValue(".gitignore", out var existing) ? System.Text.Encoding.UTF8.GetString(existing) : string.Empty;
        if (!gitignore.Contains(".env.mozaiks", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = gitignore.EndsWith('\n') || gitignore.Length == 0 ? string.Empty : "\n";
            gitignore += $"{suffix}\n# Mozaiks secrets\n.env.mozaiks\n";
            files[".gitignore"] = System.Text.Encoding.UTF8.GetBytes(gitignore);
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static (string owner, string repo) ParseGitHubOwnerRepo(string repoUrl)
    {
        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            return (string.Empty, string.Empty);
        }

        var raw = repoUrl.Trim();

        if (raw.Contains("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(raw.Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase));
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length >= 2 ? (segments[0], segments[1]) : (string.Empty, string.Empty);
        }

        var parts = raw.Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? (parts[0], parts[1]) : (string.Empty, string.Empty);
    }
}
