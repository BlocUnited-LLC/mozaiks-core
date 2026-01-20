namespace Payment.API.Models
{
    public class PaymentTransactionModel : DocumentBase
    {
        public string TransactionId { get; set; }
        public string PaymentIntentId { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; }
        public string TransactionType { get; set; } // Credit, Debit, Refund
        public string Status { get; set; } // Pending, Succeeded, Failed, Refunded

    }
}
