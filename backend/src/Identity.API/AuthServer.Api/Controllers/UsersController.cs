using AuthServer.Api.Models;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auditing;
using System.Security.Claims;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly ILogger<UserController> _logger;
        private readonly IUserContextAccessor _userContextAccessor;

        public UserController(UserService userService, ILogger<UserController> logger, IUserContextAccessor userContextAccessor)
        {
            _userService = userService;
            _logger = logger;
            _userContextAccessor = userContextAccessor;
        }


        [HttpGet("GetUserById/{id}")]
        public async Task<ActionResult<UserModel>> GetUserById(string id)
        {
            var user = await _userService.GetUserByIdAsync(id);
             
            if (user == null)
            {
                return NotFound();
            }
            else if (user.UserPhoto != null)
            {
                user.UserPhoto = $"{Request.Scheme}://{Request.Host}/images/{user.UserPhoto}";
            }
            return Ok(user);
        }
        [HttpGet("GetUserByEmail/{email}")]
        public async Task<ActionResult<UserModel>> GetUserByEmail(string email)
        {
            var user = await _userService.GetUserByEmailAsync(email);
            if (user == null)
            {
                return NotFound();
            }
            else if(user.UserPhoto != null)
            {
                user.UserPhoto = $"{Request.Scheme}://{Request.Host}/images/{user.UserPhoto}";
            }
            
            return Ok(user);
        }


        [HttpGet("GetAllUsersAdmin")]
        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        public async Task<ActionResult<IEnumerable<UserModel>>> GetAllUsersAdmin()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpPut("UpdateUser/{id}")]
        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        public async Task<IActionResult> UpdateUser(string id, UserModel user)
        {
            if (id != user.Id)
            {
                return BadRequest("User ID mismatch");
            }

            try
            {
                await _userService.UpdateUserAsync(user);
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while updating the user.");
            }
        }

        [HttpPut("UpdateUserProfile")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<IActionResult> UpdateUserProfile([FromForm] UserModel user, [FromForm] IFormFile? image)
        {

            try
            {
                if (image != null && image.Length > 0)
                {
                    var fileExtension = Path.GetExtension(image.FileName);
                    var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                    var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", uniqueFileName);

                    using (var stream = new FileStream(imagePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }
                    user.UserPhoto = uniqueFileName;
                    
                }
                var extraSlash = user.UserPhoto.Split("/");
                if (extraSlash.Length > 0)
                {
                    user.UserPhoto = extraSlash[extraSlash.Length -1];
                }
                await _userService.UpdateUserProfileAsync(user.Id,user);
                return Ok();
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while updating the user.");
            }
        }

        [HttpPatch("RevokeUser/{id}")]
        [Authorize(Policy = MozaiksAuthDefaults.RequireSuperAdminPolicy)]
        public async Task<IActionResult> RevokeUser(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { error = "invalid_user_id" });
            }

            HttpContext.SetAdminAuditContext(
                action: "UserRevoke",
                targetType: "User",
                targetId: id,
                details: $"userId={id}");

            try
            {
                await _userService.RevokeUserAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while revoking the user {UserId}", id);
                return StatusCode(500, "An error occurred while revoking the user.");
            }
        }

        [HttpDelete("DeleteUser/{id}")]
        [Authorize(Policy = MozaiksAuthDefaults.RequireSuperAdminPolicy)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { error = "invalid_user_id" });
            }

            var actorUserId = GetActorUserId();
            if (!string.IsNullOrWhiteSpace(actorUserId)
                && string.Equals(id, actorUserId, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "cannot_delete_self" });
            }

            HttpContext.SetAdminAuditContext(
                action: "UserDelete",
                targetType: "User",
                targetId: id,
                details: $"userId={id}");

            try
            {
                await _userService.SoftDeleteUserAsync(id, actorUserId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting the user {UserId}", id);
                return StatusCode(500, "An error occurred while deleting the user.");
            }
        }

        private string GetActorUserId()
        {
            return _userContextAccessor.GetUser(User)?.UserId ?? "unknown";
        }
    }
}
