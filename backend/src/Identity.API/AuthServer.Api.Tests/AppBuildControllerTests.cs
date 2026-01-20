using AuthServer.Api.Controllers;
using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Repository.Interfaces;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Mozaiks.Auth;
using System.Security.Claims;

namespace AuthServer.Api.Tests;

public sealed class AppBuildControllerTests
{
    [Fact]
    public async Task PostBuildEvent_ReturnsBadRequest_WhenEventTypeInvalid()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.PostBuildEvent(
            appId: "507f191e810c19729de860ea",
            request: new AppBuildEventRequest
            {
                EventType = "nope",
                BuildId = "build-1"
            },
            cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static AppBuildController CreateController()
    {
        var apps = new MozaiksAppService(new FakeAppsRepo());
        var status = new InMemoryBuildStatusRepo();
        var eventsRepo = new InMemoryBuildEventRepo();

        var config = new ConfigurationBuilder().Build();
        var notifications = new NotificationApiClient(
            new HttpClient(new NoopHttpMessageHandler()),
            config,
            new StubServiceToServiceTokenProvider(),
            NullLogger<NotificationApiClient>.Instance);

        var logs = new StructuredLogEmitter(NullLogger<StructuredLogEmitter>.Instance);

        return new AppBuildController(apps, status, eventsRepo, notifications, logs, new NullUserContextAccessor());
    }

    private sealed class NullUserContextAccessor : IUserContextAccessor
    {
        public UserContext? GetUser(ClaimsPrincipal? principal = null) => null;
        public UserContext GetRequiredUser(ClaimsPrincipal? principal = null)
            => throw new InvalidOperationException("User context is not available in these unit tests.");
    }

    private sealed class FakeAppsRepo : IMozaiksAppRepository
    {
        public Task<List<MozaiksAppModel>> GetAllAppsAsync() => throw new NotImplementedException();
        public Task<MozaiksAppModel?> GetByIdAsync(string id) => throw new NotImplementedException();
        public Task<List<MozaiksAppModel>> GetByOwnerUserIdAsync(string userId) => throw new NotImplementedException();
        public Task<List<MozaiksAppModel>> GetByIdsAsync(IEnumerable<string> appIds) => throw new NotImplementedException();
        public Task<List<MozaiksAppModel>> GetPublicAsync() => throw new NotImplementedException();
        public Task CreateAsync(MozaiksAppModel app) => throw new NotImplementedException();
        public Task UpdateAsync(MozaiksAppModel app) => throw new NotImplementedException();
        public Task DeleteAsync(string id) => throw new NotImplementedException();
        public Task AddMembersToTeamAsync(string appId, string[] memberIds) => throw new NotImplementedException();
        public Task<bool> PatchAppConfigAsync(string appId, AppConfigPatchRequest request) => throw new NotImplementedException();
        public Task<bool> SetPublishStatusAsync(string appId, bool publish) => throw new NotImplementedException();
        public Task<bool> SetFeatureFlagAsync(string appId, string flag, bool enabled) => throw new NotImplementedException();
        public Task<bool> TryGenerateApiKeyAsync(string appId, string apiKeyHash, string apiKeyPrefix, DateTime createdAtUtc) => throw new NotImplementedException();
        public Task<bool> RegenerateApiKeyAsync(string appId, string apiKeyHash, string apiKeyPrefix, DateTime createdAtUtc) => throw new NotImplementedException();
        public Task<bool> UpdateApiKeyLastUsedAtAsync(string appId, DateTime lastUsedAtUtc) => throw new NotImplementedException();
        public Task<bool> SetGitHubDeploymentAsync(string appId, string repoUrl, string repoFullName, DateTime deployedAtUtc) => throw new NotImplementedException();
        public Task<bool> SetDatabaseProvisioningAsync(string appId, string databaseName, DateTime provisionedAtUtc) => throw new NotImplementedException();
        public Task<bool> SetStatusAsync(string appId, AppStatus status, DateTime updatedAtUtc) => throw new NotImplementedException();
    }

    private sealed class InMemoryBuildStatusRepo : IAppBuildStatusRepository
    {
        public Task<AppBuildStatusModel?> GetByAppIdAsync(string appId, CancellationToken cancellationToken)
            => Task.FromResult<AppBuildStatusModel?>(null);

        public Task UpsertAsync(AppBuildStatusModel status, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class InMemoryBuildEventRepo : IAppBuildEventRepository
    {
        public Task InsertAsync(AppBuildEventModel evt, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class NoopHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Accepted));
    }

    private sealed class StubServiceToServiceTokenProvider : IServiceToServiceTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("test-token");
    }
}
