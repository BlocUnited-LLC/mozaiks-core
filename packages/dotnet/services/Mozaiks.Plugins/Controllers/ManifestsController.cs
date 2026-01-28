using Microsoft.AspNetCore.Mvc;
using Plugins.API.DTOs;
using Plugins.API.Services;

namespace Plugins.API.Controllers;

/// <summary>
/// Get plugin manifests for runtime loading (used by frontend shell)
/// </summary>
[ApiController]
[Route("api/plugins/manifests")]
public class ManifestsController : ControllerBase
{
    private readonly IPluginInstallationService _installationService;
    private readonly IPluginRegistryService _registryService;
    private readonly ILogger<ManifestsController> _logger;

    public ManifestsController(
        IPluginInstallationService installationService,
        IPluginRegistryService registryService,
        ILogger<ManifestsController> logger)
    {
        _installationService = installationService;
        _registryService = registryService;
        _logger = logger;
    }

    /// <summary>
    /// Get combined manifest for an app (all active plugins merged)
    /// Used by the frontend shell to configure routes, navigation, and widgets
    /// </summary>
    [HttpGet("{appId}")]
    public async Task<ActionResult<RuntimeManifestResponse>> GetRuntimeManifest(string appId)
    {
        var response = await _installationService.GetAppPluginsAsync(appId);

        var routes = response.ActiveManifests
            .SelectMany(m => m.Routes)
            .OrderBy(r => r.Path)
            .ToList();

        var navigation = response.ActiveManifests
            .SelectMany(m => m.Navigation)
            .OrderBy(n => n.Order)
            .ToList();

        var widgets = response.ActiveManifests
            .SelectMany(m => m.Widgets)
            .OrderBy(w => w.Order)
            .ToList();

        var permissions = response.ActiveManifests
            .SelectMany(m => m.Permissions)
            .Distinct()
            .ToList();

        return Ok(new RuntimeManifestResponse(
            appId,
            routes,
            navigation,
            widgets,
            permissions,
            response.InstalledPlugins.Select(p => p.PluginName).ToList()
        ));
    }
}

public record RuntimeManifestResponse(
    string AppId,
    List<PluginRouteDto> Routes,
    List<PluginNavItemDto> Navigation,
    List<PluginWidgetDto> Widgets,
    List<string> Permissions,
    List<string> InstalledPlugins
);
