using System.Reflection;
using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Services
{
    public interface IUserSettingsService
    {
        Task<MeSettingsResponse> GetAsync(string userId, CancellationToken cancellationToken);
        Task<(bool IsValid, string? ErrorMessage, MeSettingsResponse? Settings)> UpdateAsync(
            string userId,
            MeSettingsUpdateRequest request,
            CancellationToken cancellationToken);
    }

    public sealed class UserSettingsService : IUserSettingsService
    {
        private static readonly TimeZoneInfo UtcTimeZone = TimeZoneInfo.Utc;

        private readonly IMongoCollection<UserSettingsModel> _settings;
        private readonly ILogger<UserSettingsService> _logger;

        public UserSettingsService(IMongoDatabase database, ILogger<UserSettingsService> logger)
        {
            _settings = database.GetCollection<UserSettingsModel>(MongoCollectionNames.UserSettings);
            _logger = logger;
        }

        public async Task<MeSettingsResponse> GetAsync(string userId, CancellationToken cancellationToken)
        {
            var existing = await _settings.Find(x => x.UserId == userId).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                return ToResponse(existing);
            }

            var nowUtc = DateTime.UtcNow;
            var created = new UserSettingsModel
            {
                UserId = userId,
                Timezone = UtcTimeZone.Id,
                NotifyDeployments = true,
                NotifyErrors = true,
                NotifyFundingUpdates = true,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            try
            {
                await _settings.InsertOneAsync(created, cancellationToken: cancellationToken);
                return ToResponse(created);
            }
            catch (MongoWriteException ex)
            {
                _logger.LogDebug(ex, "User settings insert raced for user {UserId}", userId);
                existing = await _settings.Find(x => x.UserId == userId).FirstOrDefaultAsync(cancellationToken);
                return existing is not null ? ToResponse(existing) : ToResponse(created);
            }
        }

        public async Task<(bool IsValid, string? ErrorMessage, MeSettingsResponse? Settings)> UpdateAsync(
            string userId,
            MeSettingsUpdateRequest request,
            CancellationToken cancellationToken)
        {
            var existing = await GetOrCreateModelAsync(userId, cancellationToken);

            var timezoneCandidate = request.Timezone ?? existing.Timezone;
            if (!TryNormalizeTimeZoneId(timezoneCandidate, out var normalizedTimezone, out var tzError))
            {
                return (false, tzError, null);
            }

            var deployments = request.Notifications?.Deployments ?? existing.NotifyDeployments;
            var errors = request.Notifications?.Errors ?? existing.NotifyErrors;
            var fundingUpdates = request.Notifications?.FundingUpdates ?? existing.NotifyFundingUpdates;

            var nowUtc = DateTime.UtcNow;
            var update = Builders<UserSettingsModel>.Update
                .Set(x => x.Timezone, normalizedTimezone)
                .Set(x => x.NotifyDeployments, deployments)
                .Set(x => x.NotifyErrors, errors)
                .Set(x => x.NotifyFundingUpdates, fundingUpdates)
                .Set(x => x.UpdatedAt, nowUtc);

            await _settings.UpdateOneAsync(x => x.UserId == userId, update, cancellationToken: cancellationToken);

            var response = new MeSettingsResponse
            {
                UserId = userId,
                Timezone = normalizedTimezone,
                Notifications = new NotificationSettingsDto
                {
                    Deployments = deployments,
                    Errors = errors,
                    FundingUpdates = fundingUpdates
                },
                UpdatedAt = nowUtc
            };

            return (true, null, response);
        }

        private async Task<UserSettingsModel> GetOrCreateModelAsync(string userId, CancellationToken cancellationToken)
        {
            var existing = await _settings.Find(x => x.UserId == userId).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            var nowUtc = DateTime.UtcNow;
            var created = new UserSettingsModel
            {
                UserId = userId,
                Timezone = UtcTimeZone.Id,
                NotifyDeployments = true,
                NotifyErrors = true,
                NotifyFundingUpdates = true,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            try
            {
                await _settings.InsertOneAsync(created, cancellationToken: cancellationToken);
                return created;
            }
            catch (MongoWriteException ex)
            {
                _logger.LogDebug(ex, "User settings insert raced for user {UserId}", userId);
                existing = await _settings.Find(x => x.UserId == userId).FirstOrDefaultAsync(cancellationToken);
                return existing ?? created;
            }
        }

        private static MeSettingsResponse ToResponse(UserSettingsModel model)
        {
            return new MeSettingsResponse
            {
                UserId = model.UserId,
                Timezone = model.Timezone,
                Notifications = new NotificationSettingsDto
                {
                    Deployments = model.NotifyDeployments,
                    Errors = model.NotifyErrors,
                    FundingUpdates = model.NotifyFundingUpdates
                },
                UpdatedAt = model.UpdatedAt
            };
        }

        private static bool TryNormalizeTimeZoneId(string? input, out string normalized, out string errorMessage)
        {
            normalized = string.Empty;
            errorMessage = string.Empty;

            var candidate = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = UtcTimeZone.Id;
            }

            if (TryFindTimeZone(candidate, out var tz))
            {
                normalized = tz.Id;
                return true;
            }

            if (TryConvertTimeZoneId(candidate, "TryConvertIanaIdToWindowsId", out var windowsId)
                && TryFindTimeZone(windowsId, out tz))
            {
                normalized = tz.Id;
                return true;
            }

            if (TryConvertTimeZoneId(candidate, "TryConvertWindowsIdToIanaId", out var ianaId)
                && TryFindTimeZone(ianaId, out tz))
            {
                normalized = tz.Id;
                return true;
            }

            errorMessage = $"Invalid timezone '{candidate}'.";
            return false;
        }

        private static bool TryFindTimeZone(string id, out TimeZoneInfo tz)
        {
            tz = UtcTimeZone;

            if (string.Equals(id, "UTC", StringComparison.OrdinalIgnoreCase))
            {
                tz = UtcTimeZone;
                return true;
            }

            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(id);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
                return false;
            }
            catch (InvalidTimeZoneException)
            {
                return false;
            }
        }

        private static bool TryConvertTimeZoneId(string input, string methodName, out string converted)
        {
            converted = string.Empty;

            try
            {
                var method = typeof(TimeZoneInfo)
                    .GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (method is null)
                {
                    return false;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 2 || parameters[0].ParameterType != typeof(string))
                {
                    return false;
                }

                var args = new object?[] { input, null };
                var resultObj = method.Invoke(null, args);
                if (resultObj is bool ok && ok && args[1] is string output && !string.IsNullOrWhiteSpace(output))
                {
                    converted = output;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}

