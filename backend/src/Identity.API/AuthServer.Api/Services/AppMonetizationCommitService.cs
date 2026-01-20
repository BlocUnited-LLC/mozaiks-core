using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;

namespace AuthServer.Api.Services;

public enum AppMonetizationCommitFailureKind
{
    InvalidRequest,
    NotFound,
    Forbidden,
    InvalidSpec,
    SpecHashMismatch,
    PolicyViolation,
    StripeProvisioningFailed
}

public sealed class AppMonetizationCommitResult
{
    public bool Succeeded { get; init; }
    public AppMonetizationCommitFailureKind? FailureKind { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public AppMonetizationCommitResponse? Response { get; init; }

    public static AppMonetizationCommitResult Fail(AppMonetizationCommitFailureKind kind, string error, IReadOnlyList<string>? errors = null)
        => new()
        {
            Succeeded = false,
            FailureKind = kind,
            Error = error,
            Errors = errors ?? Array.Empty<string>()
        };

    public static AppMonetizationCommitResult Ok(AppMonetizationCommitResponse response)
        => new() { Succeeded = true, Response = response };
}

public sealed class AppMonetizationCommitService
{
    private readonly MozaiksAppService _apps;
    private readonly ITeamMembersRepository _teams;
    private readonly IAppMonetizationSpecRepository _specs;
    private readonly AppMonetizationSpecValidator _validator;
    private readonly AppMonetizationPolicyEvaluator _policyEvaluator;
    private readonly AppMonetizationAuditService _audit;
    private readonly IMonetizationStripeProvisioner _stripe;
    private readonly StructuredLogEmitter _logs;

    public AppMonetizationCommitService(
        MozaiksAppService apps,
        ITeamMembersRepository teams,
        IAppMonetizationSpecRepository specs,
        AppMonetizationSpecValidator validator,
        AppMonetizationPolicyEvaluator policyEvaluator,
        AppMonetizationAuditService audit,
        IMonetizationStripeProvisioner stripe,
        StructuredLogEmitter logs)
    {
        _apps = apps;
        _teams = teams;
        _specs = specs;
        _validator = validator;
        _policyEvaluator = policyEvaluator;
        _audit = audit;
        _stripe = stripe;
        _logs = logs;
    }

    public async Task<AppMonetizationCommitResult> CommitAsync(
        string appId,
        AppMonetizationCommitRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId) || request == null)
        {
            return AppMonetizationCommitResult.Fail(AppMonetizationCommitFailureKind.InvalidRequest, "Invalid request.");
        }

        var actorUserId = (request.ApprovedByUserId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return AppMonetizationCommitResult.Fail(AppMonetizationCommitFailureKind.InvalidRequest, "approvedByUserId is required.");
        }

        var app = await _apps.GetByIdAsync(appId);
        if (app == null || app.IsDeleted || app.Status == AppStatus.Deleted)
        {
            return AppMonetizationCommitResult.Fail(AppMonetizationCommitFailureKind.NotFound, "App not found.");
        }

        var isAuthorized = await IsAppAdminAsync(appId, app.OwnerUserId, actorUserId, cancellationToken);
        if (!isAuthorized)
        {
            return AppMonetizationCommitResult.Fail(AppMonetizationCommitFailureKind.Forbidden, "User is not authorized to commit monetization.");
        }

        var validation = _validator.Validate(request.Spec);
        if (!validation.IsValid)
        {
            return AppMonetizationCommitResult.Fail(AppMonetizationCommitFailureKind.InvalidSpec, "Spec validation failed.", validation.Errors);
        }

