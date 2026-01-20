using System.Text.Json.Serialization;

namespace AuthServer.Api.DTOs;

/// <summary>
/// Request to generate a complete app scaffold including all framework-specific boilerplate files.
/// This replaces the FileManager's framework-specific file generation logic.
/// </summary>
public sealed class GenerateScaffoldRequest
{
    /// <summary>
    /// User ID making the request (for S2S calls, use "mozaiksai" or "internal").
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Dependencies to include in package.json/requirements.txt.
    /// Key is component ("frontend" or "backend"), value is list of package names.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public Dictionary<string, List<string>>? Dependencies { get; set; }

    /// <summary>
    /// Whether to include Dockerfiles in output.
    /// </summary>
    [JsonPropertyName("includeDockerfiles")]
    public bool IncludeDockerfiles { get; set; } = true;

    /// <summary>
    /// Whether to include GitHub Actions workflow in output.
    /// </summary>
    [JsonPropertyName("includeWorkflow")]
    public bool IncludeWorkflow { get; set; } = true;

    /// <summary>
    /// Whether to include framework boilerplate files (index.html, env.js, nginx.conf, etc.).
    /// </summary>
    [JsonPropertyName("includeBoilerplate")]
    public bool IncludeBoilerplate { get; set; } = true;

    /// <summary>
    /// Whether to include __init__.py files for Python backends.
    /// </summary>
    [JsonPropertyName("includeInitFiles")]
    public bool IncludeInitFiles { get; set; } = true;

    /// <summary>
    /// Whether to include MozaiksCore SDK files.
    /// MozaiksCore is the base app foundation that provides runtime config, logging, 
    /// analytics hooks, and platform integration for every Mozaiks-generated app.
    /// Defaults to true for production-ready apps.
    /// </summary>
    [JsonPropertyName("includeMozaiksCore")]
    public bool IncludeMozaiksCore { get; set; } = true;

    /// <summary>
    /// Override the tech stack from what's stored in the app record.
    /// If null, uses the app's configured tech stack.
    /// </summary>
    [JsonPropertyName("techStackOverride")]
    public TechStackSpec? TechStackOverride { get; set; }
}

/// <summary>
/// Response containing all generated scaffold files.
/// </summary>
public sealed class GenerateScaffoldResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("appId")]
    public string AppId { get; set; } = string.Empty;

    [JsonPropertyName("techStack")]
    public TechStackSpec? TechStack { get; set; }

    /// <summary>
    /// All generated files with base64-encoded content.
    /// </summary>
    [JsonPropertyName("files")]
    public List<ScaffoldFile> Files { get; set; } = new();

    /// <summary>
    /// Summary of what was generated.
    /// </summary>
    [JsonPropertyName("summary")]
    public ScaffoldSummary? Summary { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class ScaffoldFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("contentBase64")]
    public string ContentBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Category: "boilerplate", "dockerfile", "workflow", "dependencies", "init"
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this file should overwrite existing files or skip if exists.
    /// </summary>
    [JsonPropertyName("overwriteIfExists")]
    public bool OverwriteIfExists { get; set; } = false;
}

public sealed class ScaffoldSummary
{
    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("categories")]
    public Dictionary<string, int> Categories { get; set; } = new();

    [JsonPropertyName("frontendFramework")]
    public string? FrontendFramework { get; set; }

    [JsonPropertyName("backendFramework")]
    public string? BackendFramework { get; set; }
}

/// <summary>
/// Framework configuration stored in MongoDB.
/// This is the schema for the FrameworkConfigs collection.
/// </summary>
public sealed class FrameworkConfig
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    /// <summary>
    /// Component type: "frontend" or "backend"
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Framework name (lowercase): "react", "fastapi", "flask", "streamlit", etc.
    /// </summary>
    [JsonPropertyName("framework")]
    public string Framework { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Runtime image for Docker (e.g., "python:3.11-slim", "node:18-alpine")
    /// </summary>
    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = string.Empty;

    /// <summary>
    /// Programming language: "python", "javascript", "typescript"
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Default port for the service.
    /// </summary>
    [JsonPropertyName("defaultPort")]
    public int DefaultPort { get; set; }

    /// <summary>
    /// Entry point file (e.g., "main.py", "index.js", "index.html")
    /// </summary>
    [JsonPropertyName("entryPoint")]
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>
    /// Import/dependency file name (e.g., "requirements.txt", "package.json")
    /// </summary>
    [JsonPropertyName("importFile")]
    public string ImportFile { get; set; } = string.Empty;

    /// <summary>
    /// Base packages that are always included.
    /// </summary>
    [JsonPropertyName("basePackages")]
    public List<string> BasePackages { get; set; } = new();

    /// <summary>
    /// Packages that conflict and should be removed (e.g., standalone "bson" when "pymongo" is present).
    /// Key is the conflicting package, value is the package that supersedes it.
    /// </summary>
    [JsonPropertyName("conflictingPackages")]
    public Dictionary<string, string>? ConflictingPackages { get; set; }

    /// <summary>
    /// Python built-in modules to filter out of requirements.txt.
    /// </summary>
    [JsonPropertyName("builtinModules")]
    public List<string>? BuiltinModules { get; set; }

    /// <summary>
    /// Directory structure for this framework.
    /// Key is folder name, value is list of expected files.
    /// </summary>
    [JsonPropertyName("directoryStructure")]
    public Dictionary<string, List<string>>? DirectoryStructure { get; set; }

    /// <summary>
    /// Special boilerplate files to generate.
    /// </summary>
    [JsonPropertyName("specialFiles")]
    public List<SpecialFileConfig>? SpecialFiles { get; set; }

    /// <summary>
    /// Whether to generate __init__.py files in subdirectories.
    /// </summary>
    [JsonPropertyName("generateInitFiles")]
    public bool GenerateInitFiles { get; set; }
}

public sealed class SpecialFileConfig
{
    /// <summary>
    /// Relative path for the file.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Template name to use for content generation.
    /// </summary>
    [JsonPropertyName("template")]
    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// Whether this file is required.
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    /// <summary>
    /// Description of what this file does.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Supported tech stack response for discovery.
/// </summary>
public sealed class SupportedTechStacksResponse
{
    [JsonPropertyName("frontend")]
    public List<SupportedFramework> Frontend { get; set; } = new();

    [JsonPropertyName("backend")]
    public List<SupportedFramework> Backend { get; set; } = new();

    [JsonPropertyName("databases")]
    public List<SupportedDatabase> Databases { get; set; } = new();

    [JsonPropertyName("defaultStack")]
    public TechStackSpec DefaultStack { get; set; } = new();
}

public sealed class SupportedFramework
{
    [JsonPropertyName("framework")]
    public string Framework { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("defaultPort")]
    public int DefaultPort { get; set; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }
}

public sealed class SupportedDatabase
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }
}
