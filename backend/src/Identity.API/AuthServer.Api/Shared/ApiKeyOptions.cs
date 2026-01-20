namespace AuthServer.Api.Shared;

public sealed class ApiKeyOptions
{
    public string Environment { get; set; } = "test";

    public int KeyLength { get; set; } = 32;
}

