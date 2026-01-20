using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Mozaiks.Auth.Authorization;

public sealed class PlatformAdminHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    private const string PlatformAdminRole = "Admin";

    private readonly IUserContextAccessor _userContextAccessor;
    private readonly ILogger<PlatformAdminHandler> _logger;

    public PlatformAdminHandler(IUserContextAccessor userContextAccessor, ILogger<PlatformAdminHandler> logger)
    {
        _userContextAccessor = userContextAccessor;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformAdminRequirement requirement)
    {
        var user = _userContextAccessor.GetUser(context.User);
        if (user is null)
        {
            return Task.CompletedTask;
        }

        if (user.IsSuperAdmin || user.Roles.Any(r => string.Equals(r, PlatformAdminRole, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogDebug("User rejected by RequirePlatformAdmin policy");
        }

        return Task.CompletedTask;
    }
}

