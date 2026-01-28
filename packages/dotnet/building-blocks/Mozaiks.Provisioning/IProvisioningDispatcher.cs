using Hosting.API.Models;

namespace Hosting.API.Services.Provisioning;

public interface IProvisioningDispatcher
{
    Task DispatchAsync(ProvisioningJob job, CancellationToken cancellationToken);
}
