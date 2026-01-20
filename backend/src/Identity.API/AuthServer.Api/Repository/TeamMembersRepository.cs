using MongoDB.Driver;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;

namespace AuthServer.Api.Repository
{
    public class TeamMembersRepository : ITeamMembersRepository
    {
        private readonly IMongoCollection<TeamMembersModel> _teams;
        public TeamMembersRepository(IMongoDatabase database)
        {
            _teams = database.GetCollection<TeamMembersModel>("Teams");
        }

        public async Task<TeamMembersModel> CreateAsync(TeamMembersModel team)
        {
            await _teams.InsertOneAsync(team);
            return team;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _teams.DeleteOneAsync(f => f.Id == id);
            return result.IsAcknowledged && result.DeletedCount > 0;
        }

        public async Task<IEnumerable<TeamMembersModel>> GetAllAsync(string appId)
        {
            return await _teams.Find(x => x.AppId == appId && x.MemberStatus == 1).ToListAsync();
        }

        public async Task<TeamMembersModel?> GetByAppAndUserIdAsync(string appId, string userId)
        {
            return await _teams.Find(x => x.AppId == appId && x.UserId == userId && x.MemberStatus == 1).FirstOrDefaultAsync();
        }

        public async Task<TeamMembersModel> GetByIdAsync(string id)
        {
            return await _teams.Find(x=>x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateAsync(string id, TeamMembersModel teamsModel)
        {
            var result = await _teams.ReplaceOneAsync(f => f.Id == id, teamsModel);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateByAppAndUserIdAsync(string appId, string userId, string role, int mpAllocationBps, string? note)
        {
            var filter = Builders<TeamMembersModel>.Filter.Eq(x => x.AppId, appId)
                & Builders<TeamMembersModel>.Filter.Eq(x => x.UserId, userId)
                & Builders<TeamMembersModel>.Filter.Eq(x => x.MemberStatus, 1);

            var update = Builders<TeamMembersModel>.Update
                .Set(x => x.Role, role)
                .Set(x => x.MpAllocationBps, mpAllocationBps)
                .Set(x => x.Note, note)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var result = await _teams.UpdateOneAsync(filter, update);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        public async Task<TeamMembersModel> UpsertAsync(TeamMembersModel teamsModel)
        {
            teamsModel.UpdatedAt = DateTime.UtcNow;
            if (teamsModel.CreatedAt == default)
            {
                teamsModel.CreatedAt = DateTime.UtcNow;
            }

            var filter = Builders<TeamMembersModel>.Filter.Eq(x => x.AppId, teamsModel.AppId)
                & Builders<TeamMembersModel>.Filter.Eq(x => x.UserId, teamsModel.UserId);

            var update = Builders<TeamMembersModel>.Update
                .SetOnInsert(x => x.AppId, teamsModel.AppId)
                .SetOnInsert(x => x.UserId, teamsModel.UserId)
                .Set(x => x.InvitedByUserId, teamsModel.InvitedByUserId)
                .Set(x => x.Role, teamsModel.Role)
                .Set(x => x.MpAllocationBps, teamsModel.MpAllocationBps)
                .Set(x => x.Note, teamsModel.Note)
                .Set(x => x.MemberStatus, teamsModel.MemberStatus)
                .Set(x => x.UpdatedAt, teamsModel.UpdatedAt)
                .SetOnInsert(x => x.Id, teamsModel.Id)
                .SetOnInsert(x => x.CreatedAt, teamsModel.CreatedAt);

            await _teams.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
            return teamsModel;
        }
        public async Task<IEnumerable<string>> GetAllAppIdsByUserIdAsync(string userId)
        {
            var appIds = await _teams.Find(x => x.UserId == userId && x.MemberStatus == 1)
                                     .Project(x => x.AppId)
                                     .ToListAsync();
            return appIds;
        }


    }
}
