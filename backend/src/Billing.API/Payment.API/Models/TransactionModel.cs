namespace Payment.API.Models
{
    /// <summary>
    /// CONTROL PLANE ENTITY: Transaction / Subscription State
    /// 
    /// This tracks payment and subscription state.
    /// Collection: Transactions (MongoDB)
    /// Source of Truth: STRIPE (this service syncs from Stripe webhooks)
    /// 
    /// Transaction Types:
    /// - PlatformSubscriptionContract: Platform subscription record
    /// - AppSubscriptionContract: App-specific subscription
    /// - PlatformSubscriptionPayment: Payment for platform sub
    /// - AppSubscriptionPayment: Payment for app sub
    /// - Settlement: Payout to app creator (Stripe Connect)
    /// - Refund: Refund transaction
    /// 
    /// INVARIANT: Stripe is the source of truth for all billing.
    /// This service syncs state via webhooks, does not originate it.
    /// 
    /// Entitlement checks (AllowHosting, AllowExportRepo) query this service
    /// to determine if user/app has active subscription.
    /// </summary>
    public class TransactionModel: DocumentBase
    {
        public string TransactionType { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; } = "usd";
        public string WalletId { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public string PaymentIntentId { get; set; }
        public TransactionMetadata Metadata { get; set; }
        public string Status { get; set; }
    }
}
