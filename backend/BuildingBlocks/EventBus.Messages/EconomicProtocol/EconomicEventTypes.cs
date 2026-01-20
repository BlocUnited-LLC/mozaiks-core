namespace EventBus.Messages.EconomicProtocol;

/// <summary>
/// Economic Protocol event_type constants (v1).
/// </summary>
public static class EconomicEventTypes
{
    // App economic terms / fee policy
    public const string AppEconomicTermsSet = "app.economic_terms_set";
    public const string AppRoyaltyEnabled = "app.royalty_enabled";
    public const string AppRoyaltyDisabled = "app.royalty_disabled";

    // Campaign lifecycle
    public const string CampaignCreated = "campaign.created";
    public const string CampaignUpdated = "campaign.updated";
    public const string CampaignOpenedForFunding = "campaign.opened_for_funding";
    public const string CampaignFundingClosed = "campaign.funding_closed";
    public const string CampaignActivated = "campaign.activated";
    public const string CampaignPaused = "campaign.paused";
    public const string CampaignResumed = "campaign.resumed";
    public const string CampaignCompleted = "campaign.completed";
    public const string CampaignCancelled = "campaign.cancelled";

    // Funding / governance
    public const string RoundCreated = "round.created";
    public const string RoundApproved = "round.approved";
    public const string RoundOpened = "round.opened";
    public const string RoundClosed = "round.closed";
    public const string RoundCancelled = "round.cancelled";

    // Commitments
    public const string CommitmentCreated = "commitment.created";
    public const string CommitmentConfirmed = "commitment.confirmed";
    public const string CommitmentCancelled = "commitment.cancelled";

    // Allocation
    public const string AllocationCreated = "allocation.created";
    public const string AllocationDeployed = "allocation.deployed";

    // Spend & performance
    public const string SpendAuthorized = "spend.authorized";
    public const string SpendExecuted = "spend.executed";
    public const string SpendRefunded = "spend.refunded";
    public const string KpiReported = "kpi.reported";
    public const string CreativeGenerated = "creative.generated";
    public const string CreativeLaunched = "creative.launched";

    // Attribution
    public const string AttributionAssigned = "attribution.assigned";
    public const string AttributionUpdated = "attribution.updated";

    // Revenue facts
    public const string RevenueInvoicePaid = "revenue.invoice_paid";
    public const string RevenueRefundIssued = "revenue.refund_issued";
    public const string RevenueChargeback = "revenue.chargeback";
    public const string RevenueSubscriptionCancelled = "revenue.subscription_cancelled";

    // Ledger & payouts
    public const string LedgerAccrued = "ledger.accrued";
    public const string LedgerAdjusted = "ledger.adjusted";
    public const string SettlementInitiated = "settlement.initiated";
    public const string SettlementPaid = "settlement.paid";
    public const string SettlementFailed = "settlement.failed";
}
