using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;

namespace AuthServer.Api.Services
{
    public class MozaiksAppService
    {
        private readonly IMozaiksAppRepository _repository;

        public MozaiksAppService(IMozaiksAppRepository repository)
        {
            _repository = repository;
        }

        public Task<List<MozaiksAppModel>> GetAllAsync()
            => _repository.GetAllAppsAsync();

        public Task<MozaiksAppModel?> GetByIdAsync(string id)
            => _repository.GetByIdAsync(id);

        public Task<List<MozaiksAppModel>> GetByOwnerUserIdAsync(string userId)
            => _repository.GetByOwnerUserIdAsync(userId);

        public Task<List<MozaiksAppModel>> GetByIdsAsync(IEnumerable<string> appIds)
            => _repository.GetByIdsAsync(appIds);

        public Task<List<MozaiksAppModel>> GetPublicAsync()
            => _repository.GetPublicAsync();

        public Task CreateAsync(MozaiksAppModel app)
            => _repository.CreateAsync(app);

        public Task UpdateAsync(MozaiksAppModel app)
            => _repository.UpdateAsync(app);

        public Task DeleteAsync(string id)
            => _repository.DeleteAsync(id);

        public Task AddMembersToTeamAsync(string appId, string[] memberIds)
            => _repository.AddMembersToTeamAsync(appId, memberIds);

        public Task<bool> PatchAppConfigAsync(string appId, AppConfigPatchRequest request)
            => _repository.PatchAppConfigAsync(appId, request);

        public Task<bool> SetPublishStatusAsync(string appId, bool publish)
            => _repository.SetPublishStatusAsync(appId, publish);

        public Task<bool> SetFeatureFlagAsync(string appId, string flag, bool enabled)
            => _repository.SetFeatureFlagAsync(appId, flag, enabled);

        public Task<bool> TryGenerateApiKeyAsync(string appId, string apiKeyHash, string apiKeyPrefix, DateTime createdAtUtc)
            => _repository.TryGenerateApiKeyAsync(appId, apiKeyHash, apiKeyPrefix, createdAtUtc);

        public Task<bool> RegenerateApiKeyAsync(string appId, string apiKeyHash, string apiKeyPrefix, DateTime createdAtUtc)
            => _repository.RegenerateApiKeyAsync(appId, apiKeyHash, apiKeyPrefix, createdAtUtc);

        public Task<bool> UpdateApiKeyLastUsedAtAsync(string appId, DateTime lastUsedAtUtc)
            => _repository.UpdateApiKeyLastUsedAtAsync(appId, lastUsedAtUtc);

        public Task<bool> SetGitHubDeploymentAsync(string appId, string repoUrl, string repoFullName, DateTime deployedAtUtc)
            => _repository.SetGitHubDeploymentAsync(appId, repoUrl, repoFullName, deployedAtUtc);

        public Task<bool> SetDatabaseProvisioningAsync(string appId, string databaseName, DateTime provisionedAtUtc)
            => _repository.SetDatabaseProvisioningAsync(appId, databaseName, provisionedAtUtc);

        public Task<bool> SetStatusAsync(string appId, AppStatus status, DateTime updatedAtUtc)
            => _repository.SetStatusAsync(appId, status, updatedAtUtc);

        /// <summary>
        /// Update GitHub repo URL and full name (used during initial export).
        /// </summary>
        public Task<bool> UpdateGitHubRepoAsync(string appId, string repoFullName, string repoUrl)
            => _repository.SetGitHubDeploymentAsync(appId, repoUrl, repoFullName, DateTime.UtcNow);
    }
}
