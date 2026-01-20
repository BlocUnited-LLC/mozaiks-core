using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces;

public interface IAppMonetizationSpecRepository
{
    Task<AppMonetizationSpecVersion?> GetLatestAsync(string appId, CancellationToken cancellationToken);
    Task<AppMonetizationSpecVersion?> GetLatestCommittedAsync(string appId, CancellationToken cancellationToken);
    Task<int> GetNextVersionAsync(string appId, CancellationToken cancellationToken);
    Task InsertAsync(AppMonetizationSpecVersion version, CancellationToken cancellationToken);
    Task ArchiveCommittedAsync(string appId, DateTime archivedAtUtc, CancellationToken cancellationToken);
}
