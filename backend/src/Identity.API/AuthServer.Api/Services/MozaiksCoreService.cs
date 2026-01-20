using AuthServer.Api.Shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AuthServer.Api.Services;

/// <summary>
/// Interface for MozaiksCore SDK file management.
/// </summary>
public interface IMozaiksCoreService
{
    /// <summary>
    /// Get MozaiksCore SDK files (cached).
    /// Returns empty dictionary if MozaiksCore is not configured.
    /// </summary>
    Task<IReadOnlyDictionary<string, byte[]>> GetMozaiksCoreFilesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Force refresh MozaiksCore files from GitHub.
    /// </summary>
    Task RefreshCacheAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if MozaiksCore is configured.
    /// </summary>
    bool IsConfigured { get; }
}

/// <summary>
/// Service for downloading and caching MozaiksCore SDK files.
/// MozaiksCore is the base app foundation that every Mozaiks-generated app runs on.
/// </summary>
public sealed class MozaiksCoreService : IMozaiksCoreService
{
    private const string CacheKey = "mozaikscore_files";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    
    private readonly IGitHubRepoExportService _gitHubService;
    private readonly GitHubOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MozaiksCoreService> _logger;
    private readonly SemaphoreSlim _downloadLock = new(1, 1);

    public MozaiksCoreService(
        IGitHubRepoExportService gitHubService,
        IOptions<GitHubOptions> options,
        IMemoryCache cache,
        ILogger<MozaiksCoreService> logger)
    {
        _gitHubService = gitHubService;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.MozaiksCoreRepoUrl);

    public async Task<IReadOnlyDictionary<string, byte[]>> GetMozaiksCoreFilesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogDebug("MozaiksCore not configured, returning empty files");
            return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        }

        // Try to get from cache first
        if (_cache.TryGetValue<IReadOnlyDictionary<string, byte[]>>(CacheKey, out var cachedFiles) && cachedFiles is not null)
        {
            _logger.LogDebug("MozaiksCore files retrieved from cache ({Count} files)", cachedFiles.Count);
            return cachedFiles;
        }

        // Download with lock to prevent multiple simultaneous downloads
        await _downloadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring lock
            if (_cache.TryGetValue<IReadOnlyDictionary<string, byte[]>>(CacheKey, out cachedFiles) && cachedFiles is not null)
            {
                return cachedFiles;
            }

            var files = await DownloadMozaiksCoreFilesAsync(cancellationToken);
            
            // Cache the files
            _cache.Set(CacheKey, files, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = files.Values.Sum(f => f.LongLength) // Track memory size
            });

            return files;
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return;
        }

        await _downloadLock.WaitAsync(cancellationToken);
        try
        {
            _cache.Remove(CacheKey);
            var files = await DownloadMozaiksCoreFilesAsync(cancellationToken);
            
            _cache.Set(CacheKey, files, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = files.Values.Sum(f => f.LongLength)
            });

            _logger.LogInformation("MozaiksCore cache refreshed ({Count} files)", files.Count);
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    private async Task<IReadOnlyDictionary<string, byte[]>> DownloadMozaiksCoreFilesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Parse repo URL to get owner/repo
            if (!_gitHubService.TryParseRepoFullName(_options.MozaiksCoreRepoUrl, out var repoFullName))
            {
                _logger.LogWarning("Invalid MozaiksCore repo URL: {Url}", _options.MozaiksCoreRepoUrl);
                return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            }

            _logger.LogInformation("Downloading MozaiksCore files from {Repo} (branch: {Branch}, subdir: {Subdir})",
                repoFullName, 
                _options.MozaiksCoreBranch,
                _options.MozaiksCoreSubdirectory ?? "<root>");

            var files = await _gitHubService.DownloadRepositoryFilesAsync(
                repoFullName,
                _options.MozaiksCoreBranch,
                _options.MozaiksCoreSubdirectory,
                cancellationToken);

            _logger.LogInformation("Downloaded {Count} MozaiksCore files", files.Count);
            
            // Log file paths for debugging
            foreach (var path in files.Keys.Take(10))
            {
                _logger.LogDebug("MozaiksCore file: {Path}", path);
            }
            if (files.Count > 10)
            {
                _logger.LogDebug("... and {More} more files", files.Count - 10);
            }

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download MozaiksCore files from {Url}", _options.MozaiksCoreRepoUrl);
            return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
