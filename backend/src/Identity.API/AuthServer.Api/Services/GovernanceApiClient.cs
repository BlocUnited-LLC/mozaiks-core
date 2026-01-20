using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthServer.Api.DTOs;

namespace AuthServer.Api.Services
{
    public class GovernanceApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IServiceToServiceTokenProvider _tokenProvider;

        public GovernanceApiClient(HttpClient httpClient, IServiceToServiceTokenProvider tokenProvider)
        {
            _httpClient = httpClient;
            _tokenProvider = tokenProvider;
        }

        public async Task<List<InvestorPositionDashboardDto>> GetPositionsForUserAsync(
            string userId,
            string correlationId,
            CancellationToken cancellationToken)
        {
            if (_httpClient.BaseAddress is null)
            {
                throw new InvalidOperationException("GovernanceService base URL is not configured (MicroServiceEndpoints:GovernanceService).");
            }

            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/internal/investors/{Uri.EscapeDataString(userId)}/positions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<InvestorPositionDashboardDto>>(cancellationToken: cancellationToken)
                ?? new List<InvestorPositionDashboardDto>();
        }

        public async Task<List<InvestmentListItemDto>> GetInvestmentsForUserAsync(
            string userId,
            string correlationId,
            CancellationToken cancellationToken)
        {
            if (_httpClient.BaseAddress is null)
            {
                throw new InvalidOperationException("GovernanceService base URL is not configured (MicroServiceEndpoints:GovernanceService).");
            }

            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/internal/investors/{Uri.EscapeDataString(userId)}/investments");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("x-correlation-id", correlationId);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<InvestmentListItemDto>>(cancellationToken: cancellationToken)
                ?? new List<InvestmentListItemDto>();
        }
    }
}
