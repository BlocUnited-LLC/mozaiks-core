namespace Plugins.API.DTOs;

public record CatalogSearchRequest(
    string? Query = null,
    string? Category = null,
    List<string>? Tags = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "installCount",
    bool Descending = true
);

public record CatalogSearchResponse(
    List<PluginSummaryDto> Plugins,
    int TotalCount,
    int Page,
    int PageSize
);

public record PluginSummaryDto(
    string Id,
    string Name,
    string DisplayName,
    string Description,
    string Version,
    string Author,
    string? Icon,
    string Category,
    List<string> Tags,
    int InstallCount,
    double Rating,
    string? RequiredTier,
    bool IsCore
);

public record PluginDetailDto(
    string Id,
    string Name,
    string DisplayName,
    string Description,
    string Version,
    string Author,
    string? Icon,
    string Category,
    List<string> Tags,
    PluginManifestDto Manifest,
    int InstallCount,
    double Rating,
    string? RequiredTier,
    bool IsCore,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record PluginManifestDto(
    string? FrontendEntry,
    string? BackendEntry,
    List<PluginRouteDto> Routes,
    List<PluginNavItemDto> Navigation,
    List<PluginWidgetDto> Widgets,
    object? SettingsSchema,
    List<string> Permissions,
    List<string> Dependencies
);

public record PluginRouteDto(
    string Path,
    string Component,
    bool Exact,
    List<string> RequiredRoles
);

public record PluginNavItemDto(
    string Label,
    string Path,
    string? Icon,
    int Order,
    string Section,
    List<string> RequiredRoles
);

public record PluginWidgetDto(
    string Id,
    string Component,
    string Slot,
    int Order,
    string DefaultSize
);
