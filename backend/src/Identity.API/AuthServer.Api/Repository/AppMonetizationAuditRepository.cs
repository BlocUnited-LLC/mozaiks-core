using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Repository;

public sealed class AppMonetizationAuditRepository : IAppMonetizationAuditRepository
{
    private readonly IMongoCollection<AppMonetizationAuditEvent> _collection;

    public AppMonetizationAuditRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AppMonetizationAuditEvent>(MongoCollectionNames.AppMonetizationAuditEvents);
    }

    public async Task InsertAsync(AppMonetizationAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        auditEvent.CreatedAt = DateTime.UtcNow;
        auditEvent.UpdatedAt = auditEvent.CreatedAt;
        if (auditEvent.OccurredAtUtc == default)
        {
            auditEvent.OccurredAtUtc = auditEvent.CreatedAt;
        }

        await _collection.InsertOneAsync(auditEvent, cancellationToken: cancellationToken);
    }
}
