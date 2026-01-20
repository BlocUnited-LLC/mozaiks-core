using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using MongoDB.Driver;

namespace AuthServer.Api.Repository;

public sealed class ExternalLoginRepository : IExternalLoginRepository
{
    private readonly IMongoCollection<ExternalLoginModel> _collection;

    public ExternalLoginRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ExternalLoginModel>("ExternalLogins");
    }

    public async Task<ExternalLoginModel?> FindByProviderSubjectAsync(string provider, string subject)
        => await _collection.Find(x => x.Provider == provider && x.Subject == subject).FirstOrDefaultAsync();

    public async Task<ExternalLoginModel?> FindByProviderUserIdAsync(string provider, string userId)
        => await _collection.Find(x => x.Provider == provider && x.UserId == userId).FirstOrDefaultAsync();

    public async Task CreateAsync(ExternalLoginModel login)
        => await _collection.InsertOneAsync(login);
}
