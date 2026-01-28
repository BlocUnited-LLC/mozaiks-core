namespace Payment.API.Models
{
    public class PaymentConfirmRequest
    {
        public string PaymentIntentId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? AppId { get; set; }
        public string? RoundId { get; set; }
        public string? InvestmentId { get; set; }
    }
}
