using AuthServer.Api.Models;
using AuthServer.Api.Shared;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AuthServer.Api.Infrastructure;

public sealed class MongoIndexInitializer
{
    private readonly IMongoDatabase _database;
    private readonly IMongoClient _client;
    private readonly DatabaseProvisioningOptions _provisioningOptions;

    public MongoIndexInitializer(
        IMongoDatabase database,
        IMongoClient client,
        IOptions<DatabaseProvisioningOptions> provisioningOptions)
    {
        _database = database;
        _client = client;
        _provisioningOptions = provisioningOptions.Value;
    }

    public async Task InitializeAsync()
    {
        await EnsureTeamIndexesAsync();
        await EnsureInviteIndexesAsync();
        await EnsureDeploymentJobIndexesAsync();
        await EnsureConnectionStringIndexesAsync();
        await EnsureAppLifecycleEventIndexesAsync();
        await EnsureAppBuildIndexesAsync();
        await EnsureUserSettingsIndexesAsync();
        await EnsureAppAdminSurfacesIndexesAsync();
        await EnsureAppModuleProxyAuditIndexesAsync();
        await EnsureExternalLoginIndexesAsync();
        await EnsureAppMonetizationSpecIndexesAsync();
        await EnsureAppMonetizationAuditIndexesAsync();
    }

    private async Task EnsureTeamIndexesAsync()
    {
        var coll = _database.GetCollection<TeamMembersModel>("Teams");

        var uniqueMember = Builders<TeamMembersModel>.IndexKeys
            .Ascending(x => x.AppId)
            .Ascending(x => x.UserId);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<TeamMembersModel>(
            uniqueMember,
            new CreateIndexOptions { Name = "app_user_unique", Unique = true }));

