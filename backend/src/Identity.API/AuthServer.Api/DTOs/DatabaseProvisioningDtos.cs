namespace AuthServer.Api.DTOs;

using System.Text.Json;

/// <summary>
/// Request to provision a database for an app.
/// For S2S calls, userId is used for audit trail.
/// </summary>
public sealed class ProvisionDatabaseRequest
{
    /// <summary>
    /// User ID for audit trail (optional, defaults to "system" for S2S calls).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Raw Schema definition JSON string (output from AI agent).
    /// </summary>
    public string? SchemaJson { get; set; }

    /// <summary>
    /// Raw Seed data JSON string (output from AI agent).
    /// </summary>
    public string? SeedJson { get; set; }
}

/// <summary>
/// Request to apply schema definition to an app's database.
/// </summary>
public sealed class ApplySchemaRequest
{
    /// <summary>
    /// User ID for audit trail (optional, defaults to "system" for S2S calls).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The schema definition to apply.
    /// </summary>
    public SchemaDefinition? Schema { get; set; }
}

/// <summary>
/// Request to seed an app's database with initial data.
/// </summary>
public sealed class SeedDatabaseRequest
{
    /// <summary>
    /// User ID for audit trail (optional, defaults to "system" for S2S calls).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The seed data as JSON. Can be either:
    /// - { "CollectionName": [ {...}, {...} ] }
    /// - { "collections": { "CollectionName": [ {...}, {...} ] } }
    /// </summary>
    public JsonElement? SeedData { get; set; }
}

public sealed class SchemaDefinition
{
    public List<TableDefinition> Tables { get; set; } = new();
}

public sealed class TableDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<ColumnDefinition> Columns { get; set; } = new();
    public ConstraintsDefinition? Constraints { get; set; }
    public List<string> Indices { get; set; } = new();
}

public sealed class ColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? ItemType { get; set; }
    public List<string> Constraints { get; set; } = new();
}

public sealed class ConstraintsDefinition
{
    public List<string> Unique { get; set; } = new();
}

public sealed class SeedData
{
    public List<CollectionSeedData> Collections { get; set; } = new();
}

public sealed class CollectionSeedData
{
    public string Name { get; set; } = string.Empty;
    public List<string> DocumentsJson { get; set; } = new();
}

public sealed class DatabaseProvisionResult
{
    public bool Success { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string? ConnectionString { get; set; }
    public string? ErrorMessage { get; set; }
}

