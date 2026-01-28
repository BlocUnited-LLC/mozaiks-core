namespace Insights.API.Repository;

public interface ITeamMemberReadRepository
{
    Task<bool> IsMemberAsync(string appId, string userId, CancellationToken cancellationToken);
}

