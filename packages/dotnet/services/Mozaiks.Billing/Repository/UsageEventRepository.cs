using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Payment.API.Controllers;
using Payment.API.Infrastructure.Observability;

namespace Payment.API.Repository;

/// <summary>
/// MongoDB implementation of IUsageEventStore.
/// Stores token usage events from the AI runtime for billing and analytics.
/// </summary>
public class UsageEventRepository : IUsageEventStore
{
    private readonly IMongoCollection<UsageEventDocument> _collection;
    private readonly StructuredLogEmitter _logs;

    public UsageEventRepository(IMongoDatabase database, StructuredLogEmitter logs)
    {
        _collection = database.GetCollection<UsageEventDocument>("usage_events");
        _logs = logs;

        // Ensure indexes exist
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        // Index for querying by app_id + timestamp (most common query)
        var appTimestampIndex = Builders<UsageEventDocument>.IndexKeys
            .Ascending(x => x.AppId)
            .Descending(x => x.TimestampUtc);
        _collection.Indexes.CreateOne(new CreateIndexModel<UsageEventDocument>(
            appTimestampIndex,
            new CreateIndexOptions { Name = "app_id_timestamp" }));

        // Index for event_id uniqueness
        var eventIdIndex = Builders<UsageEventDocument>.IndexKeys.Ascending(x => x.EventId);
        _collection.Indexes.CreateOne(new CreateIndexModel<UsageEventDocument>(
            eventIdIndex,
            new CreateIndexOptions { Unique = true, Name = "event_id_unique" }));

        // TTL index to auto-expire old events (90 days)
        var ttlIndex = Builders<UsageEventDocument>.IndexKeys.Ascending(x => x.TimestampUtc);
        _collection.Indexes.CreateOne(new CreateIndexModel<UsageEventDocument>(
            ttlIndex,
            new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(90), Name = "timestamp_ttl" }));
    }

    public async Task RecordAsync(TokenUsageEventDto usageEvent, CancellationToken cancellationToken = default)
    {
        var document = new UsageEventDocument
        {
            EventId = usageEvent.EventId,
            AppId = usageEvent.AppId,
            UserId = usageEvent.UserId,
            WorkflowId = usageEvent.WorkflowId,
            SessionId = usageEvent.SessionId,
            ModelId = usageEvent.ModelId,
            TokensUsed = usageEvent.TokensUsed,
            InputTokens = usageEvent.InputTokens,
            OutputTokens = usageEvent.OutputTokens,
            TimestampUtc = usageEvent.TimestampUtc,
            Metadata = usageEvent.Metadata,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Idempotent - event already recorded
            _logs.Debug(
                "Usage.Event.Duplicate",
                new ActorContext { UserId = usageEvent.UserId ?? "anonymous" },
                new EntityContext { AppId = usageEvent.AppId },
                new { eventId = usageEvent.EventId });
        }
    }

    public async Task<UsageSummaryDto> GetSummaryAsync(
        string appId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<UsageEventDocument>.Filter.And(
            Builders<UsageEventDocument>.Filter.Eq(x => x.AppId, appId),
            Builders<UsageEventDocument>.Filter.Gte(x => x.TimestampUtc, startUtc),
            Builders<UsageEventDocument>.Filter.Lte(x => x.TimestampUtc, endUtc));

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "app_id", appId },
                { "timestamp_utc", new BsonDocument
                    {
                        { "$gte", startUtc },
                        { "$lte", endUtc }
                    }
                }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "total_tokens", new BsonDocument("$sum", "$tokens_used") },
                { "total_input_tokens", new BsonDocument("$sum", new BsonDocument("$ifNull", new BsonArray { "$input_tokens", 0L })) },
                { "total_output_tokens", new BsonDocument("$sum", new BsonDocument("$ifNull", new BsonArray { "$output_tokens", 0L })) },
                { "total_events", new BsonDocument("$sum", 1) }
            })
        };

        var aggregateResult = await _collection.Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .FirstOrDefaultAsync(cancellationToken);

        var summary = new UsageSummaryDto
        {
            AppId = appId,
            StartUtc = startUtc,
            EndUtc = endUtc
        };

        if (aggregateResult != null)
        {
            summary.TotalTokens = aggregateResult.GetValue("total_tokens", 0).ToInt64();
            summary.TotalInputTokens = aggregateResult.GetValue("total_input_tokens", 0).ToInt64();
            summary.TotalOutputTokens = aggregateResult.GetValue("total_output_tokens", 0).ToInt64();
            summary.TotalEvents = aggregateResult.GetValue("total_events", 0).ToInt32();
        }

        // Get tokens by model
        var modelPipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "app_id", appId },
                { "timestamp_utc", new BsonDocument { { "$gte", startUtc }, { "$lte", endUtc } } }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$model_id" },
                { "tokens", new BsonDocument("$sum", "$tokens_used") }
            })
        };

        var modelResults = await _collection.Aggregate<BsonDocument>(modelPipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        foreach (var result in modelResults)
        {
            var modelId = result.GetValue("_id", BsonNull.Value);
            if (!modelId.IsBsonNull)
            {
                summary.TokensByModel[modelId.AsString] = result.GetValue("tokens", 0).ToInt64();
            }
        }

        // Get tokens by workflow
        var workflowPipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "app_id", appId },
                { "timestamp_utc", new BsonDocument { { "$gte", startUtc }, { "$lte", endUtc } } }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$workflow_id" },
                { "tokens", new BsonDocument("$sum", "$tokens_used") }
            })
        };

        var workflowResults = await _collection.Aggregate<BsonDocument>(workflowPipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        foreach (var result in workflowResults)
        {
            var workflowId = result.GetValue("_id", BsonNull.Value);
            if (!workflowId.IsBsonNull)
            {
                summary.TokensByWorkflow[workflowId.AsString] = result.GetValue("tokens", 0).ToInt64();
            }
        }

        return summary;
    }

    public async Task<List<TokenUsageEventDto>> GetEventsAsync(
        string appId,
        DateTime startUtc,
        DateTime endUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<UsageEventDocument>.Filter.And(
            Builders<UsageEventDocument>.Filter.Eq(x => x.AppId, appId),
            Builders<UsageEventDocument>.Filter.Gte(x => x.TimestampUtc, startUtc),
            Builders<UsageEventDocument>.Filter.Lte(x => x.TimestampUtc, endUtc));

        var sort = Builders<UsageEventDocument>.Sort.Descending(x => x.TimestampUtc);

        var documents = await _collection
            .Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return documents.Select(d => new TokenUsageEventDto
        {
            EventId = d.EventId,
            AppId = d.AppId,
            UserId = d.UserId,
            WorkflowId = d.WorkflowId,
            SessionId = d.SessionId,
            ModelId = d.ModelId,
            TokensUsed = d.TokensUsed,
            InputTokens = d.InputTokens,
            OutputTokens = d.OutputTokens,
            TimestampUtc = d.TimestampUtc,
            Metadata = d.Metadata
        }).ToList();
    }
}

/// <summary>
/// MongoDB document for storing usage events.
/// </summary>
public class UsageEventDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("event_id")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("app_id")]
    public string AppId { get; set; } = string.Empty;

    [BsonElement("user_id")]
    public string? UserId { get; set; }

    [BsonElement("workflow_id")]
    public string? WorkflowId { get; set; }

    [BsonElement("session_id")]
    public string? SessionId { get; set; }

    [BsonElement("model_id")]
    public string? ModelId { get; set; }

    [BsonElement("tokens_used")]
    public long TokensUsed { get; set; }

    [BsonElement("input_tokens")]
    public long? InputTokens { get; set; }

    [BsonElement("output_tokens")]
    public long? OutputTokens { get; set; }

    [BsonElement("timestamp_utc")]
    public DateTime TimestampUtc { get; set; }

    [BsonElement("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [BsonElement("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
}
