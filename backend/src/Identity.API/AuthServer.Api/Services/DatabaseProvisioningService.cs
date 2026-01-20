using AuthServer.Api.DTOs;
using AuthServer.Api.Models;
using AuthServer.Api.Shared;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace AuthServer.Api.Services;

public interface IDatabaseProvisioningService
{
    Task<DatabaseProvisionResult> ProvisionDatabaseAsync(string appId, string appName, string? schemaJson, string? seedJson, CancellationToken cancellationToken);
    Task<bool> ApplySchemaAsync(string appId, SchemaDefinition schema, CancellationToken cancellationToken);
    Task<bool> SeedDatabaseAsync(string appId, SeedData seedData, CancellationToken cancellationToken);
    Task<string?> GetConnectionStringAsync(string appId, CancellationToken cancellationToken);
}

public sealed class DatabaseProvisioningService : IDatabaseProvisioningService
{
    private readonly IMongoClient _adminClient;
    private readonly IMongoDatabase _metadataDb;
    private readonly IMongoCollection<ConnectionStringRecord> _connectionStrings;
    private readonly ILogger<DatabaseProvisioningService> _logger;
    private readonly string _adminConnectionString;
    private readonly DatabaseProvisioningOptions _options;
    private readonly ProvisioningAgentClient _agent;

    public DatabaseProvisioningService(
        IMongoClient platformClient,
        IConfiguration configuration,
        IOptions<DatabaseProvisioningOptions> options,
        ILogger<DatabaseProvisioningService> logger,
        ProvisioningAgentClient agent)
    {
        _options = options.Value;
        _logger = logger;
        _agent = agent;

        _adminConnectionString = !string.IsNullOrWhiteSpace(_options.AdminConnectionString)
            ? _options.AdminConnectionString.Trim()
            : (configuration.GetValue<string>("MongoDB:ConnectionString") ?? string.Empty).Trim();

        _adminClient = !string.IsNullOrWhiteSpace(_options.AdminConnectionString)
            ? new MongoClient(_adminConnectionString)
            : platformClient;

        var metadataDbName = string.IsNullOrWhiteSpace(_options.MetadataDatabase)
            ? (configuration.GetValue<string>("MongoDB:DatabaseName") ?? "MozaiksDB")
            : _options.MetadataDatabase.Trim();

        _metadataDb = _adminClient.GetDatabase(metadataDbName);
        _connectionStrings = _metadataDb.GetCollection<ConnectionStringRecord>("ConnectionStrings");
    }

    public async Task<DatabaseProvisionResult> ProvisionDatabaseAsync(string appId, string appName, string? schemaJson, string? seedJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return new DatabaseProvisionResult { Success = false, ErrorMessage = "appId is required" };
        }

        try
        {
            var existing = await _connectionStrings.Find(x => x.AppId == appId)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is not null
                && !string.IsNullOrWhiteSpace(existing.DatabaseName)
                && !string.IsNullOrWhiteSpace(existing.ConnectionString))
            {
                _logger.LogInformation("Database already provisioned for app {AppId} database {DatabaseName}", appId, existing.DatabaseName);
                return new DatabaseProvisionResult
                {
                    Success = true,
                    DatabaseName = existing.DatabaseName,
                    ConnectionString = existing.ConnectionString
                };
            }

            var databaseName = SanitizeDatabaseName($"appdb_{appId}");

            // Call Provisioning Agent
            _logger.LogInformation("Creating database via Provisioning Agent: {DatabaseName}", databaseName);
            var agentResult = await _agent.ProvisionDatabaseAsync(appId, databaseName, schemaJson, seedJson, cancellationToken);
            
            if (!agentResult.Success)
            {
                 return new DatabaseProvisionResult
                {
                    Success = false,
                    ErrorMessage = agentResult.ErrorMessage ?? "Provisioning Agent returned failure"
                };
            }

            var connectionStringRef = agentResult.Update?.Mongo?.ConnectionStringSecretRef;
            var finalDatabaseName = agentResult.Update?.Mongo?.DatabaseName ?? databaseName;

            if (string.IsNullOrWhiteSpace(connectionStringRef))
            {
                 return new DatabaseProvisionResult
                {
                    Success = false,
                    ErrorMessage = "Provisioning Agent did not return a connection string ref"
                };
            }

            var now = DateTime.UtcNow;

            var filter = Builders<ConnectionStringRecord>.Filter.Eq(x => x.AppId, appId);
            var update = Builders<ConnectionStringRecord>.Update
                .Set(x => x.AppId, appId)
                .Set(x => x.ConnectionString, connectionStringRef)
                .Set(x => x.DatabaseName, finalDatabaseName)
                .Set(x => x.Status, "active")
                .SetOnInsert(x => x.CreatedAt, now)
                .Set(x => x.LastUpdatedAt, now);

