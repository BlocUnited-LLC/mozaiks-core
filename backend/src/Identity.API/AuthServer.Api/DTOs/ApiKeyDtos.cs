namespace AuthServer.Api.DTOs;

public sealed record ApiKeyGenerateResponse(
    string ApiKey,
    DateTime CreatedAt,
    string Prefix);

public sealed record ApiKeyRegenerateResponse(
    string ApiKey,
    DateTime CreatedAt,
    string Prefix,
    bool PreviousKeyInvalidated,
    int Version);

public sealed record ApiKeyStatusResponse(
    bool Exists,
    string Prefix,
    DateTime? CreatedAt,
    DateTime? LastUsedAt,
    int Version);

