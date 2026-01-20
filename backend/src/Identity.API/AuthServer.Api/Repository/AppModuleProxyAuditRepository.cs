using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Repository
{
    public sealed class AppModuleProxyAuditRepository : IAppModuleProxyAuditRepository
    {
        private readonly IMongoCollection<AppModuleProxyAuditEvent> _collection;

        public AppModuleProxyAuditRepository(IMongoDatabase database)
        {
            _collection = database.GetCollection<AppModuleProxyAuditEvent>(MongoCollectionNames.AppModuleProxyAuditEvents);
        }

        public async Task InsertAsync(AppModuleProxyAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            auditEvent.CreatedAt = auditEvent.CreatedAt == default ? DateTime.UtcNow : auditEvent.CreatedAt;
            auditEvent.UpdatedAt = DateTime.UtcNow;
            auditEvent.Timestamp = auditEvent.Timestamp == default ? DateTime.UtcNow : auditEvent.Timestamp;

            await _collection.InsertOneAsync(auditEvent, cancellationToken: cancellationToken);
        }
    }
}

