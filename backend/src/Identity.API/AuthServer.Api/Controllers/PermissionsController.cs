using AuthServer.Api.Models;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PermissionsController : ControllerBase
    {
        private readonly PermissionService _service;

        public PermissionsController(PermissionService service)
        {
            _service = service;
        }
        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpPost("CreatePermission")]
        public async Task<IActionResult> CreatePermission([FromBody] PermissionModel model)
        {
            await _service.CreateNewPermissionAsync(model);
            return Ok(model);
        }
        [HttpGet("GetPermissionsByAppId/{appId}")]
        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        public async Task<IActionResult> GetPermissionsByAppId(string appId)
        {
            var permissions = await _service.GetPermissionsByAppIdAsync(appId);
            return Ok(permissions);
        }

        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpGet("GetAllPermissions")]
        public async Task<IActionResult> GetAllPermissions()
        {
            var role = await _service.GetAllPermissionAsync();
            if (role == null)
                return NotFound();

            return Ok(role);
        }

        [HttpGet("GetPermissions/{id}")]
        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        public async Task<IActionResult> GetPermission(string id)
        {
            var role = await _service.GetPermissionByIdAsync(id);
            if (role == null)
                return NotFound();

            return Ok(role);
        }
        [HttpGet("GetPermissionsByRoleId/{roleId}")]
        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        public async Task<IActionResult> GetPermissionsByRoleId(int roleId)
        {
            var role = await _service.GetPermissionByGlobalRoleIdAsync(roleId);
            if (role == null)
                return NotFound();

            return Ok(role);
        }

        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpPut("UpdatePermission/{id}")]
        public async Task<IActionResult> UpdatePermission(string id, [FromBody] PermissionModel model)
        {
            await _service.UpdatePermissionAsync(id, model);
            return Ok(model);
        }

        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpDelete("DeletePermission/{id}")]
        public async Task<IActionResult> DeletePermision(string id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }

         
    }
}
