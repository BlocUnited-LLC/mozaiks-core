namespace AuthServer.Api.Shared;

public sealed class DatabaseProvisioningOptions
{
    public string? AdminConnectionString { get; set; }

    public string MetadataDatabase { get; set; } = "mozaiks_platform";
}

