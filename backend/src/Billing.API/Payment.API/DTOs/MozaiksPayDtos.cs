using System.ComponentModel.DataAnnotations;

namespace Payment.API.DTOs
{
    public static class MozaiksPayScopes
    {
        public const string Platform = "platform";
        public const string App = "app";
    }

    public static class MozaiksPayModes
    {
        public const string Subscription = "subscription";
        public const string Payment = "payment";
    }

    public sealed class MozaiksPayCheckoutRequest
    {
        [Required]
        public string Scope { get; set; } = MozaiksPayScopes.Platform;

        [Required]
        public string Mode { get; set; } = MozaiksPayModes.Subscription;

        public string? AppId { get; set; }

        [Required]
        public string PlanId { get; set; } = string.Empty;

        /// <summary>
        /// Amount in smallest currency unit (e.g., cents). Required for app scope and for one-time payments.
        /// </summary>
        public long? Amount { get; set; }

        public string Currency { get; set; } = "usd";

        public string? SuccessUrl { get; set; }
        public string? CancelUrl { get; set; }

        public Dictionary<string, string>? Metadata { get; set; }
    }

    public sealed class MozaiksPayCheckoutResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Mode { get; set; } = MozaiksPayModes.Subscription;
        public string Status { get; set; } = "pending";
    }

    public sealed class MozaiksPayStatusResponse
    {
        public bool IsActive { get; set; }
        public string? PlanId { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string? SubscriptionId { get; set; }
        public DateTime? CurrentPeriodEndUtc { get; set; }
        public List<string> Features { get; set; } = new();
    }

    public sealed class MozaiksPayCancelRequest
    {
        [Required]
        public string Scope { get; set; } = MozaiksPayScopes.Platform;

        public string? AppId { get; set; }
    }
}

