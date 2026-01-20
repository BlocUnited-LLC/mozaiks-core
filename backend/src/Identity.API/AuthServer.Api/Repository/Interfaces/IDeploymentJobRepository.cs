using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces;

public interface IDeploymentJobRepository
{
    Task<DeploymentJob> CreateAsync(DeploymentJob job, CancellationToken cancellationToken);

    Task<DeploymentJob?> GetByIdAsync(string jobId, CancellationToken cancellationToken);

    Task<DeploymentJob?> ClaimNextQueuedAsync(CancellationToken cancellationToken);

    Task<bool> UpdateAsync(DeploymentJob job, CancellationToken cancellationToken);

    Task<(IReadOnlyList<DeploymentJob> Jobs, long Total)> GetByAppIdAsync(
        string appId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
