using System.Net;
using System.Text.Json;
using AuthServer.Api.Controllers;
using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuthServer.Api.Tests;

public sealed class InternalEntitlementsControllerTests
{
    [Fact]
    public async Task Get_ReturnsEntitlementsPayload()
    {
        var appPeriodEnd = DateTime.UtcNow.AddDays(14);
        var platformPeriodEnd = DateTime.UtcNow.AddDays(30);

        var handler = new StubMozaiksPayHandler(new Dictionary<string, object?>
        {
            ["platform"] = new
            {
                isActive = true,
                planId = "plan_platform_build",
                expiresAtUtc = (DateTime?)platformPeriodEnd,
                subscriptionId = "sub_platform",
                currentPeriodEndUtc = (DateTime?)platformPeriodEnd,
                features = Array.Empty<string>()
            },
            ["app"] = new
            {
                isActive = true,
                planId = "plan_app_host",
                expiresAtUtc = (DateTime?)appPeriodEnd,
                subscriptionId = "sub_app",
                currentPeriodEndUtc = (DateTime?)appPeriodEnd,
                features = Array.Empty<string>()
            }
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentApi:BaseUrl"] = "http://localhost"
            })
            .Build();

        var httpClient = new HttpClient(handler);
        var pay = new MozaiksPayClient(
            httpClient,
            config,
            new StubServiceToServiceTokenProvider(),
            NullLogger<MozaiksPayClient>.Instance);

        var plans = new StubPlansRepository(new Dictionary<string, SubscriptionPlanModel>
        {
            ["plan_app_host"] = new SubscriptionPlanModel
            {
                HostingLevel = "scale",
                MaxDomains = 3,
                EmailEnabled = true,
                MaxEmailDomains = 2
            },
            ["plan_platform_build"] = new SubscriptionPlanModel
            {
                MonthlyTokens = 100
            }
        });

        var entitlements = new AppEntitlementsService(pay, plans, new StubMonetizationSpecRepository(), new StubAppsRepository(), NullLogger<AppEntitlementsService>.Instance);

        var controller = new InternalEntitlementsController(entitlements)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Get("user-1", "app-1", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<EntitlementsResponse>(ok.Value);

        Assert.True(payload.AllowHosting);
        Assert.True(payload.AllowExportRepo);
        Assert.True(payload.AllowWorkerMode);
        Assert.Equal("scale", payload.HostingLevel);
        Assert.Equal("plan_app_host", payload.AppPlanId);
        Assert.Equal("plan_platform_build", payload.PlatformPlanId);
        Assert.Equal(3, payload.Domains.Max);
        Assert.Null(payload.Domains.Used);
        Assert.True(payload.Email.Enabled);
        Assert.Equal(2, payload.Email.MaxDomains);
        Assert.Equal(100, payload.Tokens.MonthlyLimit);
        Assert.Equal("scale", payload.Hosting.Tier);
        Assert.Null(payload.Fees.TransactionFeeBps);
        Assert.Equal("sub_app", payload.Billing.StripeSubscriptionId);
        Assert.Equal(appPeriodEnd, payload.Billing.CurrentPeriodEndUtc);
    }

    private sealed class StubPlansRepository : ISubscriptionPlanRepository
    {
        private readonly Dictionary<string, SubscriptionPlanModel> _plans;

        public StubPlansRepository(Dictionary<string, SubscriptionPlanModel> plans)
        {
            _plans = plans;
        }

        public Task<bool> AddSubscriptionPlanAsync(SubscriptionPlanModel plan) => throw new NotImplementedException();
        public Task<bool> UpdateSubscriptionPlanAsync(string id, SubscriptionPlanModel updatedPlan) => throw new NotImplementedException();
        public Task<List<SubscriptionPlanModel>> GetPlansByCategoryAsync(SubscriptionCategory category) => throw new NotImplementedException();
        public Task<List<SubscriptionPlanModel>> GetAllPlansAsync() => throw new NotImplementedException();
        public Task<bool> AssignSubscriptionToUserAsync(string userId, SubscriptionPlanModel plan) => throw new NotImplementedException();
        public Task<bool> RemoveSubscriptionFromUserAsync(string userId) => throw new NotImplementedException();

        public Task<SubscriptionPlanModel> GetPlanByIdAsync(string id)
        {
            return Task.FromResult(_plans.TryGetValue(id, out var plan) ? plan : new SubscriptionPlanModel());
        }
    }

    private sealed class StubServiceToServiceTokenProvider : IServiceToServiceTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("test-token");
    }

