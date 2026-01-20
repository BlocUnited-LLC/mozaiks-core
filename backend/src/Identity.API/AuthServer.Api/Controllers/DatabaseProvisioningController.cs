using System.Diagnostics;
using System.Security.Claims;
using AuthServer.Api.DTOs;
using AuthServer.Api.Services;
using AuthServer.Api.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Mozaiks.Auth;

namespace AuthServer.Api.Controllers;

/// <summary>
/// Database provisioning endpoints for MozaiksAI apps.
/// Supports user JWTs plus internal service (client-credentials) JWTs via internal routes under /api/internal.
/// This replaces DBManager from project-aid-v2.
/// </summary>
[ApiController]
[Route("api/apps/{appId}/database")]
public sealed class DatabaseProvisioningController : ControllerBase
{
    private readonly IDatabaseProvisioningService _provisioning;
    private readonly MozaiksAppService _apps;
    private readonly StructuredLogEmitter _logs;
    private readonly IUserContextAccessor _userContextAccessor;

    public DatabaseProvisioningController(
        IDatabaseProvisioningService provisioning,
        MozaiksAppService apps,
        StructuredLogEmitter logs,
        IUserContextAccessor userContextAccessor)
    {
        _provisioning = provisioning;
        _apps = apps;
        _logs = logs;
        _userContextAccessor = userContextAccessor;
    }

    /// <summary>
    /// Provision a new database for an app.
    /// User endpoint. Internal callers should use /api/internal/apps/{appId}/database/provision.
    /// </summary>
    [HttpPost("provision")]
    [Authorize]
    public async Task<IActionResult> ProvisionDatabase(string appId, [FromBody] ProvisionDatabaseRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        const bool isInternalCall = false;

        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
        {
            return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("userId", userId);
        Activity.Current?.SetTag("appId", appId);

        var context = new StructuredLogContext
        {
            CorrelationId = correlationId,
            UserId = userId,
            AppId = appId
        };

        _logs.Info("Apps.Database.Provision.Requested", context, new { internalCall = isInternalCall });

        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            _logs.Warn("Apps.Database.Provision.AppNotFound", context);
            return NotFound(new { error = "NotFound" });
        }

        if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            _logs.Warn("Apps.Database.Provision.Forbidden", context);
            return Forbid();
        }

        var result = await _provisioning.ProvisionDatabaseAsync(appId, app.Name, request?.SchemaJson, request?.SeedJson, cancellationToken);
        if (!result.Success)
        {
            _logs.Error("Apps.Database.Provision.Failed", context, new { error = result.ErrorMessage });
            return BadRequest(new { error = result.ErrorMessage ?? "FailedToProvisionDatabase" });
        }

        var createdNew = !string.IsNullOrWhiteSpace(result.ConnectionString);

        if (createdNew || string.IsNullOrWhiteSpace(app.DatabaseName) || app.DatabaseProvisionedAt is null)
        {
            var provisionedAt = app.DatabaseProvisionedAt ?? DateTime.UtcNow;
            await _apps.SetDatabaseProvisioningAsync(appId, result.DatabaseName, provisionedAt);
        }

        _logs.Info("Apps.Database.Provision.Completed", context, new { databaseName = result.DatabaseName, createdNew });

