using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<UserProfileModel> _userCollection;

    public UserRepository(IMongoDatabase database)
    {
        _userCollection = database.GetCollection<UserProfileModel>("UserProfiles");
    }

    public async Task<IEnumerable<UserProfileModel>> GetAllUsers()
    {
        return await _userCollection.Find(_ => true).ToListAsync();
    }

    public async Task<UserProfileModel> GetUserById(string id)
    {
        return await _userCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
    }
    public async Task CreateUser(UserProfileModel user)
    {
        await _userCollection.InsertOneAsync(user);
    }

    public async Task UpdateUser(UserProfileModel user)
    {
        await _userCollection.ReplaceOneAsync(u => u.Id == user.Id, user);
    }

    public async Task DeleteUser(string id)
    {
        await _userCollection.DeleteOneAsync(u => u.Id == id);
    }
    public async Task RevokeUser(string id)
    {
        var filter = Builders<UserProfileModel>.Filter.Eq(u => u.Id, id);
        var update = Builders<UserProfileModel>.Update.Set(u => u.IsActive, false);

        await _userCollection.UpdateOneAsync(filter, update);
    }
}
