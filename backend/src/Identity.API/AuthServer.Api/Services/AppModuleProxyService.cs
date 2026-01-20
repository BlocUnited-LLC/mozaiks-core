using System.Net.Http.Headers;
using AuthServer.Api.Repository.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AuthServer.Api.Services
{
    public sealed class AppModuleProxyService : IAppModuleProxyService
    {
        private const string CircuitCachePrefix = "app_module_proxy_circuit:";
        private const int FailureThreshold = 3;
        private static readonly TimeSpan FailureWindow = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan CircuitCacheTtl = TimeSpan.FromMinutes(10);

        private const int MaxResponseBytes = 1024 * 1024; // 1 MB

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAppAdminSurfaceRepository _adminSurfaces;
        private readonly IDataProtector _protector;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AppModuleProxyService> _logger;

        public AppModuleProxyService(
            IHttpClientFactory httpClientFactory,
            IAppAdminSurfaceRepository adminSurfaces,
            IDataProtectionProvider dataProtectionProvider,
            IMemoryCache cache,
            ILogger<AppModuleProxyService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _adminSurfaces = adminSurfaces;
            _protector = dataProtectionProvider.CreateProtector("Mozaiks.AppAdminSurface.AdminKey.v1");
            _cache = cache;
            _logger = logger;
        }

        public async Task<AppModuleProxyResult> SendAsync(
            string appId,
            string path,
            HttpMethod method,
            byte[]? jsonBodyUtf8,
            string correlationId,
            CancellationToken cancellationToken)
        {
            var config = await _adminSurfaces.GetByAppIdAsync(appId, cancellationToken);
            if (config is null ||
                string.IsNullOrWhiteSpace(config.BaseUrl) ||
                string.IsNullOrWhiteSpace(config.AdminKeyProtected))
            {
                return AppModuleProxyResult.Fail(
                    AppModuleProxyFailureKind.NotConfigured,
                    "admin_surface_not_configured",
                    "This app is not configured for module management yet.");
            }

            if (!Uri.TryCreate(config.BaseUrl.Trim(), UriKind.Absolute, out var baseUri))
            {
                return AppModuleProxyResult.Fail(
                    AppModuleProxyFailureKind.InvalidConfiguration,
                    "invalid_admin_surface_configuration",
                    "This app has an invalid admin base URL configured.");
            }

            string adminKey;
            try
            {
                adminKey = _protector.Unprotect(config.AdminKeyProtected);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unprotect app admin key for app {AppId}", appId);
                return AppModuleProxyResult.Fail(
                    AppModuleProxyFailureKind.InvalidConfiguration,
                    "invalid_admin_surface_configuration",
                    "This app has an invalid admin key configuration.");
            }

            if (IsCircuitOpen(appId, out var openUntilUtc))
            {
                return AppModuleProxyResult.Fail(
                    AppModuleProxyFailureKind.CircuitOpen,
                    "app_runtime_unavailable",
                    "The app runtime is temporarily unavailable. Please try again shortly.");
            }

            var targetUri = new Uri(new Uri(baseUri.ToString().TrimEnd('/') + "/"), path.TrimStart('/'));
            using var request = new HttpRequestMessage(method, targetUri);
            request.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);
            request.Headers.TryAddWithoutValidation("X-Mozaiks-App-Id", appId);
            request.Headers.TryAddWithoutValidation("X-Mozaiks-App-Admin-Key", adminKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (jsonBodyUtf8 is not null)
            {
                request.Content = new ByteArrayContent(jsonBodyUtf8);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            var client = _httpClientFactory.CreateClient("AppAdminProxy");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                RecordFailure(appId);
                _logger.LogWarning(ex, "App module proxy timeout for app {AppId} path {Path}", appId, path);
                return AppModuleProxyResult.Fail(
                    AppModuleProxyFailureKind.Timeout,
                    "app_runtime_timeout",
                    "The app runtime took too long to respond. Please try again.");
            }
            catch (HttpRequestException ex)
            {
                RecordFailure(appId);
                _logger.LogWarning(ex, "App module proxy network error for app {AppId} path {Path}", appId, path);
                return AppModuleProxyResult.Fail(
                    AppModuleProxyFailureKind.NetworkError,
                    "app_runtime_unreachable",
                    "The app runtime could not be reached. Please try again later.");
            }
            catch (Exception ex)
            {
                RecordFailure(appId);
                _logger.LogError(ex, "App module proxy unexpected error for app {AppId} path {Path}", appId, path);
                return AppModuleProxyResult.Fail(
                    AppModuleProxyFailureKind.Unknown,
                    "module_proxy_failed",
                    "Module request failed. Please try again later.");
            }

            using (response)
            {
                byte[] body;
                try
                {
                    body = await ReadWithLimitAsync(response.Content, MaxResponseBytes, cancellationToken);
                }
                catch (Exception ex)
                {
                    RecordFailure(appId);
                    _logger.LogWarning(ex, "App module proxy response too large for app {AppId} path {Path}", appId, path);
                    return AppModuleProxyResult.Fail(
                        AppModuleProxyFailureKind.ResponseTooLarge,
                        "app_runtime_response_too_large",
                        "The app runtime returned a response that is too large.");
                }

                var contentType = response.Content.Headers.ContentType?.ToString();
                var statusCode = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    RecordSuccess(appId);
                    return AppModuleProxyResult.Success(statusCode, contentType, body);
                }

                if (statusCode >= 500)
                {
                    RecordFailure(appId);
                    return AppModuleProxyResult.Fail(
                        AppModuleProxyFailureKind.UpstreamError,
                        "app_runtime_error",
                        "The app runtime returned an error.",
                        upstreamStatusCode: statusCode);
                }

                // For 4xx, return as upstream error but don't trip the circuit.
                return new AppModuleProxyResult
                {
                    Succeeded = false,
                    FailureKind = AppModuleProxyFailureKind.UpstreamError,
                    ErrorCode = "module_request_rejected",
                    ErrorMessage = "The app runtime rejected the request.",
                    UpstreamStatusCode = statusCode,
                    ContentType = contentType,
                    Body = body
                };
            }
        }

        private sealed record CircuitState(int FailureCount, DateTime WindowStartUtc, DateTime? OpenUntilUtc);

        private bool IsCircuitOpen(string appId, out DateTime? openUntilUtc)
        {
            var key = CircuitCachePrefix + appId;
            if (_cache.TryGetValue<CircuitState>(key, out var state) &&
                state is not null &&
                state.OpenUntilUtc is not null &&
                state.OpenUntilUtc.Value > DateTime.UtcNow)
            {
                openUntilUtc = state.OpenUntilUtc;
                return true;
            }

            openUntilUtc = null;
            return false;
        }

        private void RecordSuccess(string appId)
        {
            _cache.Remove(CircuitCachePrefix + appId);
        }

        private void RecordFailure(string appId)
        {
            var key = CircuitCachePrefix + appId;
            var now = DateTime.UtcNow;

            if (!_cache.TryGetValue<CircuitState>(key, out var state) || state is null || now - state.WindowStartUtc > FailureWindow)
            {
                state = new CircuitState(0, now, null);
            }

            var failures = state.FailureCount + 1;
            var openUntil = failures >= FailureThreshold ? now.Add(BreakDuration) : state.OpenUntilUtc;

            var updated = new CircuitState(failures, state.WindowStartUtc, openUntil);
            _cache.Set(key, updated, CircuitCacheTtl);
        }

        private static async Task<byte[]> ReadWithLimitAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
        {
            await using var stream = await content.ReadAsStreamAsync(cancellationToken);
            using var ms = new MemoryStream();

            var buffer = new byte[16 * 1024];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                ms.Write(buffer, 0, read);
                if (ms.Length > maxBytes)
                {
                    throw new InvalidOperationException("Response exceeded max allowed bytes");
                }
            }

            return ms.ToArray();
        }
    }
}
