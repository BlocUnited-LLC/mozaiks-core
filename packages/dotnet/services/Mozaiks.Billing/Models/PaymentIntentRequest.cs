namespace Payment.API.Models
{
    public class PaymentIntentRequest
    {
        public long Amount { get; set; }  // Example: $10 = 1000 cents
        public string Currency { get; set; } = "usd";
        public string UserId { get; set; }
        public string WalletId { get; set; }
        public string AppId { get; set; }
        public string? RoundId { get; set; }
        public string? InvestmentId { get; set; }
        public string? DestinationAccountId { get; set; }
        public long? ApplicationFeeAmount { get; set; }
        public string? TransactionType { get; set; }
        public string? SubscriptionId { get; set; }
    }
}
