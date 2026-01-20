using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using MongoDB.Driver;

namespace AuthServer.Api.Repository
{
    public class SubscriptionPlanRepository: ISubscriptionPlanRepository
    {
        private readonly IMongoCollection<SubscriptionPlanModel> _subscriptionPlans;
        private readonly IMongoCollection<UserModel> _users;
        public SubscriptionPlanRepository(IMongoDatabase database)
        {
            _subscriptionPlans = database.GetCollection<SubscriptionPlanModel>("SubscriptionPlans");
            _users = database.GetCollection<UserModel>("AppUsers");
        }

        public async Task<bool> AddSubscriptionPlanAsync(SubscriptionPlanModel plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan), "Subscription plan cannot be null.");

            await _subscriptionPlans.InsertOneAsync(plan);
            return true;
        }

        public async Task<bool> UpdateSubscriptionPlanAsync(string id, SubscriptionPlanModel updatedPlan)
        {
            if (updatedPlan == null)
                throw new ArgumentNullException(nameof(updatedPlan), "Updated subscription plan cannot be null.");

            var filter = Builders<SubscriptionPlanModel>.Filter.Eq(p => p.Id, id);
            var update = Builders<SubscriptionPlanModel>.Update
                .Set(p => p.Name, updatedPlan.Name)
                .Set(p => p.Price, updatedPlan.Price)
                .Set(p => p.IdealFor, updatedPlan.IdealFor)
                .Set(p => p.MonthlyTokens, updatedPlan.MonthlyTokens)
                .Set(p => p.HostingLevel, updatedPlan.HostingLevel)
                .Set(p => p.Traffic, updatedPlan.Traffic)
                .Set(p => p.SLA, updatedPlan.SLA)
                .Set(p => p.DiscountNote, updatedPlan.DiscountNote)
                .Set(p => p.MaxDomains, updatedPlan.MaxDomains)
                .Set(p => p.EmailEnabled, updatedPlan.EmailEnabled)
                .Set(p => p.MaxEmailDomains, updatedPlan.MaxEmailDomains)
                .Set(p => p.TokenOverageAllowed, updatedPlan.TokenOverageAllowed)
                .Set(p => p.SubscriptionCategory, updatedPlan.SubscriptionCategory)
                .Set(p => p.UpdatedAt, DateTime.UtcNow);

            var result = await _subscriptionPlans.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        public async Task<List<SubscriptionPlanModel>> GetPlansByCategoryAsync(SubscriptionCategory category)
        {
            var filter = Builders<SubscriptionPlanModel>.Filter.Eq(p => p.SubscriptionCategory, category);
            return await _subscriptionPlans.Find(filter).ToListAsync();
        }

        public async Task<SubscriptionPlanModel> GetPlanByIdAsync(string id)
        {
            var filter = Builders<SubscriptionPlanModel>.Filter.Eq(p => p.Id, id);
            return await _subscriptionPlans.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<List<SubscriptionPlanModel>> GetAllPlansAsync()
        {
            return await _subscriptionPlans.Find(_ => true).ToListAsync();
        }


        public async Task<bool> AssignSubscriptionToUserAsync(string userId, SubscriptionPlanModel plan)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, userId);
            var update = Builders<UserModel>.Update.Set(u => u.SubscriptionPlan, plan);
            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveSubscriptionFromUserAsync(string userId)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, userId);
            var update = Builders<UserModel>.Update.Unset(u => u.SubscriptionPlan);
            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }


    }
}
