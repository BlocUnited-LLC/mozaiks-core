namespace Payment.API.Models
{
    public class TransactionRecordModel : DocumentBase
    {
        public string TransactionId { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
    }
}
