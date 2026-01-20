namespace AuthServer.Api.Models
{
    public class SubscriptionPlanModel : DocumentBase
    {
        public SubscriptionCategory SubscriptionCategory { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string IdealFor { get; set; }

        // Optional Fields
        public int? MonthlyTokens { get; set; }
        public string? HostingLevel { get; set; }
        public string? Traffic { get; set; }
        public string? SLA { get; set; }
        public string? DiscountNote { get; set; }
        public int? MaxDomains { get; set; }
        public bool? EmailEnabled { get; set; }
        public int? MaxEmailDomains { get; set; }
        public bool? TokenOverageAllowed { get; set; }
    }
    public enum SubscriptionCategory
    {
        Free =1,
        Build =2,
        Host =3,
        Bundle =4
    }

}
