using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Mozaiks.Auth.Authorization;

public sealed class SuperAdminHandler : AuthorizationHandler<SuperAdminRequirement>
{
    private readonly IUserContextAccessor _userContextAccessor;
    private readonly ILogger<SuperAdminHandler> _logger;

    public SuperAdminHandler(IUserContextAccessor userContextAccessor, ILogger<SuperAdminHandler> logger)
    {
        _userContextAccessor = userContextAccessor;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SuperAdminRequirement requirement)
    {
        var user = _userContextAccessor.GetUser(context.User);
        if (user?.IsSuperAdmin == true)
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogDebug("User rejected by RequireSuperAdmin policy");
        }

        return Task.CompletedTask;
    }
}

