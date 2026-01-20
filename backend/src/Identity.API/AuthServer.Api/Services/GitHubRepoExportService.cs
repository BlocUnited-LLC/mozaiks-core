using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthServer.Api.DTOs;
using AuthServer.Api.Shared;
using Microsoft.Extensions.Options;

namespace AuthServer.Api.Services
{
    public sealed class GitHubRepoExportService : IGitHubRepoExportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly IOptions<GitHubSecretsOptions> _secretsOptions;
        private readonly IDatabaseProvisioningService _databaseService;
        private readonly ILogger<GitHubRepoExportService> _logger;

        public GitHubRepoExportService(
            IHttpClientFactory httpClientFactory,
            IOptions<GitHubSecretsOptions> secretsOptions,
            IDatabaseProvisioningService databaseService,
            ILogger<GitHubRepoExportService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("GitHub");
            _secretsOptions = secretsOptions;
            _databaseService = databaseService;
            _logger = logger;
        }

        public bool TryParseRepoFullName(string repoUrlOrFullName, out string repoFullName)
        {
            repoFullName = string.Empty;

            var raw = (repoUrlOrFullName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            raw = raw.Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase);

            if (raw.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 2)
                {
                    return false;
                }

                repoFullName = $"{segments[0]}/{segments[1]}";
                return true;
            }

