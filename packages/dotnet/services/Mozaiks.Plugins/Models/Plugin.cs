using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Plugins.API.Models;

/// <summary>
/// Represents a plugin in the catalog
/// </summary>
public class Plugin
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Unique plugin identifier (e.g., "moz.app.blog")
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Human-readable display name
    /// </summary>
    [BsonElement("displayName")]
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Plugin description
    /// </summary>
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Current version (semver)
    /// </summary>
    [BsonElement("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Plugin author/publisher
    /// </summary>
    [BsonElement("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Icon identifier or URL
    /// </summary>
    [BsonElement("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// Plugin category for catalog browsing
    /// </summary>
    [BsonElement("category")]
    public string Category { get; set; } = "Other";

    /// <summary>
    /// Tags for search/filtering
    /// </summary>
    [BsonElement("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Plugin manifest containing routes, navigation, settings schema
    /// </summary>
    [BsonElement("manifest")]
    public PluginManifest Manifest { get; set; } = new();

    /// <summary>
    /// Whether this plugin is publicly available in the catalog
    /// </summary>
    [BsonElement("isPublished")]
    public bool IsPublished { get; set; } = false;

    /// <summary>
    /// Whether this is a core plugin (cannot be uninstalled)
    /// </summary>
    [BsonElement("isCore")]
    public bool IsCore { get; set; } = false;

    /// <summary>
    /// Required subscription tier to use this plugin (null = free)
    /// </summary>
    [BsonElement("requiredTier")]
    public string? RequiredTier { get; set; }

    /// <summary>
    /// Download/install count
    /// </summary>
    [BsonElement("installCount")]
    public int InstallCount { get; set; } = 0;

    /// <summary>
    /// Average rating (1-5)
    /// </summary>
    [BsonElement("rating")]
    public double Rating { get; set; } = 0;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Plugin manifest containing configuration for runtime loading
/// </summary>
public class PluginManifest
{
    /// <summary>
    /// Frontend entry point (relative path or package name)
    /// </summary>
    [BsonElement("frontendEntry")]
    public string? FrontendEntry { get; set; }

    /// <summary>
    /// Backend module path (Python module or API endpoint)
    /// </summary>
    [BsonElement("backendEntry")]
    public string? BackendEntry { get; set; }

    /// <summary>
    /// Routes this plugin provides
    /// </summary>
    [BsonElement("routes")]
    public List<PluginRoute> Routes { get; set; } = new();

    /// <summary>
    /// Navigation items to add to the sidebar
    /// </summary>
    [BsonElement("navigation")]
    public List<PluginNavItem> Navigation { get; set; } = new();

    /// <summary>
    /// Dashboard widgets this plugin provides
    /// </summary>
    [BsonElement("widgets")]
    public List<PluginWidget> Widgets { get; set; } = new();

    /// <summary>
    /// Settings schema for plugin configuration
    /// </summary>
    [BsonElement("settingsSchema")]
    public object? SettingsSchema { get; set; }

    /// <summary>
    /// Required permissions
    /// </summary>
    [BsonElement("permissions")]
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Dependencies on other plugins
    /// </summary>
    [BsonElement("dependencies")]
    public List<string> Dependencies { get; set; } = new();
}

public class PluginRoute
{
    [BsonElement("path")]
    public string Path { get; set; } = null!;

    [BsonElement("component")]
    public string Component { get; set; } = null!;

    [BsonElement("exact")]
    public bool Exact { get; set; } = true;

    [BsonElement("requiredRoles")]
    public List<string> RequiredRoles { get; set; } = new();
}

public class PluginNavItem
{
    [BsonElement("label")]
    public string Label { get; set; } = null!;

    [BsonElement("path")]
    public string Path { get; set; } = null!;

    [BsonElement("icon")]
    public string? Icon { get; set; }

    [BsonElement("order")]
    public int Order { get; set; } = 100;

    [BsonElement("section")]
    public string Section { get; set; } = "plugins";

    [BsonElement("requiredRoles")]
    public List<string> RequiredRoles { get; set; } = new();
}

public class PluginWidget
{
    [BsonElement("id")]
    public string Id { get; set; } = null!;

    [BsonElement("component")]
    public string Component { get; set; } = null!;

    [BsonElement("slot")]
    public string Slot { get; set; } = "dashboard";

    [BsonElement("order")]
    public int Order { get; set; } = 100;

    [BsonElement("defaultSize")]
    public string DefaultSize { get; set; } = "medium";
}
