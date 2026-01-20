using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using MongoDB.Driver;
using System.Data;

namespace AuthServer.Api.Repository
{
    public class RoleRepository : IRoleRepository
    {
        private readonly IMongoCollection<RoleModel> _roleCollection;
        private readonly IMongoCollection<UserModel> _userCollection;

        public RoleRepository(IMongoDatabase database)
        {
            _roleCollection = database.GetCollection<RoleModel>("Roles");
            _userCollection = database.GetCollection<UserModel>("AppUsers");
        }

        public async Task<RoleModel> CreateRoleAsync(RoleModel role)
        {
            await _roleCollection.InsertOneAsync(role);
            return role;
        }

        public async Task<RoleModel> GetRoleByIdAsync(string roleId)
        {
            return await _roleCollection.Find(r => r.Id == roleId).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<RoleModel>> GetRolesByAppIdAsync(string appId)
        {
            return await _roleCollection.Find(r => r.AppId == appId).ToListAsync();
        }

        public async Task UpdateRoleAsync(string roleId, RoleModel role)
        {
            var filter = Builders<RoleModel>.Filter.Eq(r => r.Id, roleId);
            await _roleCollection.ReplaceOneAsync(filter, role);
        }

        public async Task DeleteRoleAsync(string roleId)
        {
            var filter = Builders<RoleModel>.Filter.Eq(r => r.Id, roleId);
            await _roleCollection.DeleteOneAsync(filter);
        }

        public async Task<bool> HasPermissionAsync(string userId, string permission, string appId)
        {
            var user = await _userCollection.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null) return false;

            foreach (var userRole in user.Roles)
            {
                if (userRole.AppId != appId) continue;

                var role = await _roleCollection.Find(r => r.Id == userRole.RoleId).FirstOrDefaultAsync();
                if (role != null && role.Permissions.Contains(permission))
                {
                    return true;
                }
            }

            return false;
        }
        public async Task AddPermissionsToRoleAsync(string roleName, List<string> newPermissions)
        {
            var role = await _roleCollection.Find(r => r.Name == roleName).FirstOrDefaultAsync();
            if (role != null)
            {
                // Adding new permissions to the existing list of permissions
                role.Permissions.AddRange(newPermissions.Distinct().Where(p => !role.Permissions.Contains(p)));

                // Update the role in the database
                await _roleCollection.ReplaceOneAsync(r => r.Name == roleName, role);
            }
            else
            {
                throw new ArgumentException("Role not found");
            }
        }
    }
}
