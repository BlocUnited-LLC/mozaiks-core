namespace Payment.API.Models
{
    /// <summary>
    /// Internal balance wallet for fiat currency tracking.
    /// This is NOT a crypto wallet - it tracks payment credits similar to gift card balances.
    /// 
    /// BLOCKCHAIN INTEGRATION POINT:
    /// When adding blockchain support, extend this model with:
    /// - WalletType: "Fiat" | "Crypto" | "Hybrid"
    /// - BlockchainAddress: string? (for crypto wallets)
    /// - ChainId: string? (e.g., "ethereum", "polygon", "solana")
    /// - TokenBalances: Dictionary<string, decimal>? (for multi-token support)
    /// The Balance field will continue to track fiat, while TokenBalances tracks crypto.
    /// </summary>
    public class WalletModel : DocumentBase
    {
        public string AppId { get; set; }
        public string UserId { get; set; }
        
        /// <summary>
        /// Fiat balance in smallest currency unit (cents for USD).
        /// BLOCKCHAIN: Keep this for fiat; add TokenBalances for crypto.
        /// </summary>
        public long Balance { get; set; }

        public List<WalletTransaction> Transactions { get; set; } = new();
        
        // BLOCKCHAIN INTEGRATION: Uncomment when ready
        // public string? WalletType { get; set; } = "Fiat"; // Fiat, Crypto, Hybrid
        // public string? BlockchainAddress { get; set; }
        // public string? ChainId { get; set; }
    }
    
    /// <summary>
    /// Individual wallet transaction record.
    /// 
    /// BLOCKCHAIN INTEGRATION POINT:
    /// When adding blockchain support, extend with:
    /// - TxHash: string? (blockchain transaction hash)
    /// - ChainId: string? (which blockchain)
    /// - TokenAddress: string? (for ERC-20/SPL tokens)
    /// - GasFee: long? (transaction fee in native token units)
    /// </summary>
    public class WalletTransaction : DocumentBase
    {
        public string TransactionId { get; set; }
        
        /// <summary>
        /// Payment processor reference (Stripe PaymentIntent ID for fiat).
        /// BLOCKCHAIN: For crypto, this would be the transaction hash.
        /// </summary>
        public string PaymentIntentId { get; set; }
        
        public long Amount { get; set; }
        public string Currency { get; set; }
        public string TransactionType { get; set; } // Credit, Debit, Refund
        public string Status { get; set; } // Pending, Succeeded, Failed, Refunded
        
        // BLOCKCHAIN INTEGRATION: Uncomment when ready
        // public string? TxHash { get; set; }
        // public string? ChainId { get; set; }
        // public int? Confirmations { get; set; }
    }
}
