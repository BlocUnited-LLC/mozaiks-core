using AuthServer.Api.Models;
using System.Data;

namespace AuthServer.Api.Repository.Interfaces
{
    public interface IRoleRepository
    {
        Task<RoleModel> CreateRoleAsync(RoleModel role);
        Task<RoleModel> GetRoleByIdAsync(string roleId);
        Task<IEnumerable<RoleModel>> GetRolesByAppIdAsync(string appId);
        Task UpdateRoleAsync(string roleId, RoleModel role);
        Task DeleteRoleAsync(string roleId);
        Task<bool> HasPermissionAsync(string userId, string permission, string appId);
        Task AddPermissionsToRoleAsync(string roleName, List<string> newPermissions);
    }
}
