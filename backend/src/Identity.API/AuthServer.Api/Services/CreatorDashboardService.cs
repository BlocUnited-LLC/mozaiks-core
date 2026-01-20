using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Shared;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AuthServer.Api.Services;

public interface ICreatorDashboardService
{
    Task<CreatorDashboardResponse> GetDashboardAsync(string userId, string? username, CancellationToken cancellationToken);
}

public sealed class CreatorDashboardService : ICreatorDashboardService
{
    private static readonly string[] TotalUsersMetricFallbacks = ["users_total", "users", "total_users"];
    private static readonly string[] ActiveUsers24hMetricFallbacks = ["active_users_24h", "active_users", "users_active_24h"];

    private readonly MozaiksAppService _apps;
    private readonly IMongoDatabase _database;
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CreatorDashboardService> _logger;

    public CreatorDashboardService(
        MozaiksAppService apps,
        IMongoDatabase database,
        IWebHostEnvironment env,
        IMemoryCache cache,
        ILogger<CreatorDashboardService> logger)
    {
        _apps = apps;
        _database = database;
        _env = env;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CreatorDashboardResponse> GetDashboardAsync(string userId, string? username, CancellationToken cancellationToken)
    {
        var cacheKey = $"creator_dashboard:{userId}";
        if (_cache.TryGetValue(cacheKey, out CreatorDashboardResponse? cached) && cached is not null)
        {
            return cached;
        }

        var owned = await _apps.GetByOwnerUserIdAsync(userId) ?? new List<MozaiksAppModel>();
        var ownedIds = owned
            .Select(a => (a.Id ?? string.Empty).Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var response = new CreatorDashboardResponse
        {
            UserId = userId,
            Username = username
        };

        if (ownedIds.Count == 0)
        {
            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(2));
            return response;
        }

        var resolvedEnv = _env.IsDevelopment() ? "development" : "production";
        var nowUtc = DateTime.UtcNow;
        var since24hUtc = nowUtc.AddHours(-24);
        var sinceKpiUtc = nowUtc.AddDays(-30);

        var eventsTask = CountEventsByAppIdAsync(ownedIds, resolvedEnv, since24hUtc, cancellationToken);
        var errorsTask = CountErrorsByAppIdAsync(ownedIds, resolvedEnv, since24hUtc, cancellationToken);
        var kpisTask = GetLatestKpisByAppIdAsync(
            ownedIds,
            resolvedEnv,
            TotalUsersMetricFallbacks.Concat(ActiveUsers24hMetricFallbacks).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            sinceKpiUtc,
            cancellationToken);
        var sdkTask = GetSdkConnectedByAppIdAsync(ownedIds, cancellationToken);
        var deploymentsTask = GetRecentDeploymentJobsAsync(ownedIds, cancellationToken);

        await Task.WhenAll(eventsTask, errorsTask, kpisTask, sdkTask, deploymentsTask);

        var eventsByAppId = eventsTask.Result;
        var errorsByAppId = errorsTask.Result;
        var latestKpis = kpisTask.Result;
        var sdkByAppId = sdkTask.Result;
        var recentDeployments = deploymentsTask.Result;

        var latestDeploymentByAppId = recentDeployments
            .GroupBy(j => j.AppId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);

        foreach (var app in owned.OrderByDescending(a => a.CreatedAt))
        {
            var appId = (app.Id ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(appId))
            {
                continue;
            }

            var events24h = eventsByAppId.TryGetValue(appId, out var e) ? e : 0;
            var errors24h = errorsByAppId.TryGetValue(appId, out var err) ? err : 0;

            var users = ResolveKpiValue(appId, latestKpis, TotalUsersMetricFallbacks);
            var activeUsers24h = ResolveKpiValue(appId, latestKpis, ActiveUsers24hMetricFallbacks);

            var errorRate = events24h > 0 ? (double)errors24h / events24h : 0d;

            var status = ResolveStatus(app);

            var dbProvisioned = !string.IsNullOrWhiteSpace(app.DatabaseName);

            var sdkConnected = (events24h > 0) || (sdkByAppId.TryGetValue(appId, out var sdk) && sdk);

            response.Apps.Add(new CreatorDashboardAppItem
            {
                AppId = appId,
                Name = app.Name,
                Status = status,
                CreatedAt = app.CreatedAt,
                LastDeployedAt = app.DeployedAt,
                Stats = new CreatorDashboardAppStats
                {
                    Events24h = events24h,
                    Users = users,
                    ActiveUsers24h = activeUsers24h,
                    Errors24h = errors24h,
                    ErrorRate = errorRate
                },
                Database = new CreatorDashboardDatabaseStatus
                {
                    Provisioned = dbProvisioned,
                    Name = dbProvisioned ? app.DatabaseName : null
                },
                SdkConnected = sdkConnected
            });

            if (!response.Summary.AppsByStatus.TryAdd(status, 1))
            {
                response.Summary.AppsByStatus[status] += 1;
            }
        }

        response.Summary.TotalApps = response.Apps.Count;
        response.Summary.TotalEvents24h = response.Apps.Sum(a => a.Stats.Events24h);
        response.Summary.TotalUsers = response.Apps.Sum(a => a.Stats.Users);
        response.Summary.ActiveUsers24h = response.Apps.Sum(a => a.Stats.ActiveUsers24h);
        response.Summary.TotalErrors24h = response.Apps.Sum(a => a.Stats.Errors24h);
        response.Summary.ErrorRate = response.Summary.TotalEvents24h > 0
            ? (double)response.Summary.TotalErrors24h / response.Summary.TotalEvents24h
            : 0d;

        EnsureStatusKeys(response.Summary.AppsByStatus, ["running", "paused", "stopped", "failed"]);

        response.RecentActivity = recentDeployments
            .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
            .Take(10)
            .Select(j =>
            {
                var appName = owned.FirstOrDefault(a => string.Equals(a.Id, j.AppId, StringComparison.OrdinalIgnoreCase))?.Name ?? j.AppName;
                var ts = j.CompletedAt ?? j.CreatedAt;
                var isFailure = j.Status == DeploymentStatus.Failed;
                return new CreatorDashboardActivityItem
                {
                    Type = isFailure ? "deployment_failed" : "deployment",
                    AppId = j.AppId,
                    AppName = appName,
                    Timestamp = ts,
                    Message = isFailure ? $"Deployment failed: {j.ErrorMessage}" : "Deployed to production"
                };
            })
            .ToList();

        foreach (var (appId, latest) in latestDeploymentByAppId)
        {
            if (latest.Status != DeploymentStatus.Failed)
            {
                continue;
            }

            var appName = owned.FirstOrDefault(a => string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase))?.Name ?? latest.AppName;
            response.Alerts.Add(new CreatorDashboardAlertItem
            {
                Severity = "warning",
                AppId = appId,
                AppName = appName,
                Message = "App deployment failed - check logs",
                Timestamp = latest.CompletedAt ?? latest.CreatedAt
            });
        }

        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(2));

