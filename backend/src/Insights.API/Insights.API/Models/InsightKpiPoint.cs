using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Insights.API.Models;

public sealed class InsightKpiPoint : DocumentBase
{
    [BsonElement("appId")]
    public string AppId { get; set; } = string.Empty;

    [BsonElement("env")]
    public string Env { get; set; } = string.Empty;

    [BsonElement("metric")]
    public string Metric { get; set; } = string.Empty;

    [BsonElement("bucket")]
    public string Bucket { get; set; } = string.Empty;

    [BsonElement("t")]
    public DateTime T { get; set; }

    [BsonElement("v")]
    public double V { get; set; }

    [BsonElement("unit")]
    public string Unit { get; set; } = string.Empty;

    [BsonElement("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}
