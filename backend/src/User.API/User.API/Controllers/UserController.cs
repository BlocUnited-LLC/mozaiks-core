using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mozaiks.Auth;
using User.API.Services;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UsersController> _logger;
    private readonly IUserContextAccessor _userContextAccessor;
    private readonly IServiceToServiceTokenProvider _serviceTokenProvider;

    public UsersController(
        IUserRepository userRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<UsersController> logger,
        IUserContextAccessor userContextAccessor,
        IServiceToServiceTokenProvider serviceTokenProvider)
    {
        _userRepository = userRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _userContextAccessor = userContextAccessor;
        _serviceTokenProvider = serviceTokenProvider;
    }

    [HttpGet("GetAllUsers")]
    [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
    public async Task<ActionResult<IEnumerable<UserProfileModel>>> GetUsers()
    {
        var users = await _userRepository.GetAllUsers();
        return Ok(users);
    }

    [HttpGet("GetUserById/{id}")]
    [Authorize]
    public async Task<ActionResult<UserProfileModel>> GetUserById(string id)
    {
        var user = await _userRepository.GetUserById(id);
        if (user == null)
        {
            return NotFound();
        }
        return Ok(user);
    }


    [HttpPost]
    [Authorize]
    public async Task<ActionResult<UserProfileModel>> CreateUser(UserProfileModel user)
    {
        if (user is null)
        {
            return BadRequest(new { error = "request body is required" });
        }

        var callerEmail = _userContextAccessor.GetUser(User)?.Email;
        if (string.IsNullOrWhiteSpace(callerEmail))
        {
            return Unauthorized(new { error = "Unauthorized", reason = "MissingEmailClaim" });
        }

        if (!string.IsNullOrWhiteSpace(user.Email)
            && !string.Equals(user.Email, callerEmail, System.StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        user.Email = callerEmail;
        user.EmailVerified = false;
        user.PhoneVerified = false;
        user.IsActive = true;
        user.CreatedAt = System.DateTime.UtcNow;
        user.UpdatedAt = user.CreatedAt;

        await _userRepository.CreateUser(user);
        return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
    }

    [HttpPut("UpdateProfile/{id}")]
    [Authorize]
    public async Task<ActionResult> UpdateProfile(string id, UserProfileModel user)
    {
        if (id != user.Id)
        {
            return BadRequest();
        }

        await _userRepository.UpdateUser(user);
        return NoContent();
    }

    [HttpDelete("DeleteUser/{id}")]
    [Authorize(Policy = MozaiksAuthDefaults.RequireSuperAdminPolicy)]
    public async Task<ActionResult> DeleteUser(string id, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetUserById(id);
        if (user == null)
        {
            return NotFound();
        }

        var actorUserId = _userContextAccessor.GetUser(User)?.UserId ?? "unknown";
        if (string.IsNullOrWhiteSpace(actorUserId) || actorUserId == "unknown")
        {
            return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Conflict(new { error = "cannot_delete_user", reason = "MissingProfileEmail" });
        }

        var serviceToken = await _serviceTokenProvider.GetAccessTokenAsync(cancellationToken);
        var authUserId = await ResolveAuthServerUserIdByEmailAsync(user.Email, serviceToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(authUserId))
        {
            return Conflict(new { error = "cannot_delete_user", reason = "AuthUserNotFound" });
        }

        var client = _httpClientFactory.CreateClient("AuthServer");
        using (var request = new HttpRequestMessage(
                   HttpMethod.Delete,
                   $"api/internal/users/{Uri.EscapeDataString(authUserId)}?actorUserId={Uri.EscapeDataString(actorUserId)}"))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {serviceToken}");
            if (Request.Headers.TryGetValue("X-Correlation-ID", out var cid))
            {
                request.Headers.TryAddWithoutValidation("X-Correlation-ID", cid.ToString());
            }
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AuthServer delete user failed. profileId={ProfileId} authUserId={AuthUserId} status={StatusCode}",
                    id,
                    authUserId,
                    (int)response.StatusCode);

                return StatusCode((int)response.StatusCode, new { error = "authserver_delete_failed" });
            }
        }

        await _userRepository.DeleteUser(id);
        return NoContent();
    }
    [HttpPut("revoke/{id}")]
    [Authorize(Policy = MozaiksAuthDefaults.RequireSuperAdminPolicy)]
    public async Task<ActionResult> RevokeUser(string id, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetUserById(id);
        if (user == null)
        {
            return NotFound();
        }

        var serviceToken = await _serviceTokenProvider.GetAccessTokenAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Conflict(new { error = "cannot_revoke_user", reason = "MissingProfileEmail" });
        }

        var authUserId = await ResolveAuthServerUserIdByEmailAsync(user.Email, serviceToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(authUserId))
        {
            return Conflict(new { error = "cannot_revoke_user", reason = "AuthUserNotFound" });
        }

        var client = _httpClientFactory.CreateClient("AuthServer");
        using (var request = new HttpRequestMessage(
                   HttpMethod.Patch,
                   $"api/internal/users/{Uri.EscapeDataString(authUserId)}/revoke"))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {serviceToken}");
            if (Request.Headers.TryGetValue("X-Correlation-ID", out var cid))
            {
                request.Headers.TryAddWithoutValidation("X-Correlation-ID", cid.ToString());
            }
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AuthServer revoke user failed. profileId={ProfileId} authUserId={AuthUserId} status={StatusCode}",
                    id,
                    authUserId,
                    (int)response.StatusCode);

                return StatusCode((int)response.StatusCode, new { error = "authserver_revoke_failed" });
            }
        }

        await _userRepository.RevokeUser(id);

        return NoContent();
    }

    private async Task<string?> ResolveAuthServerUserIdByEmailAsync(string email, string serviceToken, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("AuthServer");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/internal/users/by-email/{Uri.EscapeDataString(email)}");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {serviceToken}");
            if (Request.Headers.TryGetValue("X-Correlation-ID", out var cid))
            {
                request.Headers.TryAddWithoutValidation("X-Correlation-ID", cid.ToString());
            }

            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AuthServer GetUserByEmail failed. email={Email} status={StatusCode}",
                    email,
                    (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                return idProp.GetString();
            }

            if (doc.RootElement.TryGetProperty("Id", out var idPropPascal) && idPropPascal.ValueKind == JsonValueKind.String)
            {
                return idPropPascal.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve AuthServer userId by email {Email}", email);
            return null;
        }
    }
}
