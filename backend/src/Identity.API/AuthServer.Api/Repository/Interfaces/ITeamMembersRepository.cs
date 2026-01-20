

using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces
{
    public interface ITeamMembersRepository
    {
        Task<TeamMembersModel> GetByIdAsync(string id);
        Task<IEnumerable<TeamMembersModel>> GetAllAsync(string appId);
        Task<TeamMembersModel?> GetByAppAndUserIdAsync(string appId, string userId);
        Task<TeamMembersModel> CreateAsync(TeamMembersModel teamsModel);
        Task<bool> UpdateAsync(string id, TeamMembersModel teamsModel);
        Task<bool> UpdateByAppAndUserIdAsync(string appId, string userId, string role, int mpAllocationBps, string? note);
        Task<TeamMembersModel> UpsertAsync(TeamMembersModel teamsModel);
        Task<bool> DeleteAsync(string id);
        Task<IEnumerable<string>> GetAllAppIdsByUserIdAsync(string userId);
    }
}
