using System.Collections.Generic;
using System.Threading.Tasks;

public interface IUserRepository
{
    Task<IEnumerable<UserProfileModel>> GetAllUsers();
    Task<UserProfileModel> GetUserById(string id);
    Task CreateUser(UserProfileModel user);
    Task UpdateUser(UserProfileModel user);
    Task DeleteUser(string id);
    Task RevokeUser(string id);
}
