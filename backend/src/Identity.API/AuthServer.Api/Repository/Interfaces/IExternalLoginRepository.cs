using AuthServer.Api.Models;

namespace AuthServer.Api.Repository.Interfaces;

public interface IExternalLoginRepository
{
    Task<ExternalLoginModel?> FindByProviderSubjectAsync(string provider, string subject);
    Task<ExternalLoginModel?> FindByProviderUserIdAsync(string provider, string userId);
    Task CreateAsync(ExternalLoginModel login);
}
