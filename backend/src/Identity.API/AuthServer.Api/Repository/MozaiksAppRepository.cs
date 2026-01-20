using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Repository
{
    public class MozaiksAppRepository : IMozaiksAppRepository
    {
        private readonly IMongoCollection<MozaiksAppModel> _apps;

        public MozaiksAppRepository(IMongoDatabase database)
        {
            _apps = database.GetCollection<MozaiksAppModel>(MongoCollectionNames.MozaiksApps);
        }

        public Task<List<MozaiksAppModel>> GetAllAppsAsync()
            => _apps.Find(_ => true).ToListAsync();

        public Task<MozaiksAppModel?> GetByIdAsync(string id)
            => _apps.Find(a => a.Id == id).FirstOrDefaultAsync();

        public Task<List<MozaiksAppModel>> GetByOwnerUserIdAsync(string userId)
            => _apps.Find(a =>
                    a.OwnerUserId == userId
                    && a.Status != AppStatus.Deleted
                    && a.IsDeleted == false)
                .ToListAsync();

        public Task<List<MozaiksAppModel>> GetByIdsAsync(IEnumerable<string> appIds)
            => _apps.Find(a =>
                    appIds.Contains(a.Id)
                    && a.Status != AppStatus.Deleted
                    && a.IsDeleted == false)
                .ToListAsync();

        public Task<List<MozaiksAppModel>> GetPublicAsync()
            => _apps.Find(a =>
                    a.IsPublicMozaik == true
                    && a.Status != AppStatus.Deleted
                    && a.IsDeleted == false)
                .ToListAsync();

        public Task CreateAsync(MozaiksAppModel app)
            => _apps.InsertOneAsync(app);

        public Task UpdateAsync(MozaiksAppModel app)
            => _apps.ReplaceOneAsync(a => a.Id == app.Id, app);

        public Task DeleteAsync(string id)
            => _apps.DeleteOneAsync(a => a.Id == id);

        public Task AddMembersToTeamAsync(string appId, string[] memberIds)
            => _apps.UpdateOneAsync(
                Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId),
                Builders<MozaiksAppModel>.Update.PushEach(a => a.TeamMembers, memberIds));

        public async Task<bool> PatchAppConfigAsync(string appId, AppConfigPatchRequest request)
        {
            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);
            var updates = new List<UpdateDefinition<MozaiksAppModel>>
            {
                Builders<MozaiksAppModel>.Update.Set(a => a.UpdatedAt, DateTime.UtcNow)
            };

            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                updates.Add(Builders<MozaiksAppModel>.Update.Set(a => a.Name, request.DisplayName.Trim()));
            }

            if (request.Description != null)
            {
                updates.Add(Builders<MozaiksAppModel>.Update.Set(a => a.Description, request.Description));
            }

            if (request.AvatarUrl != null)
            {
                updates.Add(Builders<MozaiksAppModel>.Update.Set(a => a.LogoUrl, request.AvatarUrl));
            }

            if (request.InstalledPlugins != null)
            {
                updates.Add(Builders<MozaiksAppModel>.Update.Set(a => a.InstalledPlugins, request.InstalledPlugins));
            }

            var result = await _apps.UpdateOneAsync(filter, Builders<MozaiksAppModel>.Update.Combine(updates));
            return result.MatchedCount > 0;
        }

        public async Task<bool> SetPublishStatusAsync(string appId, bool publish)
        {
            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);
            var update = Builders<MozaiksAppModel>.Update
                .Set(a => a.IsPublicMozaik, publish)
                .Set(a => a.UpdatedAt, DateTime.UtcNow);
            var result = await _apps.UpdateOneAsync(filter, update);
            return result.MatchedCount > 0;
        }

        public async Task<bool> SetFeatureFlagAsync(string appId, string flag, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(flag))
            {
                throw new ArgumentException("flag is required", nameof(flag));
            }

            if (flag.Contains('.') || flag.Contains('$'))
            {
                throw new ArgumentException("flag contains invalid characters", nameof(flag));
            }

            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);

            var ensureFeatureFlags = Builders<MozaiksAppModel>.Update.Set(a => a.FeatureFlags, new Dictionary<string, bool>());
            await _apps.UpdateOneAsync(
                filter & Builders<MozaiksAppModel>.Filter.Eq(a => a.FeatureFlags, null),
                ensureFeatureFlags);

            var update = Builders<MozaiksAppModel>.Update
                .Set($"featureFlags.{flag}", enabled)
                .Set(a => a.UpdatedAt, DateTime.UtcNow);

            var result = await _apps.UpdateOneAsync(filter, update);
            return result.MatchedCount > 0;
        }

        public async Task<bool> TryGenerateApiKeyAsync(string appId, string apiKeyHash, string apiKeyPrefix, DateTime createdAtUtc)
        {
            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId)
                         & Builders<MozaiksAppModel>.Filter.Eq(a => a.ApiKeyHash, null);

            var update = Builders<MozaiksAppModel>.Update
                .Set(a => a.ApiKeyHash, apiKeyHash)
                .Set(a => a.ApiKeyPrefix, apiKeyPrefix)
                .Set(a => a.ApiKeyCreatedAt, createdAtUtc)
                .Set(a => a.ApiKeyLastUsedAt, (DateTime?)null)
                .Set(a => a.ApiKeyVersion, 1)
                .Set(a => a.UpdatedAt, DateTime.UtcNow);

            var result = await _apps.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> RegenerateApiKeyAsync(string appId, string apiKeyHash, string apiKeyPrefix, DateTime createdAtUtc)
        {
            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);

            var update = Builders<MozaiksAppModel>.Update
                .Set(a => a.ApiKeyHash, apiKeyHash)
                .Set(a => a.ApiKeyPrefix, apiKeyPrefix)
                .Set(a => a.ApiKeyCreatedAt, createdAtUtc)
                .Set(a => a.ApiKeyLastUsedAt, (DateTime?)null)
                .Inc(a => a.ApiKeyVersion, 1)
                .Set(a => a.UpdatedAt, DateTime.UtcNow);

            var result = await _apps.UpdateOneAsync(filter, update);
            return result.MatchedCount > 0;
        }

        public async Task<bool> UpdateApiKeyLastUsedAtAsync(string appId, DateTime lastUsedAtUtc)
        {
            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);
            var update = Builders<MozaiksAppModel>.Update
                .Set(a => a.ApiKeyLastUsedAt, lastUsedAtUtc);

            var result = await _apps.UpdateOneAsync(filter, update);
            return result.MatchedCount > 0;
        }

        public async Task<bool> SetGitHubDeploymentAsync(string appId, string repoUrl, string repoFullName, DateTime deployedAtUtc)
        {
            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);
            var update = Builders<MozaiksAppModel>.Update
                .Set(a => a.GitHubRepoUrl, repoUrl)
                .Set(a => a.GitHubRepoFullName, repoFullName)
                .Set(a => a.DeployedAt, deployedAtUtc)
                .Set(a => a.UpdatedAt, DateTime.UtcNow);

            var result = await _apps.UpdateOneAsync(filter, update);
            return result.MatchedCount > 0;
        }

        public async Task<bool> SetDatabaseProvisioningAsync(string appId, string databaseName, DateTime provisionedAtUtc)
        {
            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);
            var update = Builders<MozaiksAppModel>.Update
                .Set(a => a.DatabaseName, databaseName)
                .Set(a => a.DatabaseProvisionedAt, provisionedAtUtc)
                .Set(a => a.UpdatedAt, DateTime.UtcNow);

            var result = await _apps.UpdateOneAsync(filter, update);
            return result.MatchedCount > 0;
        }

        public async Task<bool> SetStatusAsync(string appId, AppStatus status, DateTime updatedAtUtc)
        {
            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);
            var update = Builders<MozaiksAppModel>.Update
                .Set(a => a.Status, status)
                .Set(a => a.UpdatedAt, updatedAtUtc);

            var result = await _apps.UpdateOneAsync(filter, update);
            return result.MatchedCount > 0;
        }
    }
}
