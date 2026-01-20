using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces;

public interface IAppBuildEventRepository
{
    Task InsertAsync(AppBuildEventModel evt, CancellationToken cancellationToken);
}

