namespace Plugins.API.DTOs;

public record InstallPluginRequest(
    string PluginId,
    Dictionary<string, object>? InitialSettings = null
);

public record UninstallPluginRequest(
    string PluginName
);

public record UpdatePluginSettingsRequest(
    string PluginName,
    Dictionary<string, object> Settings
);

public record TogglePluginRequest(
    string PluginName,
    bool Enabled
);

public record InstalledPluginDto(
    string InstallationId,
    string PluginId,
    string PluginName,
    string DisplayName,
    string InstalledVersion,
    string? LatestVersion,
    bool IsEnabled,
    bool HasUpdate,
    Dictionary<string, object> Settings,
    string Status,
    DateTime InstalledAt
);

public record AppPluginsResponse(
    string AppId,
    List<InstalledPluginDto> InstalledPlugins,
    List<PluginManifestDto> ActiveManifests
);
