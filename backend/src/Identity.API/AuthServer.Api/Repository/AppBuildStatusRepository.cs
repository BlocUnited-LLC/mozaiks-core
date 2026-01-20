using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Repository;

public sealed class AppBuildStatusRepository : IAppBuildStatusRepository
{
    private readonly IMongoCollection<AppBuildStatusModel> _collection;

    public AppBuildStatusRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AppBuildStatusModel>(MongoCollectionNames.AppBuildStatuses);
    }

    public async Task<AppBuildStatusModel?> GetByAppIdAsync(string appId, CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.AppId == appId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(AppBuildStatusModel status, CancellationToken cancellationToken)
    {
        status.UpdatedAt = DateTime.UtcNow;
        if (status.CreatedAt == default)
        {
            status.CreatedAt = status.UpdatedAt;
        }

        await _collection.ReplaceOneAsync(
            x => x.AppId == status.AppId,
            status,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}

