using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Repository;

public sealed class AppMonetizationSpecRepository : IAppMonetizationSpecRepository
{
    private readonly IMongoCollection<AppMonetizationSpecVersion> _collection;

    public AppMonetizationSpecRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AppMonetizationSpecVersion>(MongoCollectionNames.AppMonetizationSpecVersions);
    }

    public async Task<AppMonetizationSpecVersion?> GetLatestAsync(string appId, CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.AppId == appId)
            .SortByDescending(x => x.Version)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AppMonetizationSpecVersion?> GetLatestCommittedAsync(string appId, CancellationToken cancellationToken)
    {
        return await _collection.Find(x => x.AppId == appId && x.Status == AppMonetizationSpecStatus.Committed)
            .SortByDescending(x => x.Version)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> GetNextVersionAsync(string appId, CancellationToken cancellationToken)
    {
        var latest = await GetLatestAsync(appId, cancellationToken);
        return latest == null ? 1 : Math.Max(1, latest.Version + 1);
    }

    public async Task InsertAsync(AppMonetizationSpecVersion version, CancellationToken cancellationToken)
    {
        version.CreatedAt = DateTime.UtcNow;
        version.UpdatedAt = version.CreatedAt;
        if (version.CreatedAtUtc == default)
        {
            version.CreatedAtUtc = version.CreatedAt;
        }

        await _collection.InsertOneAsync(version, cancellationToken: cancellationToken);
    }

    public async Task ArchiveCommittedAsync(string appId, DateTime archivedAtUtc, CancellationToken cancellationToken)
    {
        var filter = Builders<AppMonetizationSpecVersion>.Filter.Eq(x => x.AppId, appId)
                     & Builders<AppMonetizationSpecVersion>.Filter.Eq(x => x.Status, AppMonetizationSpecStatus.Committed);

        var update = Builders<AppMonetizationSpecVersion>.Update
            .Set(x => x.Status, AppMonetizationSpecStatus.Archived)
            .Set(x => x.UpdatedAt, archivedAtUtc);

        await _collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
    }
}
