namespace AuthServer.Api.Shared;

public sealed class DeploymentOptions
{
    public string PlatformApiUrl { get; set; } = string.Empty;

    public string BundleTempPath { get; set; } = string.Empty;

    public long MaxBundleSizeBytes { get; set; } = 100 * 1024 * 1024;
}
