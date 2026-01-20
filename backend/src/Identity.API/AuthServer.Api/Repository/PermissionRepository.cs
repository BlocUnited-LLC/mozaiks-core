using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using MongoDB.Driver;

namespace AuthServer.Api.Repository
{
    public class PermissionRepository : IPermissionRepository
    {
        private readonly IMongoCollection<PermissionModel> _permissionCollection;
        public PermissionRepository(IMongoDatabase database)
        {
            _permissionCollection = database.GetCollection<PermissionModel>("Permissions");
        }
        public async Task<PermissionModel> CreatePermissionAsync(PermissionModel permission)
        {
            await _permissionCollection.InsertOneAsync(permission);
            return permission;
        }

        public async Task DeletePermissionAsync(string permissionId)
        {
            var filter = Builders<PermissionModel>.Filter.Eq(r => r.Id, permissionId);
            await _permissionCollection.DeleteOneAsync(filter);
        }

        public async Task<IEnumerable<PermissionModel>> GetPermissionByGlobalRoleIdAsync(int roleId)
        {
            return await _permissionCollection.Find(r => r.GlobalRole == roleId).ToListAsync();
        }

        public async Task<IEnumerable<PermissionModel>> GetPermissionsByAppIdAsync(string appId)
        {
            return await _permissionCollection.Find(r => r.AppId == appId).ToListAsync();
        }

        public async Task<PermissionModel> GetPermissionByIdAsync(string permissionId)
        {
            return await _permissionCollection.Find(r => r.Id == permissionId).FirstOrDefaultAsync();
        }
        public async Task<IEnumerable<PermissionModel>> GetAllPermissionAsync()
        {
            return await _permissionCollection.Find(_ => true).ToListAsync();
        }

        public async Task UpdatePermissionAsync(string permissionId, PermissionModel permissionModel)
        {
            var filter = Builders<PermissionModel>.Filter.Eq(r => r.Id, permissionId);
            await _permissionCollection.ReplaceOneAsync(filter, permissionModel);
        }
    }
}
