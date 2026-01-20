using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces
{
    public interface IAppAdminSurfaceRepository
    {
        Task<AppAdminSurfaceModel?> GetByAppIdAsync(string appId, CancellationToken cancellationToken);
        Task UpsertAsync(AppAdminSurfaceModel model, CancellationToken cancellationToken);
    }
}

