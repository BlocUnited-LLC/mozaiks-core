using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces
{
    public interface IPermissionRepository
    {
        Task<PermissionModel> CreatePermissionAsync(PermissionModel role);
        Task<PermissionModel> GetPermissionByIdAsync(string permissionId);
        Task<IEnumerable<PermissionModel>> GetPermissionByGlobalRoleIdAsync(int roleId);
        Task UpdatePermissionAsync(string permissionId, PermissionModel permissionModel);
        Task DeletePermissionAsync(string permissionId);
        Task<IEnumerable<PermissionModel>> GetPermissionsByAppIdAsync(string appId);
        Task<IEnumerable<PermissionModel>> GetAllPermissionAsync();
    }
}
