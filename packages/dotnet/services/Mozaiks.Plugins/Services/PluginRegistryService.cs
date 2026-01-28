using Plugins.API.DTOs;
using Plugins.API.Models;
using Plugins.API.Repository;

namespace Plugins.API.Services;

public class PluginRegistryService : IPluginRegistryService
{
    private readonly IPluginRepository _repository;
    private readonly ILogger<PluginRegistryService> _logger;

    public PluginRegistryService(IPluginRepository repository, ILogger<PluginRegistryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CatalogSearchResponse> SearchCatalogAsync(CatalogSearchRequest request)
    {
        var skip = (request.Page - 1) * request.PageSize;

        var plugins = await _repository.SearchPluginsAsync(
            request.Query,
            request.Category,
            request.Tags,
            skip,
            request.PageSize,
            request.SortBy,
            request.Descending);

        var totalCount = await _repository.CountPluginsAsync(
            request.Query,
            request.Category,
            request.Tags);

        var summaries = plugins.Select(MapToSummary).ToList();

        return new CatalogSearchResponse(summaries, totalCount, request.Page, request.PageSize);
    }

    public async Task<PluginDetailDto?> GetPluginDetailAsync(string pluginId)
    {
        var plugin = await _repository.GetPluginByIdAsync(pluginId);
        return plugin == null ? null : MapToDetail(plugin);
    }

    public async Task<PluginDetailDto?> GetPluginByNameAsync(string pluginName)
    {
        var plugin = await _repository.GetPluginByNameAsync(pluginName);
        return plugin == null ? null : MapToDetail(plugin);
    }

    public Task<List<string>> GetCategoriesAsync()
    {
        return Task.FromResult(new List<string>
        {
            "Content",
            "E-Commerce",
            "Communication",
            "Analytics",
            "Marketing",
            "Productivity",
            "Social",
            "Integrations",
            "AI",
            "Other"
        });
    }

    private static PluginSummaryDto MapToSummary(Plugin plugin)
    {
        return new PluginSummaryDto(
            plugin.Id,
            plugin.Name,
            plugin.DisplayName,
            plugin.Description,
            plugin.Version,
            plugin.Author,
            plugin.Icon,
            plugin.Category,
            plugin.Tags,
            plugin.InstallCount,
            plugin.Rating,
            plugin.RequiredTier,
            plugin.IsCore
        );
    }

    private static PluginDetailDto MapToDetail(Plugin plugin)
    {
        return new PluginDetailDto(
            plugin.Id,
            plugin.Name,
            plugin.DisplayName,
            plugin.Description,
            plugin.Version,
            plugin.Author,
            plugin.Icon,
            plugin.Category,
            plugin.Tags,
            MapManifest(plugin.Manifest),
            plugin.InstallCount,
            plugin.Rating,
            plugin.RequiredTier,
            plugin.IsCore,
            plugin.CreatedAt,
            plugin.UpdatedAt
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
