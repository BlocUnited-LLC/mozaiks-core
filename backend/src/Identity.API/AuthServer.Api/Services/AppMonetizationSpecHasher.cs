using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuthServer.Api.Models;

namespace AuthServer.Api.Services;

public static class AppMonetizationSpecHasher
{
    public static (AppMonetizationSpec Normalized, string Hash) NormalizeAndHash(AppMonetizationSpec spec)
    {
        var normalized = Normalize(spec);
        var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return (normalized, hash);
    }

    public static AppMonetizationSpec Normalize(AppMonetizationSpec spec)
    {
        var normalized = new AppMonetizationSpec
        {
            Currency = NormalizeCurrency(spec.Currency)
        };

        var plans = (spec.Plans ?? new List<AppMonetizationPlanSpec>())
            .Select(p => NormalizePlan(p, normalized.Currency))
            .OrderBy(p => p.PlanId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var addOns = (spec.AddOns ?? new List<AppMonetizationAddOnSpec>())
            .Select(a => NormalizeAddOn(a, normalized.Currency))
            .OrderBy(a => a.AddOnId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        normalized.Plans = plans;
        normalized.AddOns = addOns;
        return normalized;
    }

    private static AppMonetizationPlanSpec NormalizePlan(AppMonetizationPlanSpec plan, string defaultCurrency)
    {
        var entitlements = plan.Entitlements ?? new AppMonetizationEntitlementsSpec();

        return new AppMonetizationPlanSpec
        {
            PlanId = NormalizeId(plan.PlanId),
            Name = (plan.Name ?? string.Empty).Trim(),
            Description = string.IsNullOrWhiteSpace(plan.Description) ? null : plan.Description.Trim(),
            PriceCents = plan.PriceCents,
            Currency = NormalizeCurrency(string.IsNullOrWhiteSpace(plan.Currency) ? defaultCurrency : plan.Currency),
            BillingInterval = NormalizeInterval(plan.BillingInterval),
            Availability = plan.Availability,
            Entitlements = new AppMonetizationEntitlementsSpec
            {
                MonthlyTokens = entitlements.MonthlyTokens,
                HostingTier = NormalizeTier(entitlements.HostingTier),
                DomainsMax = entitlements.DomainsMax,
                EmailEnabled = entitlements.EmailEnabled,
                EmailMaxDomains = entitlements.EmailMaxDomains,
                TransactionFeeBps = entitlements.TransactionFeeBps ?? 0
            }
        };
    }

    private static AppMonetizationAddOnSpec NormalizeAddOn(AppMonetizationAddOnSpec addOn, string defaultCurrency)
    {
        var delta = addOn.EntitlementsDelta ?? new AppMonetizationEntitlementsDelta();

        return new AppMonetizationAddOnSpec
        {
            AddOnId = NormalizeId(addOn.AddOnId),
            Name = (addOn.Name ?? string.Empty).Trim(),
            Description = string.IsNullOrWhiteSpace(addOn.Description) ? null : addOn.Description.Trim(),
            PriceCents = addOn.PriceCents,
            Currency = NormalizeCurrency(string.IsNullOrWhiteSpace(addOn.Currency) ? defaultCurrency : addOn.Currency),
            BillingInterval = NormalizeInterval(addOn.BillingInterval),
            EntitlementsDelta = new AppMonetizationEntitlementsDelta
            {
                MonthlyTokensDelta = delta.MonthlyTokensDelta,
                DomainsMaxDelta = delta.DomainsMaxDelta,
                EmailMaxDomainsDelta = delta.EmailMaxDomainsDelta,
                EmailEnabled = delta.EmailEnabled,
                HostingTier = NormalizeTier(delta.HostingTier),
                TransactionFeeBps = delta.TransactionFeeBps
            }
        };
    }

    private static string NormalizeId(string value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeCurrency(string value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeInterval(string? value)
        => string.IsNullOrWhiteSpace(value) ? "month" : value.Trim().ToLowerInvariant();

    private static string? NormalizeTier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
