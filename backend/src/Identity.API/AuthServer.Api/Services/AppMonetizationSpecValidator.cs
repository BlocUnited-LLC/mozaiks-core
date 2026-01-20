using AuthServer.Api.Models;

namespace AuthServer.Api.Services;

public sealed class AppMonetizationSpecValidator
{
    private static readonly HashSet<string> AllowedIntervals = new(StringComparer.OrdinalIgnoreCase)
    {
        "month"
    };

    private static readonly HashSet<string> AllowedHostingTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "starter",
        "scale",
        "enterprise"
    };

    public AppMonetizationSpecValidationResult Validate(AppMonetizationSpec spec)
    {
        var result = new AppMonetizationSpecValidationResult();

        if (spec == null)
        {
            result.Errors.Add("spec is required");
            return result;
        }

        if (spec.Plans == null || spec.Plans.Count == 0)
        {
            result.Errors.Add("spec.plans must contain at least one plan");
            return result;
        }

        var planIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in spec.Plans)
        {
            if (plan == null)
            {
                result.Errors.Add("spec.plans contains null plan");
                continue;
            }

            if (string.IsNullOrWhiteSpace(plan.PlanId))
            {
                result.Errors.Add("plan.planId is required");
            }
            else if (!planIds.Add(plan.PlanId.Trim()))
            {
                result.Errors.Add($"plan.planId must be unique: {plan.PlanId}");
            }

            if (string.IsNullOrWhiteSpace(plan.Name))
            {
                result.Errors.Add($"plan.name is required for planId={plan.PlanId}");
            }

            if (plan.PriceCents <= 0)
            {
                result.Errors.Add($"plan.priceCents must be > 0 for planId={plan.PlanId}");
            }

            if (!string.IsNullOrWhiteSpace(plan.Currency) && !IsCurrency(plan.Currency))
            {
                result.Errors.Add($"plan.currency must be 3-letter code for planId={plan.PlanId}");
            }

            if (!string.IsNullOrWhiteSpace(plan.BillingInterval) && !IsInterval(plan.BillingInterval))
            {
                result.Errors.Add($"plan.billingInterval must be 'month' for planId={plan.PlanId}");
            }

            ValidateEntitlements(plan.PlanId, plan.Entitlements, result);
        }

        var addOnIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (spec.AddOns != null)
        {
            foreach (var addOn in spec.AddOns)
            {
                if (addOn == null)
                {
                    result.Errors.Add("spec.addOns contains null add-on");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(addOn.AddOnId))
                {
                    result.Errors.Add("addOn.addOnId is required");
                }
                else if (!addOnIds.Add(addOn.AddOnId.Trim()))
                {
                    result.Errors.Add($"addOn.addOnId must be unique: {addOn.AddOnId}");
                }

                if (string.IsNullOrWhiteSpace(addOn.Name))
                {
                    result.Errors.Add($"addOn.name is required for addOnId={addOn.AddOnId}");
                }

                if (addOn.PriceCents <= 0)
                {
                    result.Errors.Add($"addOn.priceCents must be > 0 for addOnId={addOn.AddOnId}");
                }

                if (!string.IsNullOrWhiteSpace(addOn.Currency) && !IsCurrency(addOn.Currency))
                {
                    result.Errors.Add($"addOn.currency must be 3-letter code for addOnId={addOn.AddOnId}");
                }

                if (!string.IsNullOrWhiteSpace(addOn.BillingInterval) && !IsInterval(addOn.BillingInterval))
                {
                    result.Errors.Add($"addOn.billingInterval must be 'month' for addOnId={addOn.AddOnId}");
                }
            }
        }

        return result;
    }

    private static void ValidateEntitlements(string? planId, AppMonetizationEntitlementsSpec? entitlements, AppMonetizationSpecValidationResult result)
    {
        if (entitlements == null)
        {
            result.Errors.Add($"plan.entitlements is required for planId={planId}");
            return;
        }

        if (entitlements.TransactionFeeBps == null)
        {
            result.Errors.Add($"plan.entitlements.transactionFeeBps is required for planId={planId}");
        }
        else if (entitlements.TransactionFeeBps < 0 || entitlements.TransactionFeeBps > 10_000)
        {
            result.Errors.Add($"plan.entitlements.transactionFeeBps must be between 0 and 10000 for planId={planId}");
        }

        if (entitlements.MonthlyTokens.HasValue && entitlements.MonthlyTokens.Value < 0)
        {
            result.Errors.Add($"plan.entitlements.monthlyTokens must be >= 0 for planId={planId}");
        }

        if (entitlements.DomainsMax.HasValue && entitlements.DomainsMax.Value < 0)
        {
            result.Errors.Add($"plan.entitlements.domainsMax must be >= 0 for planId={planId}");
        }

        if (entitlements.EmailMaxDomains.HasValue && entitlements.EmailMaxDomains.Value < 0)
        {
            result.Errors.Add($"plan.entitlements.emailMaxDomains must be >= 0 for planId={planId}");
        }

        if (!string.IsNullOrWhiteSpace(entitlements.HostingTier) && !AllowedHostingTiers.Contains(entitlements.HostingTier.Trim()))
        {
            result.Errors.Add($"plan.entitlements.hostingTier must be one of starter|scale|enterprise for planId={planId}");
        }
    }

    private static bool IsCurrency(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Trim().Length == 3;

    private static bool IsInterval(string? value)
        => !string.IsNullOrWhiteSpace(value) && AllowedIntervals.Contains(value.Trim());
}

public sealed class AppMonetizationSpecValidationResult
{
    public List<string> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;
}