        return Ok(new
        {
            databaseName = result.DatabaseName,
            connectionString = createdNew ? result.ConnectionString : null,
            message = createdNew
                ? "Database provisioned. Save the connection string - it won't be shown again."
                : "Database already provisioned."
        });
    }

    [HttpPost("/api/internal/apps/{appId}/database/provision")]
    [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
    public async Task<IActionResult> ProvisionDatabaseInternal(
        string appId,
        [FromBody] ProvisionDatabaseRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        const bool isInternalCall = true;
        var userId = (request?.UserId ?? "internal").Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "internal";
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("userId", userId);
        Activity.Current?.SetTag("appId", appId);

        var context = new StructuredLogContext
        {
            CorrelationId = correlationId,
            UserId = userId,
            AppId = appId
        };

        _logs.Info("Apps.Database.Provision.Requested", context, new { internalCall = isInternalCall });

        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            _logs.Warn("Apps.Database.Provision.AppNotFound", context);
            return NotFound(new { error = "NotFound" });
        }

        var result = await _provisioning.ProvisionDatabaseAsync(appId, app.Name, request?.SchemaJson, request?.SeedJson, cancellationToken);
        if (!result.Success)
        {
            _logs.Error("Apps.Database.Provision.Failed", context, new { error = result.ErrorMessage });
            return BadRequest(new { error = result.ErrorMessage ?? "FailedToProvisionDatabase" });
        }

        var createdNew = !string.IsNullOrWhiteSpace(result.ConnectionString);

        if (createdNew || string.IsNullOrWhiteSpace(app.DatabaseName) || app.DatabaseProvisionedAt is null)
        {
            var provisionedAt = app.DatabaseProvisionedAt ?? DateTime.UtcNow;
            await _apps.SetDatabaseProvisioningAsync(appId, result.DatabaseName, provisionedAt);
        }

        _logs.Info("Apps.Database.Provision.Completed", context, new { databaseName = result.DatabaseName, createdNew });

        return Ok(new
        {
            databaseName = result.DatabaseName,
            connectionString = createdNew ? result.ConnectionString : null,
            message = createdNew
                ? "Database provisioned. Save the connection string - it won't be shown again."
                : "Database already provisioned."
        });
    }

    /// <summary>
    /// Apply schema definition to an app's database.
    /// User endpoint. Internal callers should use /api/internal/apps/{appId}/database/schema.
    /// </summary>
    [HttpPost("schema")]
    [Authorize]
    public async Task<IActionResult> ApplySchema(string appId, [FromBody] ApplySchemaRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        if (request?.Schema is null)
        {
            return BadRequest(new { error = "InvalidRequest", reason = "Schema is required" });
        }

        const bool isInternalCall = false;

        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
        {
            return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("userId", userId);
        Activity.Current?.SetTag("appId", appId);

        var context = new StructuredLogContext
        {
            CorrelationId = correlationId,
            UserId = userId,
            AppId = appId
        };

        _logs.Info("Apps.Database.Schema.Requested", context, new { tableCount = request.Schema.Tables.Count, internalCall = isInternalCall });

        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            return NotFound(new { error = "NotFound" });
        }

        if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var success = await _provisioning.ApplySchemaAsync(appId, request.Schema, cancellationToken);
        if (!success)
        {
            _logs.Error("Apps.Database.Schema.Failed", context);
            return BadRequest(new { error = "FailedToApplySchema" });
        }

        _logs.Info("Apps.Database.Schema.Completed", context);

        return Ok(new { message = "Schema applied successfully" });
    }

    [HttpPost("/api/internal/apps/{appId}/database/schema")]
    [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
    public async Task<IActionResult> ApplySchemaInternal(
        string appId,
        [FromBody] ApplySchemaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        if (request?.Schema is null)
        {
            return BadRequest(new { error = "InvalidRequest", reason = "Schema is required" });
        }

        const bool isInternalCall = true;
        var userId = (request.UserId ?? "internal").Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "internal";
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("userId", userId);
        Activity.Current?.SetTag("appId", appId);

        var context = new StructuredLogContext
        {
            CorrelationId = correlationId,
            UserId = userId,
            AppId = appId
        };

        _logs.Info("Apps.Database.Schema.Requested", context, new { tableCount = request.Schema.Tables.Count, internalCall = isInternalCall });

        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            return NotFound(new { error = "NotFound" });
        }

        var success = await _provisioning.ApplySchemaAsync(appId, request.Schema, cancellationToken);
        if (!success)
        {
            _logs.Error("Apps.Database.Schema.Failed", context);
            return BadRequest(new { error = "FailedToApplySchema" });
        }

        _logs.Info("Apps.Database.Schema.Completed", context);

        return Ok(new { message = "Schema applied successfully" });
    }

    /// <summary>
    /// Seed an app's database with initial data.
    /// User endpoint. Internal callers should use /api/internal/apps/{appId}/database/seed.
    /// </summary>
    [HttpPost("seed")]
    [Authorize]
    public async Task<IActionResult> SeedDatabase(string appId, [FromBody] SeedDatabaseRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        if (request?.SeedData is null)
        {
            return BadRequest(new { error = "InvalidRequest", reason = "SeedData is required" });
        }

        const bool isInternalCall = false;

        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId) || userId == "unknown")
        {
            return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("userId", userId);
        Activity.Current?.SetTag("appId", appId);

        var context = new StructuredLogContext
        {
            CorrelationId = correlationId,
            UserId = userId,
            AppId = appId
        };

        _logs.Info("Apps.Database.Seed.Requested", context, new { internalCall = isInternalCall });

        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            return NotFound(new { error = "NotFound" });
        }

        if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var seedData = ParseSeedData(request.SeedData.Value);

        var success = await _provisioning.SeedDatabaseAsync(appId, seedData, cancellationToken);
        if (!success)
        {
            _logs.Error("Apps.Database.Seed.Failed", context);
            return BadRequest(new { error = "FailedToSeedDatabase" });
        }

        _logs.Info("Apps.Database.Seed.Completed", context, new { collections = seedData.Collections.Count });

        return Ok(new { message = "Database seeded successfully" });
    }

    [HttpPost("/api/internal/apps/{appId}/database/seed")]
    [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
    public async Task<IActionResult> SeedDatabaseInternal(
        string appId,
        [FromBody] SeedDatabaseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        if (request?.SeedData is null)
        {
            return BadRequest(new { error = "InvalidRequest", reason = "SeedData is required" });
        }

        const bool isInternalCall = true;
        var userId = (request.UserId ?? "internal").Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "internal";
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        Activity.Current?.SetTag("correlationId", correlationId);
        Activity.Current?.SetTag("userId", userId);
        Activity.Current?.SetTag("appId", appId);

        var context = new StructuredLogContext
        {
            CorrelationId = correlationId,
            UserId = userId,
            AppId = appId
        };

        _logs.Info("Apps.Database.Seed.Requested", context, new { internalCall = isInternalCall });

        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            return NotFound(new { error = "NotFound" });
        }

        var seedData = ParseSeedData(request.SeedData.Value);

        var success = await _provisioning.SeedDatabaseAsync(appId, seedData, cancellationToken);
        if (!success)
        {
            _logs.Error("Apps.Database.Seed.Failed", context);
            return BadRequest(new { error = "FailedToSeedDatabase" });
        }

        _logs.Info("Apps.Database.Seed.Completed", context, new { collections = seedData.Collections.Count });

        return Ok(new { message = "Database seeded successfully" });
    }

    /// <summary>
    /// Get the provisioning status of an app's database.
    /// User endpoint. Internal callers should use /api/internal/apps/{appId}/database/status.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetDatabaseStatus(string appId, [FromQuery] string? userId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        const bool isInternalCall = false;

        var effectiveUserId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(effectiveUserId) || effectiveUserId == "unknown")
        {
            return Unauthorized(new { error = "Unauthorized", reason = "MissingUserId" });
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            return NotFound(new { error = "NotFound" });
        }

        if (!IsPlatformAdmin() && !string.Equals(app.OwnerUserId, effectiveUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var provisioned = !string.IsNullOrWhiteSpace(app.DatabaseName);

        return Ok(new
        {
            provisioned,
            databaseName = provisioned ? app.DatabaseName : null,
            provisionedAt = app.DatabaseProvisionedAt
        });
    }

    [HttpGet("/api/internal/apps/{appId}/database/status")]
    [Authorize(Policy = MozaiksAuthDefaults.InternalServicePolicy)]
    public async Task<IActionResult> GetDatabaseStatusInternal(
        string appId,
        [FromQuery] string? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return BadRequest(new { error = "InvalidAppId" });
        }

        var correlationId = GetOrCreateCorrelationId();
        Response.Headers["x-correlation-id"] = correlationId;

        var app = await _apps.GetByIdAsync(appId);
        if (app is null)
        {
            return NotFound(new { error = "NotFound" });
        }

        var provisioned = !string.IsNullOrWhiteSpace(app.DatabaseName);

        return Ok(new
        {
            provisioned,
            databaseName = provisioned ? app.DatabaseName : null,
            provisionedAt = app.DatabaseProvisionedAt
        });
    }

    private static SeedData ParseSeedData(JsonElement root)
    {
        var seed = new SeedData();

        if (root.ValueKind != JsonValueKind.Object)
        {
            return seed;
        }

        var effective = root;
        if (TryGetPropertyIgnoreCase(root, "collections", out var collectionsProp)
            && collectionsProp.ValueKind == JsonValueKind.Object)
        {
            effective = collectionsProp;
        }

        foreach (var prop in effective.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Array && prop.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var docs = new List<string>();
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var doc in prop.Value.EnumerateArray())
                {
                    docs.Add(doc.GetRawText());
                }
            }
            else
            {
                docs.Add(prop.Value.GetRawText());
            }

            seed.Collections.Add(new CollectionSeedData
            {
                Name = prop.Name,
                DocumentsJson = docs
            });
        }

        return seed;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string propertyName, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private string GetCurrentUserId()
    {
        return _userContextAccessor.GetUser(User)?.UserId ?? "unknown";
    }

    private bool IsPlatformAdmin()
    {
        var user = _userContextAccessor.GetUser(User);
        if (user is null)
        {
            return false;
        }

        return user.IsSuperAdmin
               || user.Roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));
    }

    private string GetOrCreateCorrelationId()
    {
        var header = Request.Headers["x-correlation-id"].ToString();
        return string.IsNullOrWhiteSpace(header) ? Guid.NewGuid().ToString() : header;
    }
}