            var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            repoFullName = $"{parts[0]}/{parts[1]}";
            return true;
        }

        public async Task<(string repoFullName, RepoManifestResponse manifest)> BuildManifestAsync(
            string repoUrlOrFullName,
            CancellationToken cancellationToken)
        {
            if (!TryParseRepoFullName(repoUrlOrFullName, out var repoFullName))
            {
                throw new InvalidOperationException("Invalid GitHub repoUrl; expected https://github.com/<owner>/<repo> or <owner>/<repo>");
            }

            var repo = await GetRepoAsync(repoFullName, cancellationToken);
            if (string.IsNullOrWhiteSpace(repo.DefaultBranch))
            {
                throw new InvalidOperationException("GitHub repo missing default_branch");
            }

            var baseCommitSha = await GetRefShaAsync(repoFullName, repo.DefaultBranch, cancellationToken);
            var treeSha = await GetCommitTreeShaAsync(repoFullName, baseCommitSha, cancellationToken);
            var tree = await GetTreeRecursiveAsync(repoFullName, treeSha, cancellationToken);

            var fileItems = tree.Tree
                .Where(i => string.Equals(i.Type, "blob", StringComparison.OrdinalIgnoreCase))
                .Where(i => !string.IsNullOrWhiteSpace(i.Path) && !string.IsNullOrWhiteSpace(i.Sha))
                .ToList();

            var files = new List<RepoManifestFileEntry>(fileItems.Count);
            foreach (var item in fileItems)
            {
                var bytes = await GetBlobBytesAsync(repoFullName, item.Sha!, cancellationToken);
                var sha256 = SHA256.HashData(bytes);
                files.Add(new RepoManifestFileEntry
                {
                    Path = item.Path!,
                    Sha256 = Convert.ToHexString(sha256).ToLowerInvariant(),
                    SizeBytes = bytes.LongLength
                });
            }

            var ordered = files
                .OrderBy(f => f.Path, StringComparer.Ordinal)
                .ToList();

            return (repoFullName, new RepoManifestResponse
            {
                BaseCommitSha = baseCommitSha,
                Files = ordered
            });
        }

        public async Task<(string repoFullName, string prUrl)> CreatePullRequestAsync(
            string repoUrlOrFullName,
            CreatePullRequestRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryParseRepoFullName(repoUrlOrFullName, out var repoFullName))
            {
                throw new InvalidOperationException("Invalid GitHub repoUrl; expected https://github.com/<owner>/<repo> or <owner>/<repo>");
            }

            var repo = await GetRepoAsync(repoFullName, cancellationToken);
            if (string.IsNullOrWhiteSpace(repo.DefaultBranch))
            {
                throw new InvalidOperationException("GitHub repo missing default_branch");
            }

            var baseCommitSha = (request.BaseCommitSha ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseCommitSha))
            {
                throw new InvalidOperationException("baseCommitSha is required");
            }

            var branchName = NormalizeBranchName(request.BranchName);
            if (string.IsNullOrWhiteSpace(branchName))
            {
                throw new InvalidOperationException("branchName is required");
            }

            await EnsureRefExistsAsync(repoFullName, branchName, baseCommitSha, cancellationToken);

            var baseTreeSha = await GetCommitTreeShaAsync(repoFullName, baseCommitSha, cancellationToken);

            var treeEntries = new List<GitHubTreeCreateEntry>();
            foreach (var change in request.Changes ?? new List<RepoFileChange>())
            {
                var path = NormalizeGitPath(change.Path);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var op = (change.Operation ?? string.Empty).Trim().ToLowerInvariant();
                if (op is "delete" or "remove")
                {
                    treeEntries.Add(GitHubTreeCreateEntry.Delete(path));
                    continue;
                }

                // add/modify
                var bytes = GetChangeBytes(change);
                if (bytes.Length == 0)
                {
                    throw new InvalidOperationException($"Change for '{path}' is missing content");
                }

                var blobSha = await CreateBlobAsync(repoFullName, Convert.ToBase64String(bytes), cancellationToken);
                treeEntries.Add(GitHubTreeCreateEntry.Blob(path, blobSha));
            }

            var treeSha = await CreateTreeAsync(repoFullName, baseTreeSha, treeEntries, cancellationToken);

            var commitMessage = string.IsNullOrWhiteSpace(request.Title) ? "MozaiksAI update" : request.Title.Trim();
            var commitSha = await CreateCommitAsync(repoFullName, commitMessage, treeSha, baseCommitSha, cancellationToken);

            await UpdateRefAsync(repoFullName, branchName, commitSha, cancellationToken);

            var prBody = BuildPullRequestBody(request);
            var pr = await CreatePullRequestAsync(repoFullName, request.Title, prBody, branchName, repo.DefaultBranch, cancellationToken);

            return (repoFullName, pr.HtmlUrl ?? string.Empty);
        }

        private static string BuildPullRequestBody(CreatePullRequestRequest request)
        {
            var sb = new StringBuilder();
            var body = (request.Body ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(body))
            {
                sb.AppendLine(body);
            }

            if (!string.IsNullOrWhiteSpace(request.PatchId) || !string.IsNullOrWhiteSpace(request.WorkflowType))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                if (!string.IsNullOrWhiteSpace(request.PatchId))
                {
                    sb.AppendLine($"patchId: {request.PatchId.Trim()}");
                }

                if (!string.IsNullOrWhiteSpace(request.WorkflowType))
                {
                    sb.AppendLine($"workflowType: {request.WorkflowType.Trim()}");
                }
            }

            if (request.Conflicts is { ValueKind: JsonValueKind.Array } conflicts && conflicts.GetArrayLength() > 0)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine("Conflicts (reported by MozaiksAI; not auto-resolved):");
                sb.AppendLine("```json");
                sb.AppendLine(conflicts.GetRawText());
                sb.AppendLine("```");
            }

            return sb.ToString().Trim();
        }

        private static byte[] GetChangeBytes(RepoFileChange change)
        {
            var base64 = (change.ContentBase64 ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(base64))
            {
                return Convert.FromBase64String(base64);
            }

            var text = change.Content ?? string.Empty;
            return Encoding.UTF8.GetBytes(text);
        }

        private async Task<GitHubRepoDetails> GetRepoAsync(string repoFullName, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync($"repos/{repoFullName}", cancellationToken);
            response.EnsureSuccessStatusCode();
            var repo = await response.Content.ReadFromJsonAsync<GitHubRepoDetails>(JsonOptions, cancellationToken);
            if (repo is null)
            {
                throw new InvalidOperationException("GitHub repo response missing");
            }

            return repo;
        }

        private async Task<string> GetRefShaAsync(string repoFullName, string branch, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync($"repos/{repoFullName}/git/ref/heads/{branch}", cancellationToken);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<GitHubRefResponse>(JsonOptions, cancellationToken);
            var sha = data?.Object?.Sha;
            if (string.IsNullOrWhiteSpace(sha))
            {
                throw new InvalidOperationException("GitHub ref response missing sha");
            }

            return sha;
        }

        private async Task<string> GetCommitTreeShaAsync(string repoFullName, string commitSha, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync($"repos/{repoFullName}/git/commits/{commitSha}", cancellationToken);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<GitHubCommitDetails>(JsonOptions, cancellationToken);
            var treeSha = data?.Tree?.Sha;
            if (string.IsNullOrWhiteSpace(treeSha))
            {
                throw new InvalidOperationException("GitHub commit response missing tree sha");
            }

            return treeSha;
        }

        private async Task<GitHubTreeResponse> GetTreeRecursiveAsync(string repoFullName, string treeSha, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync($"repos/{repoFullName}/git/trees/{treeSha}?recursive=1", cancellationToken);
            response.EnsureSuccessStatusCode();
            var tree = await response.Content.ReadFromJsonAsync<GitHubTreeResponse>(JsonOptions, cancellationToken);
            if (tree is null)
            {
                throw new InvalidOperationException("GitHub tree response missing");
            }

            return tree;
        }

        private async Task<byte[]> GetBlobBytesAsync(string repoFullName, string blobSha, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync($"repos/{repoFullName}/git/blobs/{blobSha}", cancellationToken);
            response.EnsureSuccessStatusCode();
            var blob = await response.Content.ReadFromJsonAsync<GitHubBlobDetails>(JsonOptions, cancellationToken);
            if (blob is null || string.IsNullOrWhiteSpace(blob.Content))
            {
                return Array.Empty<byte>();
            }

            if (!string.Equals(blob.Encoding, "base64", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("GitHub blob encoding unsupported");
            }

            var base64 = blob.Content.Replace("\n", string.Empty, StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal);
            return Convert.FromBase64String(base64);
        }

        private async Task<string> CreateBlobAsync(string repoFullName, string contentBase64, CancellationToken cancellationToken)
        {
            var payload = new
            {
                content = contentBase64,
                encoding = "base64"
            };

            var response = await _httpClient.PostAsJsonAsync($"repos/{repoFullName}/git/blobs", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<GitHubCreateBlobResponse>(JsonOptions, cancellationToken);
            var sha = data?.Sha;
            if (string.IsNullOrWhiteSpace(sha))
            {
                throw new InvalidOperationException("GitHub blob response missing sha");
            }

            return sha;
        }

        private async Task<string> CreateTreeAsync(string repoFullName, string baseTreeSha, List<GitHubTreeCreateEntry> entries, CancellationToken cancellationToken)
        {
            var payload = new
            {
                base_tree = baseTreeSha,
                tree = entries.Select(e => e.ToPayload()).ToList()
            };

            var response = await _httpClient.PostAsJsonAsync($"repos/{repoFullName}/git/trees", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<GitHubCreateTreeResponse>(JsonOptions, cancellationToken);
            var sha = data?.Sha;
            if (string.IsNullOrWhiteSpace(sha))
            {
                throw new InvalidOperationException("GitHub tree response missing sha");
            }

            return sha;
        }

        private async Task<string> CreateCommitAsync(string repoFullName, string message, string treeSha, string parentSha, CancellationToken cancellationToken)
        {
            var payload = new
            {
                message = string.IsNullOrWhiteSpace(message) ? "Update files" : message,
                tree = treeSha,
                parents = new[] { parentSha }
            };

            var response = await _httpClient.PostAsJsonAsync($"repos/{repoFullName}/git/commits", payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadFromJsonAsync<GitHubCreateCommitResponse>(JsonOptions, cancellationToken);
            var sha = data?.Sha;
            if (string.IsNullOrWhiteSpace(sha))
            {
                throw new InvalidOperationException("GitHub commit response missing sha");
            }

            return sha;
        }

        private async Task EnsureRefExistsAsync(string repoFullName, string branch, string sha, CancellationToken cancellationToken)
        {
            try
            {
                await UpdateRefAsync(repoFullName, branch, sha, cancellationToken);
            }
            catch (HttpRequestException)
            {
                var createRef = new
                {
                    @ref = $"refs/heads/{branch}",
                    sha
                };

                var resp = await _httpClient.PostAsJsonAsync($"repos/{repoFullName}/git/refs", createRef, cancellationToken);
                resp.EnsureSuccessStatusCode();
            }
        }

        private async Task UpdateRefAsync(string repoFullName, string branch, string sha, CancellationToken cancellationToken)
        {
            var payload = new
            {
                sha,
                force = true
            };

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"repos/{repoFullName}/git/refs/heads/{branch}")
            {
                Content = JsonContent.Create(payload)
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task<GitHubPullRequestResponse> CreatePullRequestAsync(
            string repoFullName,
            string? title,
            string? body,
            string branchName,
            string baseBranch,
            CancellationToken cancellationToken)
        {
            var payload = new
            {
                title = string.IsNullOrWhiteSpace(title) ? "MozaiksAI update" : title.Trim(),
                head = branchName,
                @base = baseBranch,
                body = string.IsNullOrWhiteSpace(body) ? null : body
            };

            var response = await _httpClient.PostAsJsonAsync($"repos/{repoFullName}/pulls", payload, cancellationToken);
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                var details = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("GitHub PR creation rejected: {Details}", details);
            }

            response.EnsureSuccessStatusCode();

            var pr = await response.Content.ReadFromJsonAsync<GitHubPullRequestResponse>(JsonOptions, cancellationToken);
            if (pr is null)
            {
                throw new InvalidOperationException("GitHub PR response missing");
            }

            return pr;
        }

        private static string NormalizeGitPath(string? path)
            => (path ?? string.Empty).Replace('\\', '/').TrimStart('/').Trim();

        private static string NormalizeBranchName(string? branch)
        {
            var raw = (branch ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            // Conservative sanitization for Git refs.
            raw = raw.Replace(' ', '-');
            raw = raw.Replace("..", "-", StringComparison.Ordinal);
            raw = raw.Replace("~", "-", StringComparison.Ordinal);
            raw = raw.Replace("^", "-", StringComparison.Ordinal);
            raw = raw.Replace(":", "-", StringComparison.Ordinal);
            raw = raw.Replace("\\", "/", StringComparison.Ordinal);
            raw = raw.Trim('/');
            return raw;
        }

        private sealed class GitHubRepoDetails
        {
            [JsonPropertyName("default_branch")]
            public string? DefaultBranch { get; init; }
        }

        private sealed class GitHubRefResponse
        {
            [JsonPropertyName("object")]
            public GitHubRefObject? Object { get; init; }
        }

        private sealed class GitHubRefObject
        {
            [JsonPropertyName("sha")]
            public string? Sha { get; init; }
        }

        private sealed class GitHubCommitDetails
        {
            [JsonPropertyName("tree")]
            public GitHubCommitTree? Tree { get; init; }
        }

        private sealed class GitHubCommitTree
        {
            [JsonPropertyName("sha")]
            public string? Sha { get; init; }
        }

        private sealed class GitHubTreeResponse
        {
            [JsonPropertyName("tree")]
            public List<GitHubTreeItem> Tree { get; init; } = new();
        }

        private sealed class GitHubTreeItem
        {
            [JsonPropertyName("path")]
            public string? Path { get; init; }

            [JsonPropertyName("type")]
            public string? Type { get; init; }

            [JsonPropertyName("sha")]
            public string? Sha { get; init; }
        }

        private sealed class GitHubBlobDetails
        {
            [JsonPropertyName("content")]
            public string? Content { get; init; }

            [JsonPropertyName("encoding")]
            public string? Encoding { get; init; }
        }

        private sealed class GitHubCreateBlobResponse
        {
            [JsonPropertyName("sha")]
            public string? Sha { get; init; }
        }

        private sealed class GitHubCreateTreeResponse
        {
            [JsonPropertyName("sha")]
            public string? Sha { get; init; }
        }

        private sealed class GitHubCreateCommitResponse
        {
            [JsonPropertyName("sha")]
            public string? Sha { get; init; }
        }

        private sealed class GitHubPullRequestResponse
        {
            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; init; }
        }

        private sealed class GitHubTreeCreateEntry
        {
            private GitHubTreeCreateEntry(string path, string type, string mode, string? sha)
            {
                Path = path;
                Type = type;
                Mode = mode;
                Sha = sha;
            }

            public string Path { get; }
            public string Type { get; }
            public string Mode { get; }
            public string? Sha { get; }

            public static GitHubTreeCreateEntry Blob(string path, string sha)
                => new(path, "blob", "100644", sha);

            public static GitHubTreeCreateEntry Delete(string path)
                => new(path, "blob", "100644", null);

            public object ToPayload()
            {
                return new
                {
                    path = Path,
                    mode = Mode,
                    type = Type,
                    sha = Sha
                };
            }
        }

        #region GitHub Secrets API

        /// <inheritdoc />
        public async Task<SetRepositorySecretsResponse> SetRepositorySecretsAsync(
            string appId,
            string repoFullName,
            SetRepositorySecretsRequest request,
            CancellationToken cancellationToken)
        {
            // Generate container app name from appId (e.g., "app-abc123")
            var containerAppName = $"app-{appId.ToLowerInvariant()[..Math.Min(appId.Length, 20)]}";
            
            // Fetch app-specific secrets
            string? databaseUri = null;
            string? appApiKey = null;
            
            // Get database connection string if requested and provisioned
            if (request.IncludeDatabaseUri)
            {
                try
                {
                    databaseUri = await _databaseService.GetConnectionStringAsync(appId, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(databaseUri))
                    {
                        _logger.LogDebug("Retrieved database URI for app {AppId}", appId);
                    }
                    else
                    {
                        _logger.LogWarning("Database not provisioned for app {AppId}, skipping DATABASE_URI secret", appId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get database URI for app {AppId}", appId);
                }
            }
            
            // App API key handling - must be passed via SecretOverrides since we only store hashes
            if (request.IncludeAppApiKey)
            {
                _logger.LogDebug("App API keys are hashed - pass via SecretOverrides['MOZAIKS_API_KEY'] if needed");
            }
            
            // Build secrets dictionary from configuration + app-specific values
            var secrets = _secretsOptions.Value.GetAllSecrets(containerAppName, databaseUri, appApiKey);
            
            // Merge in any overrides from the request
            if (request.SecretOverrides is not null)
            {
                foreach (var (key, value) in request.SecretOverrides)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        secrets[key] = value;
                        _logger.LogDebug("Added override secret: {SecretName}", key);
                    }
                }
            }
            
            _logger.LogInformation("Setting {Count} repository secrets for app {AppId} on {Repo} (hasDbUri={HasDb})",
                secrets.Count, appId, repoFullName, !string.IsNullOrWhiteSpace(databaseUri));
            
            var response = await SetRepositorySecretsAsync(repoFullName, secrets, cancellationToken);
            response.RepoFullName = repoFullName;
            return response;
        }

        /// <inheritdoc />
        public async Task<SetRepositorySecretsResponse> SetRepositorySecretsAsync(
            string repoFullName,
            Dictionary<string, string> secrets,
            CancellationToken cancellationToken)
        {
            var response = new SetRepositorySecretsResponse { RepoFullName = repoFullName };
            
            if (secrets.Count == 0)
            {
                response.Success = true;
                return response;
            }

            try
            {
                // Get repository public key for secret encryption
                var publicKey = await GetRepositoryPublicKeyAsync(repoFullName, cancellationToken);
                if (publicKey is null)
                {
                    response.Error = "Failed to get repository public key";
                    return response;
                }

                foreach (var (secretName, secretValue) in secrets)
                {
                    try
                    {
                        var encrypted = EncryptSecretValue(publicKey.Key, secretValue);
                        await SetSecretAsync(repoFullName, secretName, encrypted, publicKey.KeyId, cancellationToken);
                        response.SecretsSet.Add(secretName);
                        _logger.LogDebug("Set secret {SecretName} on {Repo}", secretName, repoFullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to set secret {SecretName} on {Repo}", secretName, repoFullName);
                        response.SecretsFailed.Add(secretName);
                    }
                }

                response.Success = response.SecretsFailed.Count == 0;
                if (!response.Success)
                {
                    response.Error = $"Failed to set {response.SecretsFailed.Count} secrets";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set repository secrets on {Repo}", repoFullName);
                response.Error = ex.Message;
            }

            return response;
        }

        private async Task<GitHubPublicKeyResponse?> GetRepositoryPublicKeyAsync(
            string repoFullName,
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(
                $"repos/{repoFullName}/actions/secrets/public-key",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get public key for {Repo}: {Status}", repoFullName, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GitHubPublicKeyResponse>(JsonOptions, cancellationToken);
        }

        private async Task SetSecretAsync(
            string repoFullName,
            string secretName,
            string encryptedValue,
            string keyId,
            CancellationToken cancellationToken)
        {
            var payload = new { encrypted_value = encryptedValue, key_id = keyId };
            var response = await _httpClient.PutAsJsonAsync(
                $"repos/{repoFullName}/actions/secrets/{secretName}",
                payload,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Encrypt a secret value using NaCl sealed box (libsodium).
        /// GitHub requires secrets to be encrypted with the repository's public key.
        /// </summary>
        private static string EncryptSecretValue(string publicKeyBase64, string secretValue)
        {
            var publicKeyBytes = Convert.FromBase64String(publicKeyBase64);
            var secretBytes = Encoding.UTF8.GetBytes(secretValue);
            
            // Use Sodium.Core SealedPublicKeyBox for proper NaCl sealed box encryption
            var encryptedBytes = Sodium.SealedPublicKeyBox.Create(secretBytes, publicKeyBytes);
            return Convert.ToBase64String(encryptedBytes);
        }

        #endregion

        #region Deployment Status API

        /// <inheritdoc />
        public async Task<DeploymentStatusResponse> GetDeploymentStatusAsync(
            string repoFullName,
            GetDeploymentStatusRequest request,
            CancellationToken cancellationToken)
        {
            var response = new DeploymentStatusResponse
            {
                AppId = request.AppId,
                RepoFullName = repoFullName
            };

            try
            {
                GitHubWorkflowRun? run;
                
                if (request.WorkflowRunId.HasValue)
                {
                    run = await GetWorkflowRunByIdAsync(repoFullName, request.WorkflowRunId.Value, cancellationToken);
                }
                else
                {
                    run = await GetLatestWorkflowRunAsync(repoFullName, request.WorkflowName, cancellationToken);
                }

                if (run is null)
                {
                    response.Error = "No workflow runs found";
                    return response;
                }

                response.Success = true;
                response.Status = run.Status;
                response.Conclusion = run.Conclusion;
                response.WorkflowRunId = run.Id;
                response.WorkflowRunUrl = run.HtmlUrl;
                response.StartedAt = run.CreatedAt;
                response.CompletedAt = run.UpdatedAt;

                // If completed successfully, try to get deployment URLs from artifacts
                if (run.Status == "completed" && run.Conclusion == "success")
                {
                    response.DeploymentUrls = await TryGetDeploymentUrlsAsync(repoFullName, run.Id, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get deployment status for {Repo}", repoFullName);
                response.Error = ex.Message;
            }

            return response;
        }

        private async Task<GitHubWorkflowRun?> GetWorkflowRunByIdAsync(
            string repoFullName,
            long runId,
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(
                $"repos/{repoFullName}/actions/runs/{runId}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GitHubWorkflowRun>(JsonOptions, cancellationToken);
        }

        private async Task<GitHubWorkflowRun?> GetLatestWorkflowRunAsync(
            string repoFullName,
            string workflowName,
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(
                $"repos/{repoFullName}/actions/runs?per_page=10",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var runs = await response.Content.ReadFromJsonAsync<GitHubWorkflowRunsResponse>(JsonOptions, cancellationToken);
            return runs?.WorkflowRuns
                .Where(r => string.IsNullOrEmpty(workflowName) || r.Name.Contains(workflowName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();
        }

        private async Task<DeploymentUrls?> TryGetDeploymentUrlsAsync(
            string repoFullName,
            long runId,
            CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"repos/{repoFullName}/actions/runs/{runId}/artifacts",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var artifacts = await response.Content.ReadFromJsonAsync<GitHubArtifactsResponse>(JsonOptions, cancellationToken);
                var deploymentArtifact = artifacts?.Artifacts
                    .FirstOrDefault(a => a.Name.Contains("deployment-url", StringComparison.OrdinalIgnoreCase));

                if (deploymentArtifact is null)
                {
                    return null;
                }

                // Download and extract artifact
                var artifactResponse = await _httpClient.GetAsync(deploymentArtifact.ArchiveDownloadUrl, cancellationToken);
                if (!artifactResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                var bytes = await artifactResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                var files = ZipBundleExtractor.ExtractFiles(bytes);

                var urlFile = files.FirstOrDefault(f => f.Key.EndsWith("deployment_url.txt", StringComparison.OrdinalIgnoreCase));
                if (urlFile.Value is null)
                {
                    return null;
                }

                var content = Encoding.UTF8.GetString(urlFile.Value);
                var urls = new DeploymentUrls();

                foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Contains("frontend", StringComparison.OrdinalIgnoreCase))
                    {
                        urls.FrontendUrl = ExtractUrl(trimmed);
                    }
                    else if (trimmed.Contains("backend", StringComparison.OrdinalIgnoreCase))
                    {
                        urls.BackendUrl = ExtractUrl(trimmed);
                    }
                    else if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
                    {
                        urls.CombinedUrl ??= trimmed;
                    }
                }

                return urls;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract deployment URLs from artifacts");
                return null;
            }
        }

        private static string? ExtractUrl(string line)
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                var urlPart = parts[1].Trim();
                if (Uri.TryCreate(urlPart, UriKind.Absolute, out _))
                {
                    return urlPart;
                }
            }
            return Uri.TryCreate(line, UriKind.Absolute, out _) ? line : null;
        }

        #endregion

        #region Repository File Download

        /// <inheritdoc />
        public async Task<IReadOnlyDictionary<string, byte[]>> DownloadRepositoryFilesAsync(
            string repoFullName,
            string? branch = null,
            string? subdirectory = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var refParam = string.IsNullOrWhiteSpace(branch) ? "" : $"?ref={branch}";
                var response = await _httpClient.GetAsync(
                    $"repos/{repoFullName}/zipball{refParam}",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to download repo {Repo}: {Status}", repoFullName, response.StatusCode);
                    return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var allFiles = ZipBundleExtractor.ExtractFiles(bytes);

                // Filter by subdirectory if specified
                if (string.IsNullOrWhiteSpace(subdirectory))
                {
                    return allFiles;
                }

                var normalizedSubdir = subdirectory.Trim('/').ToLowerInvariant();
                var filtered = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

                foreach (var (path, content) in allFiles)
                {
                    var normalizedPath = path.ToLowerInvariant();
                    
                    // Check if path starts with subdirectory (accounting for GitHub's root folder prefix)
                    var pathParts = normalizedPath.Split('/');
                    if (pathParts.Length > 1)
                    {
                        // Skip the first part (GitHub adds repo-branch prefix)
                        var relativePath = string.Join("/", pathParts.Skip(1));
                        if (relativePath.StartsWith(normalizedSubdir, StringComparison.OrdinalIgnoreCase))
                        {
                            // Remove subdirectory prefix from path
                            var newPath = relativePath.Substring(normalizedSubdir.Length).TrimStart('/');
                            if (!string.IsNullOrEmpty(newPath))
                            {
                                filtered[newPath] = content;
                            }
                        }
                    }
                }

                return filtered;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download files from {Repo}", repoFullName);
                return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            }
        }

        #endregion
    }
}
