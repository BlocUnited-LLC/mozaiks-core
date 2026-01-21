using Plugins.API.DTOs;

namespace Plugins.API.Services;

public interface IPluginRegistryService
{
    Task<CatalogSearchResponse> SearchCatalogAsync(CatalogSearchRequest request);
    Task<PluginDetailDto?> GetPluginDetailAsync(string pluginId);
    Task<PluginDetailDto?> GetPluginByNameAsync(string pluginName);
    Task<List<string>> GetCategoriesAsync();
}

public interface IPluginInstallationService
{
    Task<AppPluginsResponse> GetAppPluginsAsync(string appId);
    Task<InstalledPluginDto> InstallPluginAsync(string appId, string userId, InstallPluginRequest request);
    Task UninstallPluginAsync(string appId, string pluginName);
    Task<InstalledPluginDto> TogglePluginAsync(string appId, string pluginName, bool enabled);
    Task<InstalledPluginDto> UpdateSettingsAsync(string appId, string pluginName, Dictionary<string, object> settings);
}