            await _connectionStrings.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);

            _logger.LogInformation("Provisioned database {DatabaseName} for app {AppId} via Agent", finalDatabaseName, appId);

            return new DatabaseProvisionResult
            {
                Success = true,
                DatabaseName = finalDatabaseName,
                ConnectionString = connectionStringRef
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision database for app {AppId}", appId);
            return new DatabaseProvisionResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> ApplySchemaAsync(string appId, SchemaDefinition schema, CancellationToken cancellationToken)
    {
        try
        {
            var record = await _connectionStrings.Find(x => x.AppId == appId)
                .FirstOrDefaultAsync(cancellationToken);

            if (record is null || string.IsNullOrWhiteSpace(record.DatabaseName))
            {
                _logger.LogError("No database found for app {AppId}", appId);
                return false;
            }

            var db = _adminClient.GetDatabase(record.DatabaseName);

            foreach (var table in schema.Tables)
            {
                var tableName = (table.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    continue;
                }

                var validator = BuildValidator(table);

                try
                {
                    await db.RunCommandAsync<BsonDocument>(new BsonDocument
                    {
                        { "create", tableName },
                        { "validator", validator },
                        { "validationLevel", "moderate" },
                        { "validationAction", "error" }
                    }, cancellationToken: cancellationToken);
                }
                catch (MongoCommandException ex) when (ex.Code == 48)
                {
                    await db.RunCommandAsync<BsonDocument>(new BsonDocument
                    {
                        { "collMod", tableName },
                        { "validator", validator },
                        { "validationLevel", "moderate" },
                        { "validationAction", "error" }
                    }, cancellationToken: cancellationToken);
                }

                await CreateIndexesAsync(db, table, cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply schema for app {AppId}", appId);
            return false;
        }
    }

    public async Task<bool> SeedDatabaseAsync(string appId, SeedData seedData, CancellationToken cancellationToken)
    {
        try
        {
            var record = await _connectionStrings.Find(x => x.AppId == appId)
                .FirstOrDefaultAsync(cancellationToken);

            if (record is null || string.IsNullOrWhiteSpace(record.DatabaseName))
            {
                _logger.LogError("No database found for app {AppId}", appId);
                return false;
            }

            var db = _adminClient.GetDatabase(record.DatabaseName);

            foreach (var collectionData in seedData.Collections)
            {
                var name = (collectionData.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (collectionData.DocumentsJson is null || collectionData.DocumentsJson.Count == 0)
                {
                    continue;
                }

                var collection = db.GetCollection<BsonDocument>(name);

                var docs = new List<BsonDocument>();
                foreach (var json in collectionData.DocumentsJson)
                {
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        continue;
                    }

                    var doc = BsonDocument.Parse(json);
                    docs.Add(ProcessSeedDocument(doc));
                }

                if (docs.Count == 0)
                {
                    continue;
                }

                try
                {
                    await collection.InsertManyAsync(docs, new InsertManyOptions { IsOrdered = false }, cancellationToken);
                }
                catch (MongoBulkWriteException<BsonDocument> ex) when (ex.WriteErrors.All(e => e.Category == ServerErrorCategory.DuplicateKey))
                {
                    // idempotency: ignore duplicates
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed database for app {AppId}", appId);
            return false;
        }
    }

    public async Task<string?> GetConnectionStringAsync(string appId, CancellationToken cancellationToken)
    {
        var record = await _connectionStrings.Find(x => x.AppId == appId)
            .FirstOrDefaultAsync(cancellationToken);

        var connectionString = record?.ConnectionString;
        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    private string BuildAppConnectionString(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(_adminConnectionString))
        {
            return string.Empty;
        }

        var builder = new MongoUrlBuilder(_adminConnectionString)
        {
            DatabaseName = databaseName
        };

        return builder.ToString();
    }

    private static string SanitizeDatabaseName(string name)
    {
        var sanitized = Regex.Replace((name ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9_]", "_");
        sanitized = Regex.Replace(sanitized, @"_+", "_").Trim('_');

        return sanitized.Length > 64 ? sanitized[..64] : sanitized;
    }

    private static BsonDocument BuildValidator(TableDefinition table)
    {
        var properties = new BsonDocument();
        var required = new BsonArray();

        foreach (var column in table.Columns)
        {
            var columnName = (column.Name ?? string.Empty).Trim();
            var columnType = (column.Type ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(columnName) || string.IsNullOrWhiteSpace(columnType))
            {
                continue;
            }

            var fieldValidator = new BsonDocument();

            if (string.Equals(columnType, "Array", StringComparison.OrdinalIgnoreCase))
            {
                fieldValidator["bsonType"] = "array";
                var itemType = string.IsNullOrWhiteSpace(column.ItemType) ? "string" : MapDataType(column.ItemType);
                fieldValidator["items"] = new BsonDocument { { "bsonType", itemType } };
            }
            else
            {
                fieldValidator["bsonType"] = MapDataType(columnType);
            }

            if (column.Constraints is not null)
            {
                foreach (var constraint in column.Constraints)
                {
                    if (constraint.StartsWith("Check: IN", StringComparison.OrdinalIgnoreCase))
                    {
                        var values = ExtractEnumValues(constraint);
                        if (values.Count > 0)
                        {
                            fieldValidator["enum"] = new BsonArray(values);
                        }
                    }

                    if (constraint.Equals("Not Null", StringComparison.OrdinalIgnoreCase)
                        || constraint.Equals("PK", StringComparison.OrdinalIgnoreCase))
                    {
                        required.Add(columnName);
                    }
                }
            }

            properties[columnName] = fieldValidator;
        }

        var schema = new BsonDocument
        {
            { "bsonType", "object" },
            { "properties", properties }
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return new BsonDocument { { "$jsonSchema", schema } };
    }

    private async Task CreateIndexesAsync(IMongoDatabase db, TableDefinition table, CancellationToken cancellationToken)
    {
        var collection = db.GetCollection<BsonDocument>(table.Name);

        var existingIndexes = await collection.Indexes.ListAsync(cancellationToken);
        var existing = await existingIndexes.ToListAsync(cancellationToken);
        var existingNames = existing
            .Where(i => i.Contains("name"))
            .Select(i => i["name"].AsString)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (table.Constraints?.Unique is not null)
        {
            foreach (var field in table.Constraints.Unique)
            {
                if (string.IsNullOrWhiteSpace(field))
                {
                    continue;
                }

                var indexName = $"{field}_unique";
                if (existingNames.Contains(indexName))
                {
                    continue;
                }

                var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending(field);
                await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
                    indexKeys,
                    new CreateIndexOptions { Unique = true, Name = indexName }),
                    cancellationToken: cancellationToken);
            }
        }

        if (table.Indices is not null)
        {
            foreach (var field in table.Indices)
            {
                if (string.IsNullOrWhiteSpace(field))
                {
                    continue;
                }

                var indexName = $"{field}_index";
                if (existingNames.Contains(indexName))
                {
                    continue;
                }

                var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending(field);
                await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
                    indexKeys,
                    new CreateIndexOptions { Name = indexName }),
                    cancellationToken: cancellationToken);
            }
        }
    }

    private static string MapDataType(string fieldType)
    {
        if (string.IsNullOrWhiteSpace(fieldType))
        {
            return "string";
        }

        var typeMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UUID"] = "string",
            ["String"] = "string",
            ["Text"] = "string",
            ["Integer"] = "int",
            ["NumberInt"] = "int",
            ["Timestamp"] = "date",
            ["DateTime"] = "date",
            ["JSON"] = "object",
            ["Boolean"] = "bool",
            ["Float"] = "double",
            ["Double"] = "double",
            ["Array"] = "array",
            ["Object"] = "object",
            ["ObjectId"] = "objectId",
            ["number"] = "double",
            ["int"] = "int",
            ["date"] = "date",
            ["bool"] = "bool"
        };

        if (typeMapping.TryGetValue(fieldType.Trim(), out var bsonType))
        {
            return bsonType;
        }

        var lower = fieldType.ToLowerInvariant();
        if (lower.Contains("time") || lower.Contains("date"))
        {
            return "date";
        }
        if (lower.Contains("num") || lower.Contains("int"))
        {
            return "double";
        }

        return "string";
    }

    private static List<string> ExtractEnumValues(string constraint)
    {
        var match = Regex.Match(constraint, @"IN\s*\(([^)]+)\)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return new List<string>();
        }

        return match.Groups[1].Value
            .Split(',')
            .Select(v => v.Trim().Trim('\'', '"'))
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
    }

    private static BsonDocument ProcessSeedDocument(BsonDocument doc)
    {
        var processed = new BsonDocument();
        foreach (var element in doc)
        {
            processed[element.Name] = ProcessBsonValue(element.Value);
        }

        return processed;
    }

    private static BsonValue ProcessBsonValue(BsonValue value)
    {
        if (value is BsonDocument doc)
        {
            if (doc.TryGetValue("$oid", out var oid) && oid.IsString)
            {
                return new ObjectId(oid.AsString);
            }

            if (doc.TryGetValue("$date", out var date) && date.IsString)
            {
                if (DateTime.TryParse(date.AsString, out var dt))
                {
                    return new BsonDateTime(dt.ToUniversalTime());
                }
            }

            var processedDoc = new BsonDocument();
            foreach (var el in doc)
            {
                processedDoc[el.Name] = ProcessBsonValue(el.Value);
            }

            return processedDoc;
        }

        if (value is BsonArray arr)
        {
            return new BsonArray(arr.Select(ProcessBsonValue));
        }

        if (value is BsonString str)
        {
            if (DateTime.TryParse(str.Value, out var dt))
            {
                if (dt.Kind == DateTimeKind.Unspecified)
                {
                    dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }

                return new BsonDateTime(dt.ToUniversalTime());
            }
        }

        return value;
    }
}
