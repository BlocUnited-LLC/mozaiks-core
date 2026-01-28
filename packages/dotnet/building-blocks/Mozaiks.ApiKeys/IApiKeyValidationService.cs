namespace Mozaiks.ApiKeys;

public interface IApiKeyValidationService
{
    Task<ApiKeyValidationResult> ValidateAsync(string appId, string apiKey);
    Task UpdateLastUsedAsync(string appId);
}

