using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces;

public interface IAppMonetizationAuditRepository
{
    Task InsertAsync(AppMonetizationAuditEvent auditEvent, CancellationToken cancellationToken);
}
