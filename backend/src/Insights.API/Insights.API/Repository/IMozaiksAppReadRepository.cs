using Insights.API.Models;

namespace Insights.API.Repository;

public interface IMozaiksAppReadRepository
{
    Task<MozaiksAppReadModel?> GetByIdAsync(string appId, CancellationToken cancellationToken);
}

