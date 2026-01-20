using AuthServer.Api.Models;
using AuthServer.Api.Shared;
using Microsoft.Extensions.Options;

namespace AuthServer.Api.Services;

public sealed class AppMonetizationPolicyEvaluator
{
    private readonly MonetizationPolicyOptions _options;

    public AppMonetizationPolicyEvaluator(IOptions<MonetizationPolicyOptions> options)
    {
        _options = options.Value;
    }

    public AppMonetizationPolicyEvaluation Evaluate(AppMonetizationSpec spec, AppMonetizationSpecVersion? previousCommitted, DateTime nowUtc)
    {
        var evaluation = new AppMonetizationPolicyEvaluation();

        EvaluateCooldown(previousCommitted, nowUtc, evaluation);
        EvaluatePlanPrices(spec, previousCommitted, evaluation);
        EvaluateAddOnPrices(spec, previousCommitted, evaluation);
        EvaluateEntitlements(spec, previousCommitted, evaluation);

        evaluation.IsAllowed = evaluation.Failures.Count == 0;
        return evaluation;
    }

    private void EvaluateCooldown(AppMonetizationSpecVersion? previousCommitted, DateTime nowUtc, AppMonetizationPolicyEvaluation evaluation)
    {
        if (previousCommitted == null || _options.CooldownMinutes <= 0)
        {
            return;
        }

        var lastCommit = previousCommitted.ApprovedAtUtc ?? previousCommitted.CreatedAtUtc;
        var cooldown = TimeSpan.FromMinutes(_options.CooldownMinutes);
        var nextAllowed = lastCommit.Add(cooldown);

        if (nowUtc < nextAllowed)
        {
            var remaining = nextAllowed - nowUtc;
            evaluation.Failures.Add($"cooldown active: wait {Math.Ceiling(remaining.TotalMinutes)} minutes");
        }
    }

    private void EvaluatePlanPrices(AppMonetizationSpec spec, AppMonetizationSpecVersion? previousCommitted, AppMonetizationPolicyEvaluation evaluation)
    {
        var previousById = BuildPreviousPlanLookup(previousCommitted);

        foreach (var plan in spec.Plans)
        {
            if (plan.PriceCents < _options.PlanPriceFloorCents || plan.PriceCents > _options.PlanPriceCeilingCents)
            {
                evaluation.Failures.Add($"plan.priceCents out of bounds for planId={plan.PlanId}");
            }

            if (_options.MaxPriceDeltaBps > 0 && previousById.TryGetValue(plan.PlanId, out var previous))
            {
                var deltaBps = ComputeDeltaBps(previous.PriceCents, plan.PriceCents);
                if (deltaBps > _options.MaxPriceDeltaBps)
                {
                    evaluation.Failures.Add($"plan.priceCents delta exceeds max for planId={plan.PlanId}");
                }
            }
        }
    }

    private void EvaluateAddOnPrices(AppMonetizationSpec spec, AppMonetizationSpecVersion? previousCommitted, AppMonetizationPolicyEvaluation evaluation)
    {
        var previousById = BuildPreviousAddOnLookup(previousCommitted);

        foreach (var addOn in spec.AddOns)
        {
            if (addOn.PriceCents < _options.AddOnPriceFloorCents || addOn.PriceCents > _options.AddOnPriceCeilingCents)
            {
                evaluation.Failures.Add($"addOn.priceCents out of bounds for addOnId={addOn.AddOnId}");
            }

            if (_options.MaxPriceDeltaBps > 0 && previousById.TryGetValue(addOn.AddOnId, out var previous))
            {
                var deltaBps = ComputeDeltaBps(previous.PriceCents, addOn.PriceCents);
                if (deltaBps > _options.MaxPriceDeltaBps)
                {
                    evaluation.Failures.Add($"addOn.priceCents delta exceeds max for addOnId={addOn.AddOnId}");
                }
            }
        }
    }

