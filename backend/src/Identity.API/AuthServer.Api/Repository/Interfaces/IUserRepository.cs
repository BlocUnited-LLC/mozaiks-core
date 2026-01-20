using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces
{
    public interface IUserRepository
    {
        Task<UserModel> GetUserByEmailAsync(string email);
        Task<bool> CreateUserAsync(UserModel user);
        Task<IEnumerable<UserModel>> GetAllUsersAsync();
        Task<IEnumerable<UserModel>> GetUsersByRoleAsync(int role);
        Task<UserModel> GetUserByIdAsync(string id);
        Task UpdateUserAsync(UserModel user);
        Task RevokeUserAsync(string id);
        Task SoftDeleteUserAsync(string id, string deletedByUserId);
        Task UpdateUserProfileAsync(string id, UserModel user);
        Task<List<UserModel>> GetUsersByIdsAsync(IEnumerable<string> userIds);

    }
}
