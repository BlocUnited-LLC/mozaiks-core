namespace Payment.API.Models
{
    public class RefundRequest
    {
        public long Amount { get; set; } // Amount in cents (optional)
        public string Reason { get; set; } = "requested_by_customer";
        public string RefundedBy { get; set; } // Admin/User initiating the refund
        public string PaymentIntentId { get; set; }
        public string WalletId { get; set; } = string.Empty;
    }
}
