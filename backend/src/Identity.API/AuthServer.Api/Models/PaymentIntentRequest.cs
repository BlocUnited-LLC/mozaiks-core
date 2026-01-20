namespace AuthServer.Api.Models
{
    public class PaymentIntentRequest
    {
        public long Amount { get; set; }  // Example: $10 = 1000 cents
        public string Currency { get; set; } = "usd";
        public string UserId { get; set; }
        public string WalletId { get; set; }
        public string AppId { get; set; }
    }
}
