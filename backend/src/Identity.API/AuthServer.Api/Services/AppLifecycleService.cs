using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Shared;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AuthServer.Api.Services
{
    public enum LifecycleFailureKind
    {
        NotFound,
        Forbidden,
        InvalidState,
        BadRequest
    }

    public sealed class LifecycleOperationResult<T>
    {
        public bool Succeeded { get; init; }
        public LifecycleFailureKind? FailureKind { get; init; }
        public string? Error { get; init; }
        public T? Value { get; init; }

        public static LifecycleOperationResult<T> Ok(T value)
            => new() { Succeeded = true, Value = value };

        public static LifecycleOperationResult<T> Fail(LifecycleFailureKind kind, string error)
            => new() { Succeeded = false, FailureKind = kind, Error = error };
    }

    public interface IAppLifecycleService
    {
        Task<LifecycleOperationResult<AppLifecycleStateResponse>> PauseAsync(
            string appId,
            string actorUserId,
            bool isAdmin,
            string? reason,
            string correlationId,
            CancellationToken cancellationToken);

        Task<LifecycleOperationResult<AppLifecycleStateResponse>> ResumeAsync(
            string appId,
            string actorUserId,
            bool isAdmin,
            string? reason,
            string correlationId,
            CancellationToken cancellationToken);

        Task<LifecycleOperationResult<AppLifecycleStateResponse>> SoftDeleteAsync(
            string appId,
            string actorUserId,
            bool isAdmin,
            bool confirm,
            string? reason,
            string correlationId,
            CancellationToken cancellationToken);
    }

    public sealed class AppLifecycleService : IAppLifecycleService
    {
        private static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(30);
        private const string FundingRoundsCollection = "FundingRounds";

        private readonly IMongoCollection<MozaiksAppModel> _apps;
        private readonly IMongoCollection<AppLifecycleEvent> _events;
        private readonly IMongoCollection<BsonDocument> _fundingRounds;
        private readonly ILogger<AppLifecycleService> _logger;

        public AppLifecycleService(
            IMongoDatabase database,
            ILogger<AppLifecycleService> logger)
        {
            _apps = database.GetCollection<MozaiksAppModel>(MongoCollectionNames.MozaiksApps);
            _events = database.GetCollection<AppLifecycleEvent>(MongoCollectionNames.AppLifecycleEvents);
            _fundingRounds = database.GetCollection<BsonDocument>(FundingRoundsCollection);
            _logger = logger;
        }

        public async Task<LifecycleOperationResult<AppLifecycleStateResponse>> PauseAsync(
            string appId,
            string actorUserId,
            bool isAdmin,
            string? reason,
            string correlationId,
            CancellationToken cancellationToken)
        {
            var app = await _apps.Find(a => a.Id == appId).FirstOrDefaultAsync(cancellationToken);
            if (app is null)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.NotFound, "App not found.");
            }

            if (!isAdmin && !string.Equals(app.OwnerUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.Forbidden, "You do not have permission to manage this app.");
            }

            if (app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.InvalidState, "Cannot pause a deleted app.");
            }

            if (app.Status == AppStatus.Paused)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Ok(ToState(app));
            }

            var nowUtc = DateTime.UtcNow;
            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);
            var update = Builders<MozaiksAppModel>.Update
                .Set(a => a.Status, AppStatus.Paused)
                .Set(a => a.PausedAt, nowUtc)
                .Set(a => a.UpdatedAt, nowUtc);

            var updated = await _apps.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<MozaiksAppModel> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (updated is null)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.NotFound, "App not found.");
            }

            await EmitEventAsync(updated.Id ?? appId, actorUserId, isAdmin, action: "pause", reason, correlationId, nowUtc, cancellationToken);

            return LifecycleOperationResult<AppLifecycleStateResponse>.Ok(ToState(updated));
        }

        public async Task<LifecycleOperationResult<AppLifecycleStateResponse>> ResumeAsync(
            string appId,
            string actorUserId,
            bool isAdmin,
            string? reason,
            string correlationId,
            CancellationToken cancellationToken)
        {
            var app = await _apps.Find(a => a.Id == appId).FirstOrDefaultAsync(cancellationToken);
            if (app is null)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.NotFound, "App not found.");
            }

            if (!isAdmin && !string.Equals(app.OwnerUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.Forbidden, "You do not have permission to manage this app.");
            }

            if (app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.InvalidState, "Cannot resume a deleted app.");
            }

            if (app.Status == AppStatus.Running)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Ok(ToState(app));
            }

            var nowUtc = DateTime.UtcNow;
            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);
            var update = Builders<MozaiksAppModel>.Update
                .Set(a => a.Status, AppStatus.Running)
                .Set(a => a.ResumedAt, nowUtc)
                .Set(a => a.UpdatedAt, nowUtc);

            var updated = await _apps.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<MozaiksAppModel> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (updated is null)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.NotFound, "App not found.");
            }

            await EmitEventAsync(updated.Id ?? appId, actorUserId, isAdmin, action: "resume", reason, correlationId, nowUtc, cancellationToken);

            return LifecycleOperationResult<AppLifecycleStateResponse>.Ok(ToState(updated));
        }

        public async Task<LifecycleOperationResult<AppLifecycleStateResponse>> SoftDeleteAsync(
            string appId,
            string actorUserId,
            bool isAdmin,
            bool confirm,
            string? reason,
            string correlationId,
            CancellationToken cancellationToken)
        {
            if (!confirm)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.BadRequest, "confirm: true is required.");
            }

            var app = await _apps.Find(a => a.Id == appId).FirstOrDefaultAsync(cancellationToken);
            if (app is null)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.NotFound, "App not found.");
            }

            if (!isAdmin && !string.Equals(app.OwnerUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.Forbidden, "You do not have permission to manage this app.");
            }

            if (app.IsDeleted || app.Status == AppStatus.Deleted)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Ok(ToState(app));
            }

            if (!isAdmin)
            {
                var (hasOpenFundingRound, checkFailed) = await CheckForOpenFundingRoundAsync(appId, cancellationToken);
                if (checkFailed)
                {
                    return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(
                        LifecycleFailureKind.InvalidState,
                        "Cannot delete app right now: unable to verify funding campaign status. Try again later or contact an admin.");
                }

                if (hasOpenFundingRound)
                {
                    return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(
                        LifecycleFailureKind.InvalidState,
                        "Cannot delete an app with an active funding campaign (OPEN funding round). Close or cancel the round first, or contact an admin.");
                }
            }

            var nowUtc = DateTime.UtcNow;
            var hardDeleteAt = nowUtc.Add(DefaultRetention);

            var filter = Builders<MozaiksAppModel>.Filter.Eq(a => a.Id, appId);
            var update = Builders<MozaiksAppModel>.Update
                .Set(a => a.Status, AppStatus.Deleted)
                .Set(a => a.IsDeleted, true)
                .Set(a => a.IsActive, false)
                .Set(a => a.DeletedAt, nowUtc)
                .Set(a => a.HardDeleteAt, hardDeleteAt)
                .Set(a => a.UpdatedAt, nowUtc);

            var updated = await _apps.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<MozaiksAppModel> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (updated is null)
            {
                return LifecycleOperationResult<AppLifecycleStateResponse>.Fail(LifecycleFailureKind.NotFound, "App not found.");
            }

            await EmitEventAsync(updated.Id ?? appId, actorUserId, isAdmin, action: "delete", reason, correlationId, nowUtc, cancellationToken);

            return LifecycleOperationResult<AppLifecycleStateResponse>.Ok(ToState(updated));
        }

        private async Task<(bool HasOpenFundingRound, bool CheckFailed)> CheckForOpenFundingRoundAsync(string appId, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(appId, out var appObjectId))
            {
                return (false, false);
            }

            try
            {
                var filter = Builders<BsonDocument>.Filter.Eq("appId", appObjectId)
                             & Builders<BsonDocument>.Filter.Eq("status", "OPEN");

                var hasOpen = await _fundingRounds.Find(filter).Limit(1).AnyAsync(cancellationToken);
                return (hasOpen, false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check funding rounds for app {AppId}", appId);
                return (false, true);
            }
        }

        private async Task EmitEventAsync(
            string appId,
            string actorUserId,
            bool isAdmin,
            string action,
            string? reason,
            string correlationId,
            DateTime timestampUtc,
            CancellationToken cancellationToken)
        {
            try
            {
                var ev = new AppLifecycleEvent
                {
                    AppId = appId,
                    ActorUserId = actorUserId,
                    ActorRole = isAdmin ? "admin" : "creator",
                    Action = action,
                    Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                    Timestamp = timestampUtc,
                    CorrelationId = correlationId,
                    CreatedAt = timestampUtc,
                    UpdatedAt = timestampUtc
                };

                await _events.InsertOneAsync(ev, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record lifecycle event {Action} for app {AppId}", action, appId);
            }
        }

        private static AppLifecycleStateResponse ToState(MozaiksAppModel app)
        {
            return new AppLifecycleStateResponse
            {
                AppId = app.Id ?? string.Empty,
                Status = app.Status.ToString().ToLowerInvariant(),
                PausedAt = app.PausedAt,
                ResumedAt = app.ResumedAt,
                DeletedAt = app.DeletedAt,
                HardDeleteAt = app.HardDeleteAt
            };
        }
    }
}
