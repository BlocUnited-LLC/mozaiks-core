using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces
{
    public interface IAppModuleProxyAuditRepository
    {
        Task InsertAsync(AppModuleProxyAuditEvent auditEvent, CancellationToken cancellationToken);
    }
}

