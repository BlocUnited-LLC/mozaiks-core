using AuthServer.Api.DTOs;
using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces
{
    public interface IMozaiksAppRepository
    {
        Task<List<MozaiksAppModel>> GetAllAppsAsync();
        Task<MozaiksAppModel?> GetByIdAsync(string id);
        Task<List<MozaiksAppModel>> GetByOwnerUserIdAsync(string userId);
        Task<List<MozaiksAppModel>> GetByIdsAsync(IEnumerable<string> appIds);
        Task<List<MozaiksAppModel>> GetPublicAsync();
        Task CreateAsync(MozaiksAppModel app);
        Task UpdateAsync(MozaiksAppModel app);
        Task DeleteAsync(string id);
        Task AddMembersToTeamAsync(string appId, string[] memberIds);
        Task<bool> PatchAppConfigAsync(string appId, AppConfigPatchRequest request);
        Task<bool> SetPublishStatusAsync(string appId, bool publish);
        Task<bool> SetFeatureFlagAsync(string appId, string flag, bool enabled);

        Task<bool> TryGenerateApiKeyAsync(string appId, string apiKeyHash, string apiKeyPrefix, DateTime createdAtUtc);
        Task<bool> RegenerateApiKeyAsync(string appId, string apiKeyHash, string apiKeyPrefix, DateTime createdAtUtc);
        Task<bool> UpdateApiKeyLastUsedAtAsync(string appId, DateTime lastUsedAtUtc);

        Task<bool> SetGitHubDeploymentAsync(string appId, string repoUrl, string repoFullName, DateTime deployedAtUtc);

        Task<bool> SetDatabaseProvisioningAsync(string appId, string databaseName, DateTime provisionedAtUtc);

        Task<bool> SetStatusAsync(string appId, AppStatus status, DateTime updatedAtUtc);
    }
}