        var userApps = Builders<TeamMembersModel>.IndexKeys
            .Ascending(x => x.UserId)
            .Ascending(x => x.AppId);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<TeamMembersModel>(
            userApps,
            new CreateIndexOptions { Name = "user_app" }));
    }

    private async Task EnsureInviteIndexesAsync()
    {
        var coll = _database.GetCollection<InviteModel>("Invites");

        var recipientStatus = Builders<InviteModel>.IndexKeys
            .Ascending(x => x.ReceipentUserId)
            .Ascending(x => x.InviteStatus)
            .Descending(x => x.CreatedAt);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<InviteModel>(
            recipientStatus,
            new CreateIndexOptions { Name = "recipient_status_createdAt" }));

        var appInviterRecipientStatus = Builders<InviteModel>.IndexKeys
            .Ascending(x => x.AppId)
            .Ascending(x => x.InvitedByUserId)
            .Ascending(x => x.ReceipentUserId)
            .Ascending(x => x.InviteStatus);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<InviteModel>(
            appInviterRecipientStatus,
            new CreateIndexOptions { Name = "app_inviter_recipient_status" }));
    }

    private async Task EnsureDeploymentJobIndexesAsync()
    {
        var coll = _database.GetCollection<DeploymentJob>(MongoCollectionNames.DeploymentJobs);

        var statusCreated = Builders<DeploymentJob>.IndexKeys
            .Ascending(x => x.Status)
            .Ascending(x => x.CreatedAt);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<DeploymentJob>(
            statusCreated,
            new CreateIndexOptions { Name = "status_createdAt" }));
    }

    private async Task EnsureConnectionStringIndexesAsync()
    {
        var dbName = string.IsNullOrWhiteSpace(_provisioningOptions.MetadataDatabase)
            ? _database.DatabaseNamespace.DatabaseName
            : _provisioningOptions.MetadataDatabase.Trim();

        var db = _client.GetDatabase(dbName);
        var coll = db.GetCollection<ConnectionStringRecord>("ConnectionStrings");

        var appIdUnique = Builders<ConnectionStringRecord>.IndexKeys.Ascending(x => x.AppId);
        await coll.Indexes.CreateOneAsync(new CreateIndexModel<ConnectionStringRecord>(
            appIdUnique,
            new CreateIndexOptions { Name = "ux_appId", Unique = true }));

        var databaseNameUnique = Builders<ConnectionStringRecord>.IndexKeys.Ascending(x => x.DatabaseName);
        await coll.Indexes.CreateOneAsync(new CreateIndexModel<ConnectionStringRecord>(
            databaseNameUnique,
            new CreateIndexOptions { Name = "ux_databaseName", Unique = true }));
    }

    private async Task EnsureAppLifecycleEventIndexesAsync()
    {
        var coll = _database.GetCollection<AppLifecycleEvent>(MongoCollectionNames.AppLifecycleEvents);

        var appIdTimestamp = Builders<AppLifecycleEvent>.IndexKeys
            .Ascending(x => x.AppId)
            .Descending(x => x.Timestamp);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<AppLifecycleEvent>(
            appIdTimestamp,
            new CreateIndexOptions { Name = "appId_timestamp" }));
    }

    private async Task EnsureAppBuildIndexesAsync()
    {
        var statusColl = _database.GetCollection<AppBuildStatusModel>(MongoCollectionNames.AppBuildStatuses);

        var appIdUnique = Builders<AppBuildStatusModel>.IndexKeys.Ascending(x => x.AppId);
        await statusColl.Indexes.CreateOneAsync(new CreateIndexModel<AppBuildStatusModel>(
            appIdUnique,
            new CreateIndexOptions { Name = "ux_appId", Unique = true }));

        var eventsColl = _database.GetCollection<AppBuildEventModel>(MongoCollectionNames.AppBuildEvents);

        var appIdOccurredAt = Builders<AppBuildEventModel>.IndexKeys
            .Ascending(x => x.AppId)
            .Descending(x => x.OccurredAtUtc);

        await eventsColl.Indexes.CreateOneAsync(new CreateIndexModel<AppBuildEventModel>(
            appIdOccurredAt,
            new CreateIndexOptions { Name = "appId_occurredAtUtc" }));
    }

    private async Task EnsureUserSettingsIndexesAsync()
    {
        var coll = _database.GetCollection<UserSettingsModel>(MongoCollectionNames.UserSettings);

        var userIdUnique = Builders<UserSettingsModel>.IndexKeys.Ascending(x => x.UserId);
        await coll.Indexes.CreateOneAsync(new CreateIndexModel<UserSettingsModel>(
            userIdUnique,
            new CreateIndexOptions { Name = "ux_userId", Unique = true }));
    }

    private async Task EnsureAppAdminSurfacesIndexesAsync()
    {
        var coll = _database.GetCollection<AppAdminSurfaceModel>(MongoCollectionNames.AppAdminSurfaces);

        var appIdUnique = Builders<AppAdminSurfaceModel>.IndexKeys.Ascending(x => x.AppId);
        await coll.Indexes.CreateOneAsync(new CreateIndexModel<AppAdminSurfaceModel>(
            appIdUnique,
            new CreateIndexOptions { Name = "ux_appId", Unique = true }));
    }

    private async Task EnsureAppModuleProxyAuditIndexesAsync()
    {
        var coll = _database.GetCollection<AppModuleProxyAuditEvent>(MongoCollectionNames.AppModuleProxyAuditEvents);

        var appIdTimestamp = Builders<AppModuleProxyAuditEvent>.IndexKeys
            .Ascending(x => x.AppId)
            .Descending(x => x.Timestamp);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<AppModuleProxyAuditEvent>(
            appIdTimestamp,
            new CreateIndexOptions { Name = "appId_timestamp" }));
    }

    private async Task EnsureExternalLoginIndexesAsync()
    {
        var coll = _database.GetCollection<ExternalLoginModel>("ExternalLogins");

        var providerSubjectUnique = Builders<ExternalLoginModel>.IndexKeys
            .Ascending(x => x.Provider)
            .Ascending(x => x.Subject);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<ExternalLoginModel>(
            providerSubjectUnique,
            new CreateIndexOptions { Name = "ux_provider_subject", Unique = true }));

        var providerUserId = Builders<ExternalLoginModel>.IndexKeys
            .Ascending(x => x.Provider)
            .Ascending(x => x.UserId);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<ExternalLoginModel>(
            providerUserId,
            new CreateIndexOptions { Name = "provider_userId" }));
    }

    private async Task EnsureAppMonetizationSpecIndexesAsync()
    {
        var coll = _database.GetCollection<AppMonetizationSpecVersion>(MongoCollectionNames.AppMonetizationSpecVersions);

        var appStatusVersion = Builders<AppMonetizationSpecVersion>.IndexKeys
            .Ascending(x => x.AppId)
            .Ascending(x => x.Status)
            .Descending(x => x.Version);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<AppMonetizationSpecVersion>(
            appStatusVersion,
            new CreateIndexOptions { Name = "app_status_version" }));

        var appVersion = Builders<AppMonetizationSpecVersion>.IndexKeys
            .Ascending(x => x.AppId)
            .Descending(x => x.Version);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<AppMonetizationSpecVersion>(
            appVersion,
            new CreateIndexOptions { Name = "app_version" }));
    }

    private async Task EnsureAppMonetizationAuditIndexesAsync()
    {
        var coll = _database.GetCollection<AppMonetizationAuditEvent>(MongoCollectionNames.AppMonetizationAuditEvents);

        var appOccurredAt = Builders<AppMonetizationAuditEvent>.IndexKeys
            .Ascending(x => x.AppId)
            .Descending(x => x.OccurredAtUtc);

        await coll.Indexes.CreateOneAsync(new CreateIndexModel<AppMonetizationAuditEvent>(
            appOccurredAt,
            new CreateIndexOptions { Name = "app_occurredAtUtc" }));
    }
}
