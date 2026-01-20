using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Shared;
using MongoDB.Driver;

namespace AuthServer.Api.Services
{
    public interface IPlatformSettingsService
    {
        Task<PlatformSettingsResponse> GetAsync(CancellationToken cancellationToken);
        Task<(bool IsValid, string? ErrorMessage, PlatformSettingsResponse? Settings)> UpdateAsync(
            string updatedByUserId,
            PlatformSettingsUpdateRequest request,
            CancellationToken cancellationToken);
    }

    public sealed class PlatformSettingsService : IPlatformSettingsService
    {
        private const int MaxAllowedPageSize = 500;
        private readonly IMongoCollection<PlatformSettingsModel> _settings;
        private readonly ILogger<PlatformSettingsService> _logger;

        public PlatformSettingsService(IMongoDatabase database, ILogger<PlatformSettingsService> logger)
        {
            _settings = database.GetCollection<PlatformSettingsModel>(MongoCollectionNames.PlatformSettings);
            _logger = logger;
        }

        public async Task<PlatformSettingsResponse> GetAsync(CancellationToken cancellationToken)
        {
            var existing = await _settings
                .Find(x => x.Id == PlatformSettingsModel.PlatformDocumentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null)
            {
                return ToResponse(existing);
            }

            var nowUtc = DateTime.UtcNow;
            var created = new PlatformSettingsModel
            {
                Id = PlatformSettingsModel.PlatformDocumentId,
                EnableFunding = true,
                EnableE2BValidation = true,
                DefaultPageSize = 20,
                MaxPageSize = 100,
                UpdatedAt = nowUtc,
                UpdatedByUserId = null
            };

            try
            {
                await _settings.InsertOneAsync(created, cancellationToken: cancellationToken);
                return ToResponse(created);
            }
            catch (MongoWriteException ex)
            {
                _logger.LogDebug(ex, "Platform settings insert raced");
                existing = await _settings
                    .Find(x => x.Id == PlatformSettingsModel.PlatformDocumentId)
                    .FirstOrDefaultAsync(cancellationToken);
                return existing is not null ? ToResponse(existing) : ToResponse(created);
            }
        }

        public async Task<(bool IsValid, string? ErrorMessage, PlatformSettingsResponse? Settings)> UpdateAsync(
            string updatedByUserId,
            PlatformSettingsUpdateRequest request,
            CancellationToken cancellationToken)
        {
            var existing = await _settings
                .Find(x => x.Id == PlatformSettingsModel.PlatformDocumentId)
                .FirstOrDefaultAsync(cancellationToken);

            var enableFunding = request.Features?.Funding ?? existing?.EnableFunding ?? true;
            var enableE2bValidation = request.Features?.E2bValidation ?? existing?.EnableE2BValidation ?? true;

            var defaultPageSize = request.Pagination?.DefaultPageSize ?? existing?.DefaultPageSize ?? 20;
            var maxPageSize = request.Pagination?.MaxPageSize ?? existing?.MaxPageSize ?? 100;

            if (defaultPageSize <= 0)
            {
                return (false, "pagination.defaultPageSize must be greater than 0.", null);
            }

            if (maxPageSize <= 0)
            {
                return (false, "pagination.maxPageSize must be greater than 0.", null);
            }

            if (maxPageSize > MaxAllowedPageSize)
            {
                return (false, $"pagination.maxPageSize must be <= {MaxAllowedPageSize}.", null);
            }

            if (defaultPageSize > maxPageSize)
            {
                return (false, "pagination.defaultPageSize must be <= pagination.maxPageSize.", null);
            }

            var nowUtc = DateTime.UtcNow;
            var filter = Builders<PlatformSettingsModel>.Filter.Eq(x => x.Id, PlatformSettingsModel.PlatformDocumentId);
            var update = Builders<PlatformSettingsModel>.Update
                .Set(x => x.EnableFunding, enableFunding)
                .Set(x => x.EnableE2BValidation, enableE2bValidation)
                .Set(x => x.DefaultPageSize, defaultPageSize)
                .Set(x => x.MaxPageSize, maxPageSize)
                .Set(x => x.UpdatedAt, nowUtc)
                .Set(x => x.UpdatedByUserId, updatedByUserId);

            await _settings.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);

            var response = new PlatformSettingsResponse
            {
                Features = new PlatformFeaturesDto
                {
                    Funding = enableFunding,
                    E2bValidation = enableE2bValidation
                },
                Pagination = new PaginationSettingsDto
                {
                    DefaultPageSize = defaultPageSize,
                    MaxPageSize = maxPageSize
                },
                UpdatedAt = nowUtc,
                UpdatedByUserId = updatedByUserId
            };

            return (true, null, response);
        }

        private static PlatformSettingsResponse ToResponse(PlatformSettingsModel model)
        {
            return new PlatformSettingsResponse
            {
                Features = new PlatformFeaturesDto
                {
                    Funding = model.EnableFunding,
                    E2bValidation = model.EnableE2BValidation
                },
                Pagination = new PaginationSettingsDto
                {
                    DefaultPageSize = model.DefaultPageSize,
                    MaxPageSize = model.MaxPageSize
                },
                UpdatedAt = model.UpdatedAt,
                UpdatedByUserId = model.UpdatedByUserId
            };
        }
    }
}
