namespace AuthServer.Api.Shared;

public sealed record ApiKeyMaterial(
    string ApiKey,
    string Hash,
    string Prefix);

