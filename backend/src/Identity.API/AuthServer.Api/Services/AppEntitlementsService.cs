using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;

namespace AuthServer.Api.Services;

public sealed record AppEntitlements(
    bool AllowHosting,
    bool AllowExportRepo,
    bool AllowWorkerMode,
    string? HostingLevel,
    string? AppPlanId,
    string? PlatformPlanId,
    int? DomainsMax,
    bool EmailEnabled,
    int? EmailMaxDomains,
    int? TokensMonthlyLimit,
    int? TransactionFeeBps,
    List<string> InstalledPlugins);

public sealed record AppEntitlementsSnapshot(
    AppEntitlements Entitlements,
    MozaiksPayClient.MozaiksPayStatusSnapshot PlatformStatus,
    MozaiksPayClient.MozaiksPayStatusSnapshot AppStatus);

public sealed class AppEntitlementsService
{
    private readonly MozaiksPayClient _pay;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly IAppMonetizationSpecRepository _monetizationSpecs;
    private readonly IMozaiksAppRepository _apps;
    private readonly ILogger<AppEntitlementsService> _logger;

    public AppEntitlementsService(
        MozaiksPayClient pay,
        ISubscriptionPlanRepository plans,
        IAppMonetizationSpecRepository monetizationSpecs,
        IMozaiksAppRepository apps,
        ILogger<AppEntitlementsService> logger)
    {
        _pay = pay;
        _plans = plans;
        _monetizationSpecs = monetizationSpecs;
        _apps = apps;
        _logger = logger;
    }

    public async Task<AppEntitlements> GetForUserAndAppAsync(
        string userId,
        string appId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(userId, appId, correlationId, cancellationToken);
        return snapshot.Entitlements;
    }

    public async Task<AppEntitlementsSnapshot> GetSnapshotAsync(
        string userId,
        string appId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("userId is required", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentException("appId is required", nameof(appId));
        }

        var (platformStatus, appStatus) = await GetStatusAsync(userId, appId, correlationId, cancellationToken);
        var entitlements = await BuildEntitlementsAsync(appId, platformStatus, appStatus, cancellationToken);
        return new AppEntitlementsSnapshot(entitlements, platformStatus, appStatus);
    }

    private async Task<(MozaiksPayClient.MozaiksPayStatusSnapshot Platform, MozaiksPayClient.MozaiksPayStatusSnapshot App)> GetStatusAsync(
        string userId,
        string appId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        MozaiksPayClient.MozaiksPayStatusSnapshot platformStatus;
        MozaiksPayClient.MozaiksPayStatusSnapshot appStatus;

        try
        {
            platformStatus = await _pay.GetSubscriptionStatusAsync(userId, "platform", null, correlationId, cancellationToken);
            appStatus = await _pay.GetSubscriptionStatusAsync(userId, "app", appId, correlationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate entitlements from Payment.API userId={UserId} appId={AppId}", userId, appId);
            throw;
        }

        return (platformStatus, appStatus);
    }

    private async Task<AppEntitlements> BuildEntitlementsAsync(
        string appId,
        MozaiksPayClient.MozaiksPayStatusSnapshot platformStatus,
        MozaiksPayClient.MozaiksPayStatusSnapshot appStatus,
        CancellationToken cancellationToken)
    {
        var allowHosting = appStatus.IsActive;
        var allowExportRepo = platformStatus.IsActive || allowHosting;

        var hostingLevel = default(string);
        var allowWorkerMode = false;
        var domainsMax = default(int?);
        var emailEnabled = false;
        var emailMaxDomains = default(int?);
        var tokensMonthlyLimit = default(int?);
        var transactionFeeBps = default(int?);

        SubscriptionPlanModel? appPlan = null;
        AppMonetizationPlanSpec? monetizationPlan = null;

        var appPlanId = string.IsNullOrWhiteSpace(appStatus.PlanId) ? null : appStatus.PlanId.Trim();
        if (allowHosting && !string.IsNullOrWhiteSpace(appPlanId))
        {
            monetizationPlan = await TryResolveMonetizationPlanAsync(appId, appPlanId, cancellationToken);
            if (monetizationPlan != null)
            {
                hostingLevel = (monetizationPlan.Entitlements?.HostingTier ?? string.Empty).Trim();
                allowWorkerMode = IsScaleOrEnterprise(hostingLevel);
                domainsMax = monetizationPlan.Entitlements?.DomainsMax;
                emailEnabled = monetizationPlan.Entitlements?.EmailEnabled ?? false;
                emailMaxDomains = monetizationPlan.Entitlements?.EmailMaxDomains;
                tokensMonthlyLimit = monetizationPlan.Entitlements?.MonthlyTokens;
                transactionFeeBps = monetizationPlan.Entitlements?.TransactionFeeBps;
            }
            else
            {
                appPlan = await TryResolvePlanAsync(appPlanId, cancellationToken);
                hostingLevel = (appPlan?.HostingLevel ?? string.Empty).Trim();
                allowWorkerMode = IsScaleOrEnterprise(hostingLevel);
                domainsMax = appPlan?.MaxDomains;
                emailEnabled = appPlan?.EmailEnabled ?? false;
                emailMaxDomains = appPlan?.MaxEmailDomains;
                tokensMonthlyLimit = appPlan?.MonthlyTokens;
            }
        }

        SubscriptionPlanModel? platformPlan = null;
        if (!string.IsNullOrWhiteSpace(platformStatus.PlanId))
        {
            platformPlan = await TryResolvePlanAsync(platformStatus.PlanId, cancellationToken);
        }

        tokensMonthlyLimit ??= platformPlan?.MonthlyTokens ?? appPlan?.MonthlyTokens;

        // Fetch App Model for Plugins
        var app = await _apps.GetByIdAsync(appId);
        var plugins = app?.InstalledPlugins ?? new List<string>();

        return new AppEntitlements(
            AllowHosting: allowHosting,
            AllowExportRepo: allowExportRepo,
            AllowWorkerMode: allowWorkerMode,
            HostingLevel: hostingLevel,
            AppPlanId: appStatus.PlanId,
            PlatformPlanId: platformStatus.PlanId,
            DomainsMax: domainsMax,
            EmailEnabled: emailEnabled,
            EmailMaxDomains: emailMaxDomains,
            TokensMonthlyLimit: tokensMonthlyLimit,
            TransactionFeeBps: transactionFeeBps,
            InstalledPlugins: plugins);
    }

    private async Task<SubscriptionPlanModel?> TryResolvePlanAsync(string planId, CancellationToken cancellationToken)
    {
        try
        {
            return await _plans.GetPlanByIdAsync(planId);
        }
        catch
        {
            return null;
        }
    }

    private async Task<AppMonetizationPlanSpec?> TryResolveMonetizationPlanAsync(string appId, string planId, CancellationToken cancellationToken)
    {
        try
        {
            var spec = await _monetizationSpecs.GetLatestCommittedAsync(appId, cancellationToken);
            return spec?.Spec?.Plans?
                .FirstOrDefault(p => string.Equals(p.PlanId, planId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve monetization spec for appId={AppId} planId={PlanId}", appId, planId);
            return null;
        }
    }

    private static bool IsScaleOrEnterprise(string? hostingLevel)
        => string.Equals(hostingLevel, "scale", StringComparison.OrdinalIgnoreCase)
           || string.Equals(hostingLevel, "enterprise", StringComparison.OrdinalIgnoreCase);
}
