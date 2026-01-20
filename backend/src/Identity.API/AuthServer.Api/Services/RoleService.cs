using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using System.Data;

namespace AuthServer.Api.Services
{
    public class RoleService
    {
        private readonly IRoleRepository _roleRepository;

        public RoleService(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }

        public async Task<bool> UserHasPermissionAsync(string userId, string permission, string appId)
        {
            return await _roleRepository.HasPermissionAsync(userId, permission, appId);
        }

        public async Task CreateNewRoleAsync(RoleModel role)
        {
            await _roleRepository.CreateRoleAsync(role);
        }

        public async Task UpdateExistingRoleAsync(string roleId, RoleModel role)
        {
            await _roleRepository.UpdateRoleAsync(roleId, role);
        }

        public async Task DeleteRoleAsync(string id)
        {
            await _roleRepository.DeleteRoleAsync(id);
        }
        public async Task<RoleModel> GetRoleByIdAsync(string id)
        {
           return await _roleRepository.GetRoleByIdAsync(id);  
        }
        public async Task<IEnumerable<RoleModel>> GetRolesByAppIdAsync(string id)
        {
            return await _roleRepository.GetRolesByAppIdAsync(id);
        }

        public async Task AddPermissionsToRoleAsync(string roleName, List<string> newPermissions)
        {
            var role = await _roleRepository.GetRoleByIdAsync(roleName);
            if (role == null) return;
            role.Permissions.AddRange(newPermissions);
            await _roleRepository.UpdateRoleAsync(roleName, role);
        }
    }
}
