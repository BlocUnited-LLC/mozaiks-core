using MongoDB.Driver;

namespace Mozaiks.ApiKeys;

public sealed class ApiKeyValidationService : IApiKeyValidationService
{
    private readonly IMongoCollection<InternalMozaiksAppApiKeyDocument> _apps;

    public ApiKeyValidationService(IMongoDatabase database)
    {
        _apps = database.GetCollection<InternalMozaiksAppApiKeyDocument>("MozaiksApps");
    }

    public async Task<ApiKeyValidationResult> ValidateAsync(string appId, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return new ApiKeyValidationResult { IsValid = false, ErrorMessage = "Missing appId" };
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ApiKeyValidationResult { IsValid = false, AppId = appId, ErrorMessage = "Missing apiKey" };
        }

        var doc = await _apps.Find(x => x.Id == appId)
            .Project(x => new { x.Id, x.OwnerUserId, x.ApiKeyHash })
            .FirstOrDefaultAsync();

        if (doc is null)
        {
            return new ApiKeyValidationResult { IsValid = false, AppId = appId, ErrorMessage = "App not found" };
        }

        if (string.IsNullOrWhiteSpace(doc.ApiKeyHash))
        {
            return new ApiKeyValidationResult { IsValid = false, AppId = appId, OwnerUserId = doc.OwnerUserId, ErrorMessage = "API key not configured" };
        }

        if (!ApiKeyHashing.FixedTimeEqualsBase64Hash(apiKey, doc.ApiKeyHash))
        {
            return new ApiKeyValidationResult { IsValid = false, AppId = appId, OwnerUserId = doc.OwnerUserId, ErrorMessage = "Invalid API key" };
        }

        _ = UpdateLastUsedAsync(appId).ContinueWith(
            t => _ = t.Exception,
            TaskContinuationOptions.OnlyOnFaulted);

        return new ApiKeyValidationResult
        {
            IsValid = true,
            AppId = appId,
            OwnerUserId = doc.OwnerUserId
        };
    }

    public async Task UpdateLastUsedAsync(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return;
        }

        var filter = Builders<InternalMozaiksAppApiKeyDocument>.Filter.Eq(x => x.Id, appId);
        var update = Builders<InternalMozaiksAppApiKeyDocument>.Update
            .Set(x => x.ApiKeyLastUsedAt, DateTime.UtcNow);

        await _apps.UpdateOneAsync(filter, update);
    }
}

