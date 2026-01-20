using AuthServer.Api.DTOs;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers;

[ApiController]
[Route("api/internal/users")]
[Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
public sealed class InternalUsersController : ControllerBase
{
    private readonly UserService _users;

    public InternalUsersController(UserService users)
    {
        _users = users;
    }

    [HttpGet("by-email/{email}")]
    public async Task<ActionResult<UserInfoResponse>> GetByEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "email is required" });
        }

        var user = await _users.GetUserByEmailAsync(email.Trim());
        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [HttpPatch("{id}/revoke")]
    public async Task<IActionResult> Revoke(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(new { error = "id is required" });
        }

        await _users.RevokeUserAsync(id.Trim());
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> SoftDelete(string id, [FromQuery] string? actorUserId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(new { error = "id is required" });
        }

        var actor = string.IsNullOrWhiteSpace(actorUserId) ? "internal" : actorUserId.Trim();
        await _users.SoftDeleteUserAsync(id.Trim(), actor);
        return NoContent();
    }
}

