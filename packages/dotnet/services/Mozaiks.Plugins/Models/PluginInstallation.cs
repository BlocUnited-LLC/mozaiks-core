using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Plugins.API.Models;

/// <summary>
/// Represents an installed plugin for a specific app/tenant
/// </summary>
public class PluginInstallation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// The app/tenant ID this installation belongs to
    /// </summary>
    [BsonElement("appId")]
    public string AppId { get; set; } = null!;

    /// <summary>
    /// Reference to the plugin
    /// </summary>
    [BsonElement("pluginId")]
    public string PluginId { get; set; } = null!;

    /// <summary>
    /// Plugin name for quick lookup
    /// </summary>
    [BsonElement("pluginName")]
    public string PluginName { get; set; } = null!;

    /// <summary>
    /// Installed version
    /// </summary>
    [BsonElement("installedVersion")]
    public string InstalledVersion { get; set; } = null!;

    /// <summary>
    /// Whether this plugin is currently enabled
    /// </summary>
    [BsonElement("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// App-specific settings for this plugin
    /// </summary>
    [BsonElement("settings")]
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// Installation status
    /// </summary>
    [BsonElement("status")]
    public InstallationStatus Status { get; set; } = InstallationStatus.Installed;

    /// <summary>
    /// User who installed the plugin
    /// </summary>
    [BsonElement("installedBy")]
    public string? InstalledBy { get; set; }

    [BsonElement("installedAt")]
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum InstallationStatus
{
    Pending,
    Installing,
    Installed,
    Failed,
    Uninstalling
}
