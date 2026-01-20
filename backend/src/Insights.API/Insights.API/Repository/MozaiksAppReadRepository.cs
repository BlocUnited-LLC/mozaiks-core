using Insights.API.Models;
using MongoDB.Driver;

namespace Insights.API.Repository;

public sealed class MozaiksAppReadRepository : IMozaiksAppReadRepository
{
    private readonly IMongoCollection<MozaiksAppReadModel> _collection;

    public MozaiksAppReadRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MozaiksAppReadModel>("MozaiksApps");
    }

    public async Task<MozaiksAppReadModel?> GetByIdAsync(string appId, CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.Id == appId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
