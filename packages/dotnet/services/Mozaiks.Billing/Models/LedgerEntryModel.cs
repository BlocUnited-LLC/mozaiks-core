namespace Payment.API.Models
{
    /// <summary>
    /// Double-entry ledger for auditable transaction tracking.
    /// Every money movement creates ledger entries for full traceability.
    /// 
    /// BLOCKCHAIN INTEGRATION POINT:
    /// This model is already blockchain-ready! When adding crypto:
    /// - Source: Add "Blockchain", "SmartContract" as valid sources
    /// - Add: TxHash, ChainId, BlockNumber for on-chain audit trail
    /// - Currency: Support token symbols ("ETH", "USDC", "SOL") alongside "usd"
    /// - The ledger pattern works identically for both fiat and crypto
    /// </summary>
    public class LedgerEntryModel: DocumentBase
    {
        public string UserId { get; set; }
        public string AppId { get; set; }              // Multi-tenant linkage
        public string WalletId { get; set; }           // or AccountId (AppCreator, Investor, Mozaiks)
        public string TransactionId { get; set; }      // Link to the main payment transaction
        
        /// <summary>
        /// Payment processor reference. For fiat: Stripe PaymentIntent ID.
        /// BLOCKCHAIN: For crypto, use the transaction hash here.
        /// </summary>
        public string PaymentIntentId { get; set; }
        
        public string Type { get; set; }               // "Credit", "Debit", "Refund", "Fee", etc.
        
        /// <summary>
        /// Origin of the transaction.
        /// Current: "PaymentProcessor", "Manual"
        /// BLOCKCHAIN: Add "Blockchain", "SmartContract", "Bridge"
        /// </summary>
        public string Source { get; set; }
        
        public string Reason { get; set; }             // "PlatformFee", "Investment", "AppSale"
        public long Amount { get; set; }               // Always in smallest unit (cents/wei/lamports)
        public string Currency { get; set; } = "usd"; // BLOCKCHAIN: Support "ETH", "USDC", etc.
        
        // BLOCKCHAIN INTEGRATION: Uncomment when ready
        // public string? TxHash { get; set; }
        // public string? ChainId { get; set; }
        // public long? BlockNumber { get; set; }
    }
}
