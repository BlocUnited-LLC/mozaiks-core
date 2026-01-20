using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Repository;

public sealed class AppBuildEventRepository : IAppBuildEventRepository
{
    private readonly IMongoCollection<AppBuildEventModel> _collection;

    public AppBuildEventRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AppBuildEventModel>(MongoCollectionNames.AppBuildEvents);
    }

    public async Task InsertAsync(AppBuildEventModel evt, CancellationToken cancellationToken)
    {
        evt.CreatedAt = DateTime.UtcNow;
        evt.UpdatedAt = evt.CreatedAt;
        await _collection.InsertOneAsync(evt, cancellationToken: cancellationToken);
    }
}

