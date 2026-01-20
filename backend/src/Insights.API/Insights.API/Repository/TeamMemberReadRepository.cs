using Insights.API.Models;
using MongoDB.Driver;

namespace Insights.API.Repository;

public sealed class TeamMemberReadRepository : ITeamMemberReadRepository
{
    private readonly IMongoCollection<TeamMemberReadModel> _collection;

    public TeamMemberReadRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TeamMemberReadModel>("Teams");
    }

    public async Task<bool> IsMemberAsync(string appId, string userId, CancellationToken cancellationToken)
    {
        var filter = Builders<TeamMemberReadModel>.Filter.Eq(x => x.AppId, appId)
                     & Builders<TeamMemberReadModel>.Filter.Eq(x => x.UserId, userId)
                     & Builders<TeamMemberReadModel>.Filter.Eq(x => x.MemberStatus, 1);

        var member = await _collection.Find(filter)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);

        return member is not null;
    }
}

