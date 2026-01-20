namespace AuthServer.Api.DTOs
{
    public class CreateUserSubscriptionDto
    {
        public string UserId { get; set; }
        public string AppId { get; set; } = "";
        public string SubscriptionPlanId { get; set; }
    }
}
