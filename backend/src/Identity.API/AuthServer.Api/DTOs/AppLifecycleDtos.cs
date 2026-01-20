namespace AuthServer.Api.DTOs
{
    public class PauseResumeRequest
    {
        public string? Reason { get; set; }
    }

    public class DeleteMozaiksAppRequest
    {
        public bool Confirm { get; set; }
        public string? Reason { get; set; }
    }

    public class AppLifecycleStateResponse
    {
        public string AppId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? PausedAt { get; set; }
        public DateTime? ResumedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public DateTime? HardDeleteAt { get; set; }
    }
}