    private void EvaluateEntitlements(AppMonetizationSpec spec, AppMonetizationSpecVersion? previousCommitted, AppMonetizationPolicyEvaluation evaluation)
    {
        var previousById = BuildPreviousPlanLookup(previousCommitted);

        foreach (var plan in spec.Plans)
        {
            var entitlements = plan.Entitlements ?? new AppMonetizationEntitlementsSpec();

            if (entitlements.MonthlyTokens.HasValue)
            {
                if (entitlements.MonthlyTokens.Value < _options.TokensMonthlyFloor || entitlements.MonthlyTokens.Value > _options.TokensMonthlyCeiling)
                {
                    evaluation.Failures.Add($"plan.entitlements.monthlyTokens out of bounds for planId={plan.PlanId}");
                }
            }

            if (entitlements.DomainsMax.HasValue)
            {
                if (entitlements.DomainsMax.Value < _options.DomainsMaxFloor || entitlements.DomainsMax.Value > _options.DomainsMaxCeiling)
                {
                    evaluation.Failures.Add($"plan.entitlements.domainsMax out of bounds for planId={plan.PlanId}");
                }
            }

            if (entitlements.EmailMaxDomains.HasValue)
            {
                if (entitlements.EmailMaxDomains.Value < _options.EmailMaxDomainsFloor || entitlements.EmailMaxDomains.Value > _options.EmailMaxDomainsCeiling)
                {
                    evaluation.Failures.Add($"plan.entitlements.emailMaxDomains out of bounds for planId={plan.PlanId}");
                }
            }

            if (entitlements.TransactionFeeBps.HasValue)
            {
                if (entitlements.TransactionFeeBps.Value < _options.TransactionFeeBpsFloor || entitlements.TransactionFeeBps.Value > _options.TransactionFeeBpsCeiling)
                {
                    evaluation.Failures.Add($"plan.entitlements.transactionFeeBps out of bounds for planId={plan.PlanId}");
                }
            }

            if (_options.MaxTransactionFeeDeltaBps > 0 && previousById.TryGetValue(plan.PlanId, out var previous))
            {
                var previousFee = previous.Entitlements?.TransactionFeeBps;
                var currentFee = entitlements.TransactionFeeBps;
                if (previousFee.HasValue && currentFee.HasValue)
                {
                    var delta = Math.Abs(currentFee.Value - previousFee.Value);
                    if (delta > _options.MaxTransactionFeeDeltaBps)
                    {
                        evaluation.Failures.Add($"plan.entitlements.transactionFeeBps delta exceeds max for planId={plan.PlanId}");
                    }
                }
            }
        }
    }

    private static Dictionary<string, AppMonetizationPlanSpec> BuildPreviousPlanLookup(AppMonetizationSpecVersion? previousCommitted)
    {
        if (previousCommitted == null)
        {
            return new Dictionary<string, AppMonetizationPlanSpec>(StringComparer.OrdinalIgnoreCase);
        }

        return previousCommitted.Spec?.Plans?
            .Where(p => !string.IsNullOrWhiteSpace(p.PlanId))
            .ToDictionary(p => p.PlanId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, AppMonetizationPlanSpec>(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, AppMonetizationAddOnSpec> BuildPreviousAddOnLookup(AppMonetizationSpecVersion? previousCommitted)
    {
        if (previousCommitted == null)
        {
            return new Dictionary<string, AppMonetizationAddOnSpec>(StringComparer.OrdinalIgnoreCase);
        }

        return previousCommitted.Spec?.AddOns?
            .Where(a => !string.IsNullOrWhiteSpace(a.AddOnId))
            .ToDictionary(a => a.AddOnId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, AppMonetizationAddOnSpec>(StringComparer.OrdinalIgnoreCase);
    }

    private static int ComputeDeltaBps(long previous, long current)
    {
        if (previous <= 0)
        {
            return 0;
        }

        var delta = Math.Abs(current - previous);
        return (int)Math.Round(delta * 10000m / previous, MidpointRounding.AwayFromZero);
    }
}

public sealed class AppMonetizationPolicyEvaluation
{
    public bool IsAllowed { get; set; }
    public List<string> Failures { get; } = new();
}