    private sealed class StubMonetizationSpecRepository : IAppMonetizationSpecRepository
    {
        public Task<AppMonetizationSpecVersion?> GetLatestAsync(string appId, CancellationToken cancellationToken) => Task.FromResult<AppMonetizationSpecVersion?>(null);
        public Task<AppMonetizationSpecVersion?> GetLatestCommittedAsync(string appId, CancellationToken cancellationToken) => Task.FromResult<AppMonetizationSpecVersion?>(null);
        public Task<int> GetNextVersionAsync(string appId, CancellationToken cancellationToken) => Task.FromResult(1);
        public Task InsertAsync(AppMonetizationSpecVersion version, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ArchiveCommittedAsync(string appId, DateTime archivedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubMozaiksPayHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, object?> _responses;

        public StubMozaiksPayHandler(Dictionary<string, object?> responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? new Uri("http://localhost/");
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            var scope = query.TryGetValue("scope", out var scopeValues) ? scopeValues.ToString() : string.Empty;

            var body = _responses.TryGetValue(scope, out var dto) ? dto : new { isActive = false };
            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubAppsRepository : IMozaiksAppRepository
    {
        public Task<List<MozaiksAppModel>> GetAllAppsAsync() => Task.FromResult(new List<MozaiksAppModel>());
        public Task<MozaiksAppModel?> GetByIdAsync(string id) => Task.FromResult<MozaiksAppModel?>(null);
        public Task<List<MozaiksAppModel>> GetByOwnerUserIdAsync(string userId) => Task.FromResult(new List<MozaiksAppModel>());
        public Task<List<MozaiksAppModel>> GetByIdsAsync(IEnumerable<string> appIds) => Task.FromResult(new List<MozaiksAppModel>());
        public Task<List<MozaiksAppModel>> GetPublicAsync() => Task.FromResult(new List<MozaiksAppModel>());
        public Task CreateAsync(MozaiksAppModel app) => Task.CompletedTask;
        public Task UpdateAsync(MozaiksAppModel app) => Task.CompletedTask;
        public Task DeleteAsync(string id) => Task.CompletedTask;
        public Task AddMembersToTeamAsync(string appId, string[] memberIds) => Task.CompletedTask;
        public Task<bool> PatchAppConfigAsync(string appId, AppConfigPatchRequest request) => Task.FromResult(true);
        public Task<bool> SetPublishStatusAsync(string appId, bool publish) => Task.FromResult(true);
        public Task<bool> SetFeatureFlagAsync(string appId, string flag, bool enabled) => Task.FromResult(true);
        public Task<bool> TryGenerateApiKeyAsync(string appId, string apiKeyHash, string apiKeyPrefix, DateTime createdAtUtc) => Task.FromResult(true);
        public Task<bool> RegenerateApiKeyAsync(string appId, string apiKeyHash, string apiKeyPrefix, DateTime createdAtUtc) => Task.FromResult(true);
        public Task<bool> UpdateApiKeyLastUsedAtAsync(string appId, DateTime lastUsedAtUtc) => Task.FromResult(true);
        public Task<bool> SetGitHubDeploymentAsync(string appId, string repoUrl, string repoFullName, DateTime deployedAtUtc) => Task.FromResult(true);
        public Task<bool> SetDatabaseProvisioningAsync(string appId, string databaseName, DateTime provisionedAtUtc) => Task.FromResult(true);
        public Task<bool> SetStatusAsync(string appId, AppStatus status, DateTime updatedAtUtc) => Task.FromResult(true);
    }
}
