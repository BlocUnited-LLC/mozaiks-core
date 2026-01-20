namespace AuthServer.Api.Services
{
    public interface IAppModuleProxyService
    {
        Task<AppModuleProxyResult> SendAsync(
            string appId,
            string path,
            HttpMethod method,
            byte[]? jsonBodyUtf8,
            string correlationId,
            CancellationToken cancellationToken);
    }
}

