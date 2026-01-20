using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Repository
{
    public sealed class AppAdminSurfaceRepository : IAppAdminSurfaceRepository
    {
        private readonly IMongoCollection<AppAdminSurfaceModel> _collection;

        public AppAdminSurfaceRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<AppAdminSurfaceModel>(MongoCollectionNames.AppAdminSurfaces);
        }

        public async Task<AppAdminSurfaceModel?> GetByAppIdAsync(string appId, CancellationToken cancellationToken)
        {
            return await _collection.Find(x => x.AppId == appId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task UpsertAsync(AppAdminSurfaceModel model, CancellationToken cancellationToken)
        {
            model.UpdatedAt = DateTime.UtcNow;
            if (model.CreatedAt == default)
            {
                model.CreatedAt = DateTime.UtcNow;
            }

            var filter = Builders<AppAdminSurfaceModel>.Filter.Eq(x => x.AppId, model.AppId);
            var update = Builders<AppAdminSurfaceModel>.Update
                .Set(x => x.BaseUrl, model.BaseUrl)
                .Set(x => x.AdminKeyProtected, model.AdminKeyProtected)
                .Set(x => x.KeyVersion, model.KeyVersion)
                .Set(x => x.LastRotatedAt, model.LastRotatedAt)
                .Set(x => x.UpdatedByUserId, model.UpdatedByUserId)
                .Set(x => x.Notes, model.Notes)
                .Set(x => x.UpdatedAt, model.UpdatedAt)
                .SetOnInsert(x => x.Id, model.Id)
                .SetOnInsert(x => x.CreatedAt, model.CreatedAt);

            await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
        }
    }
}
