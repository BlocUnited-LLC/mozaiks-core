namespace AuthServer.Api.Models
{
    [MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElements]
    public class FundingRoundModel: DocumentBase
    {
        public string RoundName { get; set; } = string.Empty;
        public decimal Valuation { get; set; }
        public decimal CurrentRoundPercentage { get; set; }
        public decimal CurrentRoundAmount => Math.Round(Valuation * CurrentRoundPercentage / 100, 2);
        public string Description { get; set; } = string.Empty;
    }
}
