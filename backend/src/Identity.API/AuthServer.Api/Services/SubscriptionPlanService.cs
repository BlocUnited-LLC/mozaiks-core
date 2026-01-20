using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;

namespace AuthServer.Api.Services
{
    public class SubscriptionPlanService
    {
        private readonly ISubscriptionPlanRepository _repository;
        private readonly IUserRepository _userRepository;

        public SubscriptionPlanService(ISubscriptionPlanRepository repository, IUserRepository userRepository)
        {
            _repository = repository;
            _userRepository = userRepository;
        }

        public async Task<bool> AddSubscriptionPlanAsync(SubscriptionPlanModel plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan), "Subscription plan cannot be null.");

            return await _repository.AddSubscriptionPlanAsync(plan);
        }
        public async Task<bool> UpdateSubscriptionPlanAsync(string id, SubscriptionPlanModel updatedPlan)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Plan ID cannot be null or empty.", nameof(id));

            if (updatedPlan == null)
                throw new ArgumentNullException(nameof(updatedPlan), "Updated subscription plan cannot be null.");

            return await _repository.UpdateSubscriptionPlanAsync(id, updatedPlan);
        }


        public async Task<List<SubscriptionPlanDto>> GetPlansByCategoryAsync(string category)
        {
            if (!Enum.TryParse(category, true, out SubscriptionCategory parsedCategory))
                throw new ArgumentException("Invalid category");

            var plans = await _repository.GetPlansByCategoryAsync(parsedCategory);

            return plans.Select(p => new SubscriptionPlanDto
            {
                Category = p.SubscriptionCategory.ToString(),
                Name = p.Name,
                Price = p.Price,
                IdealFor = p.IdealFor,
                MonthlyTokens = p.MonthlyTokens,
                HostingLevel = p.HostingLevel,
                Traffic = p.Traffic,
                SLA = p.SLA,
                DiscountNote = p.DiscountNote,
                MaxDomains = p.MaxDomains,
                EmailEnabled = p.EmailEnabled,
                MaxEmailDomains = p.MaxEmailDomains,
                TokenOverageAllowed = p.TokenOverageAllowed
            }).ToList();
        }
        public async Task<List<SubscriptionPlanDto>> GetAllPlansAsync()
        {
            var plans = await _repository.GetAllPlansAsync();

            return plans.Select(p => new SubscriptionPlanDto
            {
                Category = p.SubscriptionCategory.ToString(),
                Name = p.Name,
                Price = p.Price,
                IdealFor = p.IdealFor,
                MonthlyTokens = p.MonthlyTokens,
                HostingLevel = p.HostingLevel,
                Traffic = p.Traffic,
                SLA = p.SLA,
                DiscountNote = p.DiscountNote,
                MaxDomains = p.MaxDomains,
                EmailEnabled = p.EmailEnabled,
                MaxEmailDomains = p.MaxEmailDomains,
                TokenOverageAllowed = p.TokenOverageAllowed,
                Id = p.Id
            }).ToList();
        }
        public async Task<SubscriptionPlanDto> GetPlansByIdAsync(string id)
        {
            var plan = await _repository.GetPlanByIdAsync(id);
            if (plan == null)
                throw new Exception("Subscription plan not found");

            return 
                new SubscriptionPlanDto
                {
                    Category = plan.SubscriptionCategory.ToString(),
                    Name = plan.Name ?? string.Empty, // Handle possible null reference
                    Price = plan.Price,
                    IdealFor = plan.IdealFor ?? string.Empty, // Handle possible null reference
                    MonthlyTokens = plan.MonthlyTokens,
                    HostingLevel = plan.HostingLevel ?? string.Empty, // Handle possible null reference
                    Traffic = plan.Traffic ?? string.Empty, // Handle possible null reference
                    SLA = plan.SLA ?? string.Empty, // Handle possible null reference
                    DiscountNote = plan.DiscountNote ?? string.Empty, // Handle possible null reference
                    MaxDomains = plan.MaxDomains,
                    EmailEnabled = plan.EmailEnabled,
                    MaxEmailDomains = plan.MaxEmailDomains,
                    TokenOverageAllowed = plan.TokenOverageAllowed
                
            };
        }

        public async Task<UserModel> CreateUserSubscriptionAsync(CreateUserSubscriptionDto dto)
        {
            var plan = await _repository.GetPlanByIdAsync(dto.SubscriptionPlanId);
            if (plan == null) throw new Exception("Subscription plan not found");
             
            await _repository.AssignSubscriptionToUserAsync(dto.UserId, plan);

            var user = await _userRepository.GetUserByIdAsync(dto.UserId);
            return user;
        }
        public async Task<bool> AssignSubscriptionToUserAsync(string userId, string subscriptionPlanId)
        {
            // Validate inputs
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

            if (string.IsNullOrEmpty(subscriptionPlanId))
                throw new ArgumentException("Subscription Plan ID cannot be null or empty.", nameof(subscriptionPlanId));

            // Retrieve the subscription plan
            var plan = await _repository.GetPlanByIdAsync(subscriptionPlanId);
            if (plan == null)
                throw new Exception("Subscription plan not found.");

            // Call the repository method to assign the subscription
            var result = await _repository.AssignSubscriptionToUserAsync(userId, plan);

            return result;
        }
        public async Task<bool> RemoveSubscriptionFromUserAsync(string userId)
        {
            // Validate input
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));

            // Call the repository method to remove the subscription
            var result = await _repository.RemoveSubscriptionFromUserAsync(userId);

            return result;
        }


    }
}
