using AuthServer.Api.DTOs;
using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces
{
    public interface ISubscriptionPlanRepository
    {
        Task<bool> AddSubscriptionPlanAsync(SubscriptionPlanModel plan);
        Task<bool> UpdateSubscriptionPlanAsync(string id, SubscriptionPlanModel updatedPlan);

        Task<List<SubscriptionPlanModel>> GetPlansByCategoryAsync(SubscriptionCategory category);
        Task<SubscriptionPlanModel> GetPlanByIdAsync(string id);
        Task<List<SubscriptionPlanModel>> GetAllPlansAsync();
        Task<bool> AssignSubscriptionToUserAsync(string userId, SubscriptionPlanModel plan);
        Task<bool> RemoveSubscriptionFromUserAsync(string userId);
        
    }
}
