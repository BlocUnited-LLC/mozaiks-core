using Plugins.API.Models;

namespace Plugins.API.Repository;

public interface IPluginRepository
{
    // Catalog operations
    Task<List<Plugin>> SearchPluginsAsync(string? query, string? category, List<string>? tags, int skip, int take, string sortBy, bool descending);
    Task<int> CountPluginsAsync(string? query, string? category, List<string>? tags);
    Task<Plugin?> GetPluginByIdAsync(string id);
    Task<Plugin?> GetPluginByNameAsync(string name);
    Task<Plugin> CreatePluginAsync(Plugin plugin);
    Task<Plugin> UpdatePluginAsync(Plugin plugin);
    Task DeletePluginAsync(string id);
    Task IncrementInstallCountAsync(string pluginId);

    // Installation operations
    Task<List<PluginInstallation>> GetAppInstallationsAsync(string appId);
    Task<PluginInstallation?> GetInstallationAsync(string appId, string pluginName);
    Task<PluginInstallation> CreateInstallationAsync(PluginInstallation installation);
    Task<PluginInstallation> UpdateInstallationAsync(PluginInstallation installation);
    Task DeleteInstallationAsync(string installationId);
}
