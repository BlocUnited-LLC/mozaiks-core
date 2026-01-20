namespace AuthServer.Api.DTOs
{
    public class SubscriptionPlanDto
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string IdealFor { get; set; }
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
}
