using Plugins.API.DTOs;
using Plugins.API.Models;
using Plugins.API.Repository;

namespace Plugins.API.Services;

public class PluginInstallationService : IPluginInstallationService
{
    private readonly IPluginRepository _repository;
    private readonly ILogger<PluginInstallationService> _logger;

    public PluginInstallationService(IPluginRepository repository, ILogger<PluginInstallationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AppPluginsResponse> GetAppPluginsAsync(string appId)
    {
        var installations = await _repository.GetAppInstallationsAsync(appId);
        var installedPlugins = new List<InstalledPluginDto>();
        var activeManifests = new List<PluginManifestDto>();

        foreach (var installation in installations)
        {
            var plugin = await _repository.GetPluginByIdAsync(installation.PluginId);
            if (plugin == null) continue;

            var hasUpdate = plugin.Version != installation.InstalledVersion;

            installedPlugins.Add(new InstalledPluginDto(
                installation.Id,
                installation.PluginId,
                installation.PluginName,
                plugin.DisplayName,
                installation.InstalledVersion,
                plugin.Version,
                installation.IsEnabled,
                hasUpdate,
                installation.Settings,
                installation.Status.ToString(),
                installation.InstalledAt
            ));

            if (installation.IsEnabled && installation.Status == InstallationStatus.Installed)
            {
                activeManifests.Add(MapManifest(plugin.Manifest));
            }
        }

        return new AppPluginsResponse(appId, installedPlugins, activeManifests);
    }

    public async Task<InstalledPluginDto> InstallPluginAsync(string appId, string userId, InstallPluginRequest request)
    {
        var plugin = await _repository.GetPluginByIdAsync(request.PluginId)
            ?? throw new InvalidOperationException($"Plugin {request.PluginId} not found");

        // Check if already installed
        var existing = await _repository.GetInstallationAsync(appId, plugin.Name);
        if (existing != null)
        {
            throw new InvalidOperationException($"Plugin {plugin.Name} is already installed");
        }

        // Check dependencies
        foreach (var dep in plugin.Manifest.Dependencies)
        {
            var depInstalled = await _repository.GetInstallationAsync(appId, dep);
            if (depInstalled == null)
            {
                throw new InvalidOperationException($"Required dependency {dep} is not installed");
            }
        }

        var installation = new PluginInstallation
        {
            AppId = appId,
            PluginId = plugin.Id,
            PluginName = plugin.Name,
            InstalledVersion = plugin.Version,
            IsEnabled = true,
            Settings = request.InitialSettings ?? new Dictionary<string, object>(),
            Status = InstallationStatus.Installed,
            InstalledBy = userId
        };

        await _repository.CreateInstallationAsync(installation);
        await _repository.IncrementInstallCountAsync(plugin.Id);

        _logger.LogInformation("Plugin {PluginName} installed for app {AppId} by {UserId}",
            plugin.Name, appId, userId);

        return new InstalledPluginDto(
            installation.Id,
            installation.PluginId,
            installation.PluginName,
            plugin.DisplayName,
            installation.InstalledVersion,
            plugin.Version,
            installation.IsEnabled,
            false,
            installation.Settings,
            installation.Status.ToString(),
            installation.InstalledAt
        );
    }

    public async Task UninstallPluginAsync(string appId, string pluginName)
    {
        var installation = await _repository.GetInstallationAsync(appId, pluginName)
            ?? throw new InvalidOperationException($"Plugin {pluginName} is not installed");

        var plugin = await _repository.GetPluginByIdAsync(installation.PluginId);
        if (plugin?.IsCore == true)
        {
            throw new InvalidOperationException("Cannot uninstall core plugins");
        }

        // Check if other plugins depend on this one
        var allInstallations = await _repository.GetAppInstallationsAsync(appId);
        foreach (var inst in allInstallations)
        {
            if (inst.PluginName == pluginName) continue;

            var otherPlugin = await _repository.GetPluginByIdAsync(inst.PluginId);
            if (otherPlugin?.Manifest.Dependencies.Contains(pluginName) == true)
            {
                throw new InvalidOperationException(
                    $"Cannot uninstall {pluginName}: plugin {otherPlugin.Name} depends on it");
            }
        }

        await _repository.DeleteInstallationAsync(installation.Id);

        _logger.LogInformation("Plugin {PluginName} uninstalled from app {AppId}", pluginName, appId);
    }

    public async Task<InstalledPluginDto> TogglePluginAsync(string appId, string pluginName, bool enabled)
    {
        var installation = await _repository.GetInstallationAsync(appId, pluginName)
            ?? throw new InvalidOperationException($"Plugin {pluginName} is not installed");

        var plugin = await _repository.GetPluginByIdAsync(installation.PluginId)
            ?? throw new InvalidOperationException($"Plugin definition not found");

        if (plugin.IsCore && !enabled)
        {
            throw new InvalidOperationException("Cannot disable core plugins");
        }

        installation.IsEnabled = enabled;
        await _repository.UpdateInstallationAsync(installation);

        _logger.LogInformation("Plugin {PluginName} {Action} for app {AppId}",
            pluginName, enabled ? "enabled" : "disabled", appId);

        return new InstalledPluginDto(
            installation.Id,
            installation.PluginId,
            installation.PluginName,
            plugin.DisplayName,
            installation.InstalledVersion,
            plugin.Version,
            installation.IsEnabled,
            plugin.Version != installation.InstalledVersion,
            installation.Settings,
            installation.Status.ToString(),
            installation.InstalledAt
        );
    }

    public async Task<InstalledPluginDto> UpdateSettingsAsync(string appId, string pluginName, Dictionary<string, object> settings)
    {
        var installation = await _repository.GetInstallationAsync(appId, pluginName)
            ?? throw new InvalidOperationException($"Plugin {pluginName} is not installed");

        var plugin = await _repository.GetPluginByIdAsync(installation.PluginId)
            ?? throw new InvalidOperationException($"Plugin definition not found");

        // Merge settings
        foreach (var kvp in settings)
        {
            installation.Settings[kvp.Key] = kvp.Value;
        }

        await _repository.UpdateInstallationAsync(installation);

        _logger.LogInformation("Plugin {PluginName} settings updated for app {AppId}", pluginName, appId);

        return new InstalledPluginDto(
            installation.Id,
            installation.PluginId,
            installation.PluginName,
            plugin.DisplayName,
            installation.InstalledVersion,
            plugin.Version,
            installation.IsEnabled,
            plugin.Version != installation.InstalledVersion,
            installation.Settings,
            installation.Status.ToString(),
            installation.InstalledAt
        );
    }

    private static PluginManifestDto MapManifest(PluginManifest manifest)
    {
        return new PluginManifestDto(
            manifest.FrontendEntry,
            manifest.BackendEntry,
            manifest.Routes.Select(r => new PluginRouteDto(r.Path, r.Component, r.Exact, r.RequiredRoles)).ToList(),
            manifest.Navigation.Select(n => new PluginNavItemDto(n.Label, n.Path, n.Icon, n.Order, n.Section, n.RequiredRoles)).ToList(),
            manifest.Widgets.Select(w => new PluginWidgetDto(w.Id, w.Component, w.Slot, w.Order, w.DefaultSize)).ToList(),
            manifest.SettingsSchema,
            manifest.Permissions,
            manifest.Dependencies
        );
    }
}