        return response;
    }

    private async Task<Dictionary<string, int>> CountEventsByAppIdAsync(
        IReadOnlyCollection<string> appIds,
        string env,
        DateTime sinceUtc,
        CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<BsonDocument>("InsightsEvents");

        var filter = Builders<BsonDocument>.Filter.In("appId", appIds)
                     & Builders<BsonDocument>.Filter.Eq("env", env)
                     & Builders<BsonDocument>.Filter.Gte("t", sinceUtc);

        var results = await collection.Aggregate()
            .Match(filter)
            .Group(new BsonDocument
            {
                { "_id", "$appId" },
                { "count", new BsonDocument("$sum", 1) }
            })
            .ToListAsync(cancellationToken);

        return results
            .Where(r => r.Contains("_id") && r.Contains("count"))
            .ToDictionary(
                r => r["_id"].AsString,
                r => r["count"].ToInt32(),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, int>> CountErrorsByAppIdAsync(
        IReadOnlyCollection<string> appIds,
        string env,
        DateTime sinceUtc,
        CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<BsonDocument>("InsightsEvents");

        var baseFilter = Builders<BsonDocument>.Filter.In("appId", appIds)
                         & Builders<BsonDocument>.Filter.Eq("env", env)
                         & Builders<BsonDocument>.Filter.Gte("t", sinceUtc);

        var typeError = Builders<BsonDocument>.Filter.Eq("type", "error");
        var severityError = Builders<BsonDocument>.Filter.In("severity", new[] { "error", "critical", "fatal" });

        var filter = baseFilter & (typeError | severityError);

        var results = await collection.Aggregate()
            .Match(filter)
            .Group(new BsonDocument
            {
                { "_id", "$appId" },
                { "count", new BsonDocument("$sum", 1) }
            })
            .ToListAsync(cancellationToken);

        return results
            .Where(r => r.Contains("_id") && r.Contains("count"))
            .ToDictionary(
                r => r["_id"].AsString,
                r => r["count"].ToInt32(),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<(string AppId, string Metric), double>> GetLatestKpisByAppIdAsync(
        IReadOnlyCollection<string> appIds,
        string env,
        string[] metrics,
        DateTime sinceUtc,
        CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<BsonDocument>("InsightsKpiPoints");

        var filter = Builders<BsonDocument>.Filter.In("appId", appIds)
                     & Builders<BsonDocument>.Filter.Eq("env", env)
                     & Builders<BsonDocument>.Filter.In("metric", metrics)
                     & Builders<BsonDocument>.Filter.Gte("t", sinceUtc);

        var results = await collection.Aggregate()
            .Match(filter)
            .Sort(new BsonDocument("t", -1))
            .Group(new BsonDocument
            {
                {
                    "_id",
                    new BsonDocument
                    {
                        { "appId", "$appId" },
                        { "metric", "$metric" }
                    }
                },
                { "v", new BsonDocument("$first", "$v") }
            })
            .ToListAsync(cancellationToken);

        var dict = new Dictionary<(string AppId, string Metric), double>();

        foreach (var doc in results)
        {
            if (!doc.Contains("_id") || !doc.Contains("v"))
            {
                continue;
            }

            var id = doc["_id"].AsBsonDocument;
            if (!id.TryGetValue("appId", out var appIdVal) || !id.TryGetValue("metric", out var metricVal))
            {
                continue;
            }

            var appId = appIdVal.AsString;
            var metric = metricVal.AsString;
            var value = doc["v"].ToDouble();

            dict[(appId, metric)] = value;
        }

        return dict;
    }

    private async Task<Dictionary<string, bool>> GetSdkConnectedByAppIdAsync(
        IReadOnlyCollection<string> appIds,
        CancellationToken cancellationToken)
    {
        var parsedIds = appIds
            .Select(id => ObjectId.TryParse(id, out var oid) ? oid : (ObjectId?)null)
            .Where(oid => oid.HasValue)
            .Select(oid => oid!.Value)
            .ToList();

        if (parsedIds.Count == 0)
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        var collection = _database.GetCollection<BsonDocument>("ApiKeyUsageRecords");

        var filter = Builders<BsonDocument>.Filter.In("appId", parsedIds);
        var docs = await collection.Find(filter).Project(new BsonDocument
        {
            { "appId", 1 },
            { "lastPingAt", 1 },
            { "lastKpiPushAt", 1 },
            { "lastEventPushAt", 1 }
        }).ToListAsync(cancellationToken);

        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in docs)
        {
            if (!doc.TryGetValue("appId", out var appIdValue))
            {
                continue;
            }

            var appId = appIdValue.IsObjectId
                ? appIdValue.AsObjectId.ToString()
                : appIdValue.IsString
                    ? appIdValue.AsString
                    : appIdValue.ToString();

            if (string.IsNullOrWhiteSpace(appId))
            {
                continue;
            }
            var connected =
                doc.TryGetValue("lastPingAt", out var lp) && !lp.IsBsonNull
                || doc.TryGetValue("lastKpiPushAt", out var lk) && !lk.IsBsonNull
                || doc.TryGetValue("lastEventPushAt", out var le) && !le.IsBsonNull;

            result[appId] = connected;
        }

        return result;
    }

    private async Task<IReadOnlyList<DeploymentJob>> GetRecentDeploymentJobsAsync(
        IReadOnlyCollection<string> appIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var collection = _database.GetCollection<DeploymentJob>(MongoCollectionNames.DeploymentJobs);
            var filter = Builders<DeploymentJob>.Filter.In(x => x.AppId, appIds);
            var limit = Math.Min(500, Math.Max(50, appIds.Count * 10));

            return await collection.Find(filter)
                .SortByDescending(x => x.CreatedAt)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load recent deployments for creator dashboard");
            return Array.Empty<DeploymentJob>();
        }
    }

    private static int ResolveKpiValue(string appId, Dictionary<(string AppId, string Metric), double> latest, string[] metricFallbacks)
    {
        foreach (var metric in metricFallbacks)
        {
            if (latest.TryGetValue((appId, metric), out var value))
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    return 0;
                }

                return (int)Math.Max(0, Math.Round(value));
            }
        }

        return 0;
    }

    private static string ResolveStatus(MozaiksAppModel app)
    {
        var status = app.Status;
        if (status == AppStatus.Draft && app.DeployedAt is not null)
        {
            status = AppStatus.Running;
        }

        return status.ToString().ToLowerInvariant();
    }

    private static void EnsureStatusKeys(Dictionary<string, int> appsByStatus, string[] keys)
    {
        foreach (var key in keys)
        {
            appsByStatus.TryAdd(key, 0);
        }
    }
}
