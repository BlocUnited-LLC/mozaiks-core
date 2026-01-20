using Insights.API.Models;

namespace Insights.API.Repository;

public interface IHostedAppReadRepository
{
    Task<HostedApp?> GetByAppIdAsync(string appId, CancellationToken cancellationToken);
}
