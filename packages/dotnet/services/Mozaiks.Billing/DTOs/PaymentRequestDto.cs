using Payment.API.Models;

namespace Payment.API.DTOs
{
    public class PaymentRequestDto
    {
        public string TransactionType { get; set; }
        public long Amount { get; set; } // in cents
        public TransactionMetadata Metadata { get; set; }
    }
}
