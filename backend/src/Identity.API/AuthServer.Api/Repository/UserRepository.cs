using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly IMongoCollection<UserModel> _userCollection;

        public UserRepository(IMongoDatabase database)
        {
            _userCollection = database.GetCollection<UserModel>("AppUsers");
        }

        public async Task<UserModel> GetUserByEmailAsync(string email)
        {
            return await _userCollection.Find(u => u.Email == email).SingleOrDefaultAsync();
        }
        public async Task<List<UserModel>> GetUsersByIdsAsync(IEnumerable<string> userIds)
        {
            return await _userCollection
                .Find(u => userIds.Contains(u.Id))
                .ToListAsync();
        }


        public async Task<bool> CreateUserAsync(UserModel user)
        {
            try
            {
                await _userCollection.InsertOneAsync(user);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<UserModel> GetUserByIdAsync(string id)
        {
            return await _userCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<UserModel>> GetUsersByRoleAsync(int role)
        {
            var userRoleEnum = (UserRole)role;
            return await _userCollection.Find(x => x.UserRoles.Contains(userRoleEnum)).ToListAsync();
        }

        public async Task<IEnumerable<UserModel>> GetAllUsersAsync()
        {
            return await _userCollection.Find(_ => true).ToListAsync();
        }

        
        public async Task DeleteUserAsync(string id)
        {
            await _userCollection.DeleteOneAsync(u => u.Id == id);
        }
        public async Task RevokeUserAsync(string id)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, id);
            var update = Builders<UserModel>.Update
                .Set(u => u.IsActive, false)
                .Set(u => u.UpdatedAt, DateTime.UtcNow);

            await _userCollection.UpdateOneAsync(filter, update);
        }

        public async Task SoftDeleteUserAsync(string id, string deletedByUserId)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, id);
            var now = DateTime.UtcNow;
            var update = Builders<UserModel>.Update
                .Set(u => u.IsActive, false)
                .Set(u => u.DeletedAt, now)
                .Set(u => u.DeletedByUserId, deletedByUserId)
                .Set(u => u.UpdatedAt, now);

            await _userCollection.UpdateOneAsync(filter, update);
        }

        public async Task UpdateUserAsync(UserModel user)
        {
            await _userCollection.ReplaceOneAsync(u => u.Id == user.Id, user);
        }

        public async Task UpdateUserProfileAsync(string id, UserModel user)
        {
            var filter = Builders<UserModel>.Filter.Eq(u => u.Id, id);
            var update = Builders<UserModel>.Update.Set(u => u.FirstName, user.FirstName)
                .Set(u => u.LastName, user.LastName)
                .Set(u => u.Email, user.Email)
                .Set(u => u.UserPhoto, user.UserPhoto)
                .Set(u => u.Phone, user.Phone)
                .Set(u => u.Bio, user.Bio)
                .Set(u=> u.UpdatedAt, DateTime.UtcNow);

            await _userCollection.UpdateOneAsync(filter, update);
        }



    }
}
