using AuthServer.Api.Models;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Data;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly RoleService _roleService;
        private readonly IUserContextAccessor _userContextAccessor;

        public RolesController(RoleService roleService, IUserContextAccessor userContextAccessor)
        {
            _roleService = roleService;
            _userContextAccessor = userContextAccessor;
        }

        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpPost("CreateRole")]
        public async Task<IActionResult> CreateRole([FromBody] RoleModel role)
        {
            await _roleService.CreateNewRoleAsync(role);
            return Ok(role);
        }

        [HttpGet("GetRoleById/{id}")]
        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        public async Task<IActionResult> GetRole(string id)
        {
            var role = await _roleService.GetRoleByIdAsync(id);
            if (role == null)
                return NotFound();

            return Ok(role);
        }
        [HttpGet("GetRoleByAppId/{appId}")]
        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        public async Task<IActionResult> GetRoleByAppId(string appId)
        {
            var role = await _roleService.GetRolesByAppIdAsync(appId);
            if (role == null)
                return NotFound();

            return Ok(role);
        }

        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpPut("UpdateRole/{id}")]
        public async Task<IActionResult> UpdateRole(string id, [FromBody] RoleModel role)
        {
            await _roleService.UpdateExistingRoleAsync(id, role);
            return Ok(role);
        }

        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpDelete("DeleteRole/{id}")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            await _roleService.DeleteRoleAsync(id);
            return NoContent();
        }

        [HttpGet("HasPermission/{userId}/{permission}/{appId}")]
        public async Task<IActionResult> HasPermission(string userId, string permission, string appId)
        {
            var callerUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(callerUserId) || callerUserId == "unknown")
            {
                return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
            }

            if (!IsPlatformAdmin() && !string.Equals(callerUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var hasPermission = await _roleService.UserHasPermissionAsync(userId, permission, appId);
            return Ok(hasPermission);
        }

        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpPost("AddPermissions/{roleName}/{newPermissions}")]
        public async Task<IActionResult> AddPermissionsToRole(string roleName, List<string> newPermissions)
        {
            await _roleService.AddPermissionsToRoleAsync(roleName, newPermissions);
            return Ok();
        }

        private string GetCurrentUserId()
        {
            return _userContextAccessor.GetUser(User)?.UserId ?? "unknown";
        }

        private bool IsPlatformAdmin()
        {
            var user = _userContextAccessor.GetUser(User);
            if (user is null)
            {
                return false;
            }

            return user.IsSuperAdmin
                   || user.Roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));
        }
    }
}
