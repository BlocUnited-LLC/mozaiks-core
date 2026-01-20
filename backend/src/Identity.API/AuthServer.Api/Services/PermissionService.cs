using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;


namespace AuthServer.Api.Services
{
    public class PermissionService
    {
        private readonly IPermissionRepository _repository;

        public PermissionService(IPermissionRepository repository)
        {
            _repository = repository;
        }
        public async Task CreateNewPermissionAsync(PermissionModel model)
        {
            await _repository.CreatePermissionAsync(model);
        }

        public async Task UpdatePermissionAsync(string permissionId, PermissionModel model)
        {
            await _repository.UpdatePermissionAsync(permissionId,model);
        }

        public async Task DeleteAsync(string id)
        {
            await _repository.DeletePermissionAsync(id);
        }
        public async Task<PermissionModel> GetPermissionByIdAsync(string id)
        {
           return await _repository.GetPermissionByIdAsync(id);  
        }
        public Task<IEnumerable<PermissionModel>> GetPermissionsByAppIdAsync(string appId)
            => _repository.GetPermissionsByAppIdAsync(appId);
        public async Task<IEnumerable<PermissionModel>> GetAllPermissionAsync()
        {
            return await _repository.GetAllPermissionAsync();
        }
        public async Task<IEnumerable<PermissionModel>> GetPermissionByGlobalRoleIdAsync(int roleId)
        {
           return await _repository.GetPermissionByGlobalRoleIdAsync(roleId);  
        }

         
    }
}
