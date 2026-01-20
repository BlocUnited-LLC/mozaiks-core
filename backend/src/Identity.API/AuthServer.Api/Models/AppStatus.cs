namespace AuthServer.Api.Models;

public enum AppStatus
{
    Draft = 0,
    Running = 1,
    Paused = 2,
    Stopped = 3,
    Failed = 4,
    Deleted = 5,
    Archived = Deleted
}
