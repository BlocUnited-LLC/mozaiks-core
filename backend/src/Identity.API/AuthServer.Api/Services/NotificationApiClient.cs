using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AuthServer.Api.Services;

public sealed class NotificationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IServiceToServiceTokenProvider _tokenProvider;
    private readonly ILogger<NotificationApiClient> _logger;

    public NotificationApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IServiceToServiceTokenProvider tokenProvider,
        ILogger<NotificationApiClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;

        var baseUrl = (configuration.GetValue<string>("MicroServiceEndpoints:NotificationService") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        }
    }

    public async Task SendAppBuildEventAsync(
        string appId,
        string recipientUserId,
        string? appName,
        string buildId,
        string status,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            appId,
            recipientUserId,
            appName,
            buildId,
            status
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "PushNotification/SendAppBuildEvent")
        {
            Content = JsonContent.Create(payload)
        };

        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Notification send failed status={StatusCode} appId={AppId} userId={UserId} body={Body}",
                (int)response.StatusCode,
                appId,
                recipientUserId,
                body);
        }
    }
}
