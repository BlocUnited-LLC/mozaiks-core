using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Repository;

public sealed class DeploymentJobRepository : IDeploymentJobRepository
{
    private readonly IMongoCollection<DeploymentJob> _jobs;

    public DeploymentJobRepository(IMongoDatabase database)
    {
        _jobs = database.GetCollection<DeploymentJob>(MongoCollectionNames.DeploymentJobs);
    }

    public async Task<DeploymentJob> CreateAsync(DeploymentJob job, CancellationToken cancellationToken)
    {
        await _jobs.InsertOneAsync(job, cancellationToken: cancellationToken);
        return job;
    }

    public async Task<DeploymentJob?> GetByIdAsync(string jobId, CancellationToken cancellationToken)
    {
        return await _jobs.Find(x => x.Id == jobId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DeploymentJob?> ClaimNextQueuedAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var filter = Builders<DeploymentJob>.Filter.Eq(x => x.Status, DeploymentStatus.Queued);
        var update = Builders<DeploymentJob>.Update
            .Set(x => x.Status, DeploymentStatus.Running)
            .Set(x => x.StartedAt, now)
            .Set(x => x.UpdatedAt, now)
            .Inc(x => x.Attempt, 1);

        return await _jobs.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<DeploymentJob>
            {
                Sort = Builders<DeploymentJob>.Sort.Ascending(x => x.CreatedAt),
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(DeploymentJob job, CancellationToken cancellationToken)
    {
        job.UpdatedAt = DateTime.UtcNow;
        var result = await _jobs.ReplaceOneAsync(x => x.Id == job.Id, job, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.ModifiedCount > 0;
    }

    public async Task<(IReadOnlyList<DeploymentJob> Jobs, long Total)> GetByAppIdAsync(
        string appId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page <= 0 ? 1 : page;
        var resolvedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
        var skip = (resolvedPage - 1) * resolvedPageSize;

        var filter = Builders<DeploymentJob>.Filter.Eq(x => x.AppId, appId);

        var totalTask = _jobs.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var jobsTask = _jobs.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Limit(resolvedPageSize)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(totalTask, jobsTask);

        return (jobsTask.Result, totalTask.Result);
    }
}
