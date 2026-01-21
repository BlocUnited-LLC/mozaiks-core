using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plugins.API.DTOs;
using Plugins.API.Services;

namespace Plugins.API.Controllers;

/// <summary>
/// Plugin catalog - browse and search available plugins
/// </summary>
[ApiController]
[Route("api/plugins/catalog")]
public class CatalogController : ControllerBase
{
    private readonly IPluginRegistryService _registryService;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(IPluginRegistryService registryService, ILogger<CatalogController> logger)
    {
        _registryService = registryService;
        _logger = logger;
    }

    /// <summary>
    /// Search the plugin catalog
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<CatalogSearchResponse>> SearchCatalog(
        [FromQuery] string? query = null,
        [FromQuery] string? category = null,
        [FromQuery] string? tags = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "installCount",
        [FromQuery] bool descending = true)
    {
        var tagList = string.IsNullOrEmpty(tags) 
            ? null 
            : tags.Split(',').Select(t => t.Trim()).ToList();

        var request = new CatalogSearchRequest(query, category, tagList, page, pageSize, sortBy, descending);
        var response = await _registryService.SearchCatalogAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Get plugin details by ID
    /// </summary>
    [HttpGet("{pluginId}")]
    [AllowAnonymous]
    public async Task<ActionResult<PluginDetailDto>> GetPlugin(string pluginId)
    {
        var plugin = await _registryService.GetPluginDetailAsync(pluginId);
        if (plugin == null)
        {
            return NotFound(new { error = "Plugin not found" });
        }
        return Ok(plugin);
    }

    /// <summary>
    /// Get plugin by name
    /// </summary>
    [HttpGet("by-name/{pluginName}")]
    [AllowAnonymous]
    public async Task<ActionResult<PluginDetailDto>> GetPluginByName(string pluginName)
    {
        var plugin = await _registryService.GetPluginByNameAsync(pluginName);
        if (plugin == null)
        {
            return NotFound(new { error = "Plugin not found" });
        }
        return Ok(plugin);
    }

    /// <summary>
    /// Get available categories
    /// </summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<ActionResult<List<string>>> GetCategories()
    {
        var categories = await _registryService.GetCategoriesAsync();
        return Ok(categories);
    }
}
