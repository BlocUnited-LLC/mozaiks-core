namespace Payment.API.Models
{
    public class TransactionMetadata
    {
        public string PayerUserId { get; set; } = string.Empty;
        public string AppCreatorId { get; set; } = string.Empty;
        public List<InvestorShare> InvestorShares { get; set; } = new();
        public string SubscriptionId { get; set; } = string.Empty;

        // MozaiksPay context (provider-agnostic naming).
        public string? Scope { get; set; }
        public string? PlanId { get; set; }
        public DateTime? CurrentPeriodEndUtc { get; set; }
        public string? CustomerRef { get; set; }

        // Platform fee auditing (Stripe Connect).
        public int? AppliedFeeBps { get; set; }
        public long? AppliedFeeAmount { get; set; }
    }
    public class InvestorShare
    {
        public string InvestorId { get; set; } = string.Empty;
        public decimal SharePercentage { get; set; } // e.g., 0.20 for 20%
    }
}
