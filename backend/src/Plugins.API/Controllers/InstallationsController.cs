using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mozaiks.Auth;
using Plugins.API.DTOs;
using Plugins.API.Services;

namespace Plugins.API.Controllers;

/// <summary>
/// Manage plugin installations for an app
/// </summary>
[ApiController]
[Route("api/plugins/apps/{appId}")]
[Authorize]
public class InstallationsController : ControllerBase
{
    private readonly IPluginInstallationService _installationService;
    private readonly IUserContextAccessor _userContext;
    private readonly ILogger<InstallationsController> _logger;

    public InstallationsController(
        IPluginInstallationService installationService,
        IUserContextAccessor userContext,
        ILogger<InstallationsController> logger)
    {
        _installationService = installationService;
        _userContext = userContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all installed plugins for an app
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AppPluginsResponse>> GetInstalledPlugins(string appId)
    {
        var response = await _installationService.GetAppPluginsAsync(appId);
        return Ok(response);
    }

    /// <summary>
    /// Get active manifests for runtime loading
    /// </summary>
    [HttpGet("manifests")]
    public async Task<ActionResult<List<PluginManifestDto>>> GetActiveManifests(string appId)
    {
        var response = await _installationService.GetAppPluginsAsync(appId);
        return Ok(response.ActiveManifests);
    }

    /// <summary>
    /// Install a plugin
    /// </summary>
    [HttpPost("install")]
    public async Task<ActionResult<InstalledPluginDto>> InstallPlugin(string appId, [FromBody] InstallPluginRequest request)
    {
        try
        {
            var user = _userContext.GetUser();
            var userId = user?.UserId ?? "unknown";
            var result = await _installationService.InstallPluginAsync(appId, userId, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Uninstall a plugin
    /// </summary>
    [HttpDelete("{pluginName}")]
    public async Task<IActionResult> UninstallPlugin(string appId, string pluginName)
    {
        try
        {
            await _installationService.UninstallPluginAsync(appId, pluginName);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Enable or disable a plugin
    /// </summary>
    [HttpPatch("{pluginName}/toggle")]
    public async Task<ActionResult<InstalledPluginDto>> TogglePlugin(string appId, string pluginName, [FromBody] TogglePluginRequest request)
    {
        try
        {
            var result = await _installationService.TogglePluginAsync(appId, pluginName, request.Enabled);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update plugin settings
    /// </summary>
    [HttpPatch("{pluginName}/settings")]
    public async Task<ActionResult<InstalledPluginDto>> UpdateSettings(string appId, string pluginName, [FromBody] UpdatePluginSettingsRequest request)
    {
        try
        {
            var result = await _installationService.UpdateSettingsAsync(appId, pluginName, request.Settings);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
