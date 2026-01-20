namespace AuthServer.Api.Models
{
    public class TransactionRecordModel
    {
        public string TransactionId { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public DateTime Created { get; set; }
    }
}