        var (normalizedSpec, computedHash) = AppMonetizationSpecHasher.NormalizeAndHash(request.Spec);
        if (!string.Equals(computedHash, request.SpecHash?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            await _audit.TryWriteAsync(
                appId,
                actorUserId,
                "monetization.spec_hash_mismatch",
                correlationId,
                new
                {
                    request.SpecHash,
                    computedHash,
                    request.Source,
                    request.ProposalId
                },
                cancellationToken);

            return AppMonetizationCommitResult.Fail(AppMonetizationCommitFailureKind.SpecHashMismatch, "specHash does not match computed hash.");
        }

        await _audit.TryWriteAsync(
            appId,
            actorUserId,
            "monetization.proposal.received",
            correlationId,
            new
            {
                request.SpecHash,
                computedHash,
                request.Source,
                request.ProposalId,
                approvedByUserId = actorUserId,
                approvedAtUtc = request.ApprovedAtUtc,
                spec = normalizedSpec
            },
            cancellationToken);

        var previousCommitted = await _specs.GetLatestCommittedAsync(appId, cancellationToken);
        var nowUtc = request.ApprovedAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var policyResult = _policyEvaluator.Evaluate(normalizedSpec, previousCommitted, nowUtc);

        await _audit.TryWriteAsync(
            appId,
            actorUserId,
            "monetization.policy.evaluated",
            correlationId,
            new
            {
                allowed = policyResult.IsAllowed,
                failures = policyResult.Failures
            },
            cancellationToken);

        if (!policyResult.IsAllowed)
        {
            return AppMonetizationCommitResult.Fail(AppMonetizationCommitFailureKind.PolicyViolation, "Policy checks failed.", policyResult.Failures);
        }

        var version = await _specs.GetNextVersionAsync(appId, cancellationToken);
        var planVersions = normalizedSpec.Plans
            .Select(plan => new AppMonetizationPlanVersion
            {
                PlanId = plan.PlanId,
                Name = plan.Name,
                PriceCents = plan.PriceCents,
                Currency = plan.Currency,
                BillingInterval = plan.BillingInterval,
                Availability = plan.Availability,
                Entitlements = plan.Entitlements ?? new AppMonetizationEntitlementsSpec(),
                Stripe = new StripePlanMapping()
            })
            .ToList();

        var stripeResults = new List<MonetizationStripePlanResult>();
        var stripeSkipped = false;
        foreach (var plan in planVersions.Where(p => p.Availability == AppMonetizationPlanAvailability.NewSubscribersOnly))
        {
            var stripeResult = await _stripe.ProvisionPlanAsync(new MonetizationStripePlanRequest
            {
                AppId = appId,
                PlanId = plan.PlanId,
                PlanName = plan.Name,
                AmountCents = plan.PriceCents,
                Currency = plan.Currency,
                BillingInterval = plan.BillingInterval,
                SpecVersion = version,
                SpecHash = computedHash,
                ProposalId = request.ProposalId
            }, correlationId, cancellationToken);

            stripeResults.Add(stripeResult);
            if (stripeResult.Skipped)
            {
                stripeSkipped = true;
            }

            if (!stripeResult.Succeeded && !stripeResult.Skipped)
            {
                await _audit.TryWriteAsync(
                    appId,
                    actorUserId,
                    "monetization.stripe.failed",
                    correlationId,
                    new { planId = plan.PlanId, stripeResult.Error },
                    cancellationToken);

                return AppMonetizationCommitResult.Fail(AppMonetizationCommitFailureKind.StripeProvisioningFailed, stripeResult.Error ?? "Stripe provisioning failed.");
            }

            plan.Stripe = new StripePlanMapping
            {
                ProductId = stripeResult.StripeProductId,
                PriceId = stripeResult.StripePriceId,
                LookupKey = stripeResult.StripeLookupKey,
                CreatedAtUtc = stripeResult.Succeeded ? DateTime.UtcNow : null
            };
        }

        await _audit.TryWriteAsync(
            appId,
            actorUserId,
            "monetization.stripe.actions",
            correlationId,
            new
            {
                skipped = stripeSkipped,
                results = stripeResults.Select(r => new
                {
                    r.PlanId,
                    r.Succeeded,
                    r.Skipped,
                    r.Error,
                    r.StripeProductId,
                    r.StripePriceId,
                    r.StripeLookupKey
                })
            },
            cancellationToken);

        await _specs.ArchiveCommittedAsync(appId, nowUtc, cancellationToken);

        var versionRecord = new AppMonetizationSpecVersion
        {
            AppId = appId,
            SpecHash = computedHash,
            Version = version,
            CreatedAtUtc = nowUtc,
            CreatedByUserId = actorUserId,
            Status = AppMonetizationSpecStatus.Committed,
            ApprovedByUserId = actorUserId,
            ApprovedAtUtc = request.ApprovedAtUtc?.ToUniversalTime() ?? nowUtc,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "chat" : request.Source.Trim(),
            ProposalId = string.IsNullOrWhiteSpace(request.ProposalId) ? null : request.ProposalId.Trim(),
            Spec = normalizedSpec,
            PlanVersions = planVersions
        };

        await _specs.InsertAsync(versionRecord, cancellationToken);

        await _audit.TryWriteAsync(
            appId,
            actorUserId,
            "monetization.commit.completed",
            correlationId,
            new { version = versionRecord.Version, versionRecord.SpecHash, versionRecord.Status },
            cancellationToken);

        _logs.Info("Monetization.Commit.Completed", new StructuredLogContext
        {
            CorrelationId = correlationId,
            UserId = actorUserId,
            AppId = appId
        }, new { version = versionRecord.Version, specHash = versionRecord.SpecHash });

        var stripeStatus = stripeResults.Count == 0
            ? "not_required"
            : stripeSkipped
                ? "skipped"
                : "provisioned";

        var response = new AppMonetizationCommitResponse
        {
            AppId = appId,
            SpecHash = versionRecord.SpecHash,
            Version = versionRecord.Version,
            Status = versionRecord.Status.ToString().ToLowerInvariant(),
            CreatedAtUtc = versionRecord.CreatedAtUtc,
            ApprovedAtUtc = versionRecord.ApprovedAtUtc,
            StripeStatus = stripeStatus,
            Plans = planVersions.Select(plan => new AppMonetizationPlanProvisioningResponse
            {
                PlanId = plan.PlanId,
                Availability = plan.Availability.ToString(),
                StripeProductId = plan.Stripe.ProductId,
                StripePriceId = plan.Stripe.PriceId,
                StripeLookupKey = plan.Stripe.LookupKey
            }).ToList()
        };

        return AppMonetizationCommitResult.Ok(response);
    }

    private async Task<bool> IsAppAdminAsync(string appId, string ownerUserId, string actorUserId, CancellationToken cancellationToken)
    {
        if (string.Equals(ownerUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var member = await _teams.GetByAppAndUserIdAsync(appId, actorUserId);
        if (member == null || member.MemberStatus != 1)
        {
            return false;
        }

        return string.Equals(member.Role, "Owner", StringComparison.OrdinalIgnoreCase)
               || string.Equals(member.Role, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}
