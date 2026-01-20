namespace Mozaiks.Auditing;

public sealed class AdminAuditOptions
{
    public string ServiceName { get; set; } = string.Empty;
    public string CollectionName { get; set; } = AdminAuditConstants.DefaultCollectionName;

    /// <summary>
    /// Optional retention window enforced via TTL index on <c>timestamp</c>.
    /// Set to <c>null</c> or <c>&lt;= 0</c> to disable TTL.
    /// </summary>
    public int? RetentionDays { get; set; }
}

