using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SubscriptionPlanController : ControllerBase
    {
        private readonly SubscriptionPlanService _subscriptionPlanService;
        private readonly IUserContextAccessor _userContextAccessor;

        public SubscriptionPlanController(SubscriptionPlanService subscriptionPlanService, IUserContextAccessor userContextAccessor)
        {
            _subscriptionPlanService = subscriptionPlanService;
            _userContextAccessor = userContextAccessor;
        }

        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpPost("add")]
        public async Task<ActionResult<bool>> AddSubscriptionPlan([FromBody] SubscriptionPlanModel plan)
        {
            try
            {
                var result = await _subscriptionPlanService.AddSubscriptionPlanAsync(plan);
                return Ok(result);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpPut("update/{id}")]
        public async Task<ActionResult<bool>> UpdateSubscriptionPlan(string id, [FromBody] SubscriptionPlanModel updatedPlan)
        {
            try
            {
                var result = await _subscriptionPlanService.UpdateSubscriptionPlanAsync(id, updatedPlan);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    [AllowAnonymous]
    [HttpGet("category/{category}")]
        public async Task<ActionResult<List<SubscriptionPlanDto>>> GetPlansByCategory(string category)
        {
            try
            {
                var plans = await _subscriptionPlanService.GetPlansByCategoryAsync(category);
                return Ok(plans);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("all")]
        public async Task<ActionResult<List<SubscriptionPlanDto>>> GetAllPlans()
        {
            var plans = await _subscriptionPlanService.GetAllPlansAsync();
            return Ok(plans);
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<SubscriptionPlanDto>> GetPlanById(string id)
        {
            try
            {
                var plan = await _subscriptionPlanService.GetPlansByIdAsync(id);
                return Ok(plan);
            }
            catch (Exception ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpPost("user-subscription")]
        public async Task<ActionResult<UserModel>> CreateUserSubscription([FromBody] CreateUserSubscriptionDto dto)
        {
            try
            {
                var callerUserId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(callerUserId) || callerUserId == "unknown")
                {
                    return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
                }

                if (!IsPlatformAdmin() && !string.Equals(callerUserId, dto.UserId, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }

                var user = await _subscriptionPlanService.CreateUserSubscriptionAsync(dto);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpPost("assign")]
        public async Task<ActionResult<bool>> AssignSubscriptionToUser([FromQuery] string userId, [FromQuery] string subscriptionPlanId)
        {
            try
            {
                var result = await _subscriptionPlanService.AssignSubscriptionToUserAsync(userId, subscriptionPlanId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [Authorize(Policy = MozaiksAuthDefaults.RequirePlatformAdminPolicy)]
        [HttpDelete("remove/{userId}")]
        public async Task<ActionResult<bool>> RemoveSubscriptionFromUser(string userId)
        {
            try
            {
                var result = await _subscriptionPlanService.RemoveSubscriptionFromUserAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
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
