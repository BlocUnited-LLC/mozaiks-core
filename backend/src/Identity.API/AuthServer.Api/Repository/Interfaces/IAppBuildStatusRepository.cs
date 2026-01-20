using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces;

public interface IAppBuildStatusRepository
{
    Task<AppBuildStatusModel?> GetByAppIdAsync(string appId, CancellationToken cancellationToken);
    Task UpsertAsync(AppBuildStatusModel status, CancellationToken cancellationToken);
}

