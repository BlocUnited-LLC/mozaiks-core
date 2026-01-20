using System.Text;
using System.Text.Json;
using AuthServer.Api.DTOs;
using MongoDB.Driver;

namespace AuthServer.Api.Services;

/// <summary>
/// Interface for the scaffold generation service.
/// This service owns all framework-specific file generation logic,
/// migrated from project-aid-v2's FileManager.
/// </summary>
public interface IScaffoldService
{
    /// <summary>
    /// Generate a complete scaffold for an app based on its tech stack.
    /// </summary>
    Task<GenerateScaffoldResponse> GenerateScaffoldAsync(string appId, GenerateScaffoldRequest request);

    /// <summary>
    /// Get supported tech stacks (for UI selection or validation).
    /// </summary>
    Task<SupportedTechStacksResponse> GetSupportedTechStacksAsync();

    /// <summary>
    /// Validate that a tech stack is supported.
    /// </summary>
    Task<(bool IsValid, string? Error)> ValidateTechStackAsync(TechStackSpec techStack);
}

/// <summary>
/// Service that generates complete app scaffolds including all framework-specific boilerplate.
/// This is the backend equivalent of project-aid-v2's FileManager.
/// 
/// Responsibilities:
/// - Generate framework-specific boilerplate files (index.html, env.js, nginx.conf, etc.)
/// - Generate __init__.py files for Python backends
/// - Generate package.json / requirements.txt with proper dependency handling
/// - Generate Dockerfiles and workflows (delegates to DeploymentTemplateService)
/// - Filter Python built-in modules from requirements
/// - Handle package conflicts (bson vs pymongo)
/// </summary>
public sealed class ScaffoldService : IScaffoldService
{
    private readonly ILogger<ScaffoldService> _logger;
    private readonly MozaiksAppService _appService;
    private readonly IDeploymentTemplateService _templateService;
    private readonly IMozaiksCoreService _mozaiksCoreService;
    private readonly IMongoDatabase _database;

    // Known package conflicts (from FileManager)
    private static readonly Dictionary<string, string> KnownConflicts = new()
    {
        { "bson", "pymongo" }  // bson is included in pymongo, standalone bson conflicts
    };

    // Python built-in modules to filter from requirements.txt
    private static readonly HashSet<string> PythonBuiltins = new(StringComparer.OrdinalIgnoreCase)
    {
        "os", "sys", "json", "re", "math", "datetime", "time", "random", "collections",
        "itertools", "functools", "typing", "pathlib", "logging", "asyncio", "io",
        "abc", "copy", "enum", "hashlib", "base64", "uuid", "threading", "subprocess",
        "shutil", "tempfile", "glob", "fnmatch", "pickle", "sqlite3", "csv", "xml",
        "html", "http", "urllib", "socket", "email", "mimetypes", "codecs", "struct",
        "array", "bisect", "heapq", "operator", "contextlib", "dataclasses", "secrets",
        "traceback", "warnings", "inspect", "dis", "gc", "weakref", "types", "importlib",
        "pkgutil", "pprint", "textwrap", "string", "difflib", "decimal", "fractions",
        "statistics", "cmath", "zlib", "gzip", "bz2", "lzma", "zipfile", "tarfile"
    };

    // Default supported frameworks (hardcoded for MVP, can move to MongoDB later)
    private static readonly List<FrameworkConfig> DefaultFrameworkConfigs = new()
    {
        // React (default frontend)
        new FrameworkConfig
        {
            Type = "frontend",
            Framework = "react",
            DisplayName = "React",
            Runtime = "node:18-alpine",
            Language = "javascript",
            DefaultPort = 3000,
            EntryPoint = "index.html",
            ImportFile = "package.json",
            BasePackages = new() { "react", "react-dom", "react-scripts", "react-router-dom" },
            DirectoryStructure = new()
            {
                { "public", new() { "index.html", "env.js", "favicon.ico", "nginx.conf", "entrypoint.sh" } },
                { "src", new() { "App.js", "index.js" } }
            },
            SpecialFiles = new()
            {
                new() { Path = "public/index.html", Template = "react_index_html", Required = true, Description = "React HTML entry point with env.js reference" },
                new() { Path = "public/env.js", Template = "react_env_js", Required = true, Description = "Runtime environment variables injection" },
                new() { Path = "public/nginx.conf", Template = "nginx_spa_conf", Required = true, Description = "Nginx config for SPA routing" },
                new() { Path = "public/entrypoint.sh", Template = "entrypoint_sh", Required = true, Description = "Docker entrypoint for env var substitution" },
                new() { Path = "public/favicon.ico", Template = "empty_favicon", Required = false, Description = "Placeholder favicon" }
            },
            GenerateInitFiles = false
        },
        // FastAPI (default backend)
        new FrameworkConfig
        {
            Type = "backend",
            Framework = "fastapi",
            DisplayName = "FastAPI",
            Runtime = "python:3.11-slim",
            Language = "python",
            DefaultPort = 8000,
            EntryPoint = "main.py",
            ImportFile = "requirements.txt",
            BasePackages = new() { "fastapi", "uvicorn", "python-dotenv", "pydantic" },
            ConflictingPackages = new() { { "bson", "pymongo" } },
            DirectoryStructure = new()
            {
                { "routers", new() { "__init__.py" } },
                { "models", new() { "__init__.py" } },
                { "services", new() { "__init__.py" } },
                { "schemas", new() { "__init__.py" } }
            },
            GenerateInitFiles = true
        },
        // Flask (alternative backend)
        new FrameworkConfig
        {
            Type = "backend",
            Framework = "flask",
            DisplayName = "Flask",
            Runtime = "python:3.11-slim",
            Language = "python",
            DefaultPort = 5000,
            EntryPoint = "app.py",
            ImportFile = "requirements.txt",
            BasePackages = new() { "flask", "python-dotenv", "gunicorn" },
            ConflictingPackages = new() { { "bson", "pymongo" } },
            GenerateInitFiles = true
        },
        // Streamlit (alternative frontend for data apps)
        new FrameworkConfig
        {
            Type = "frontend",
            Framework = "streamlit",
            DisplayName = "Streamlit",
            Runtime = "python:3.11-slim",
            Language = "python",
            DefaultPort = 8501,
            EntryPoint = "app.py",
            ImportFile = "requirements.txt",
            BasePackages = new() { "streamlit", "pandas", "numpy" },
            ConflictingPackages = new() { { "bson", "pymongo" } },
            GenerateInitFiles = false
        }
    };

    public ScaffoldService(
        ILogger<ScaffoldService> logger,
        MozaiksAppService appService,
        IDeploymentTemplateService templateService,
        IMozaiksCoreService mozaiksCoreService,
        IMongoDatabase database)
    {
        _logger = logger;
        _appService = appService;
        _templateService = templateService;
        _mozaiksCoreService = mozaiksCoreService;
        _database = database;
    }

    public async Task<GenerateScaffoldResponse> GenerateScaffoldAsync(string appId, GenerateScaffoldRequest request)
    {
        var response = new GenerateScaffoldResponse { AppId = appId };

        try
        {
            // 1. Get app info to determine tech stack
            var app = await _appService.GetByIdAsync(appId);
            if (app == null)
            {
                response.Error = $"App not found: {appId}";
                return response;
            }

            // Use override tech stack or default (MozaiksAppModel doesn't have TechStack yet)
            // For now, we always use the override if provided, else defaults
            var techStack = request.TechStackOverride ?? GetDefaultTechStack();
            
            response.TechStack = techStack;

            // Get framework configs
            var frontendConfig = GetFrameworkConfig("frontend", techStack.Frontend?.Framework ?? "react");
            var backendConfig = GetFrameworkConfig("backend", techStack.Backend?.Framework ?? "fastapi");

            var files = new List<ScaffoldFile>();

            // 2. Generate boilerplate files
            if (request.IncludeBoilerplate)
            {
                if (frontendConfig != null)
                {
                    files.AddRange(GenerateBoilerplateFiles(frontendConfig, "frontend", app.Name));
                }
            }

            // 3. Generate __init__.py files for Python
            if (request.IncludeInitFiles && backendConfig?.GenerateInitFiles == true)
            {
                files.AddRange(GenerateInitFiles(backendConfig, "backend"));
            }

            // 4. Generate dependency files (package.json / requirements.txt)
            var dependencies = request.Dependencies ?? new Dictionary<string, List<string>>();
            
            if (frontendConfig != null)
            {
                var frontendDeps = dependencies.GetValueOrDefault("frontend", new List<string>());
                files.Add(GenerateDependencyFile(frontendConfig, frontendDeps));
            }

            if (backendConfig != null)
            {
                var backendDeps = dependencies.GetValueOrDefault("backend", new List<string>());
                files.Add(GenerateDependencyFile(backendConfig, backendDeps));
            }

            // 5. Generate Dockerfiles and workflow (delegate to existing service)
            if (request.IncludeDockerfiles || request.IncludeWorkflow)
            {
                var templateInput = new GenerateTemplatesInput
                {
                    AppId = appId,
                    AppName = app.Name,
                    TechStack = MapToTechStackInput(techStack),
                    IncludeDockerfiles = request.IncludeDockerfiles,
                    IncludeWorkflow = request.IncludeWorkflow
                };

                var templateResult = _templateService.GenerateTemplates(templateInput);
                if (templateResult.Success)
                {
                    foreach (var templateFile in templateResult.Files)
                    {
                        files.Add(new ScaffoldFile
                        {
                            Path = templateFile.Path,
                            ContentBase64 = Convert.ToBase64String(templateFile.Content),
                            Category = templateFile.Path.Contains("Dockerfile") ? "dockerfile" : "workflow",
                            Description = templateFile.Description,
                            OverwriteIfExists = true
                        });
                    }
                }
            }

            // 6. Include MozaiksCore SDK files if requested
            if (request.IncludeMozaiksCore && _mozaiksCoreService.IsConfigured)
            {
                try
                {
                    var mozaiksCoreFiles = await _mozaiksCoreService.GetMozaiksCoreFilesAsync();
                    if (mozaiksCoreFiles.Count > 0)
                    {
                        _logger.LogInformation("Adding {Count} MozaiksCore SDK files to scaffold", mozaiksCoreFiles.Count);
                        
                        foreach (var (path, content) in mozaiksCoreFiles)
                        {
                            files.Add(new ScaffoldFile
                            {
                                Path = path,
                                ContentBase64 = Convert.ToBase64String(content),
                                Category = "mozaikscore",
                                Description = "MozaiksCore SDK file",
                                OverwriteIfExists = false // Don't overwrite user customizations
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to include MozaiksCore files, continuing without them");
                    // Non-fatal: continue without MozaiksCore files
                }
            }

            response.Files = files;
            response.Success = true;
            response.Summary = new ScaffoldSummary
            {
                TotalFiles = files.Count,
                Categories = files.GroupBy(f => f.Category).ToDictionary(g => g.Key, g => g.Count()),
                FrontendFramework = frontendConfig?.Framework,
                BackendFramework = backendConfig?.Framework
            };

            _logger.LogInformation(
                "Generated scaffold for app {AppId}: {FileCount} files ({Categories})",
                appId, files.Count, string.Join(", ", response.Summary.Categories.Select(kv => $"{kv.Key}:{kv.Value}")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate scaffold for app {AppId}", appId);
            response.Error = ex.Message;
        }

        return response;
    }

    public Task<SupportedTechStacksResponse> GetSupportedTechStacksAsync()
    {
        var response = new SupportedTechStacksResponse
        {
            Frontend = DefaultFrameworkConfigs
                .Where(c => c.Type == "frontend")
                .Select(c => new SupportedFramework
                {
                    Framework = c.Framework,
                    DisplayName = c.DisplayName ?? c.Framework,
                    Language = c.Language,
                    DefaultPort = c.DefaultPort,
                    IsDefault = c.Framework == "react"
                })
                .ToList(),
            Backend = DefaultFrameworkConfigs
                .Where(c => c.Type == "backend")
                .Select(c => new SupportedFramework
                {
                    Framework = c.Framework,
                    DisplayName = c.DisplayName ?? c.Framework,
                    Language = c.Language,
                    DefaultPort = c.DefaultPort,
                    IsDefault = c.Framework == "fastapi"
                })
                .ToList(),
            Databases = new()
            {
                new() { Type = "mongodb", DisplayName = "MongoDB", Provider = "Atlas", IsDefault = true }
            },
            DefaultStack = GetDefaultTechStack()
        };

        return Task.FromResult(response);
    }

    public Task<(bool IsValid, string? Error)> ValidateTechStackAsync(TechStackSpec techStack)
    {
        var errors = new List<string>();

        if (techStack.Frontend != null && !string.IsNullOrEmpty(techStack.Frontend.Framework))
        {
            var config = GetFrameworkConfig("frontend", techStack.Frontend.Framework);
            if (config == null)
            {
                errors.Add($"Unsupported frontend framework: {techStack.Frontend.Framework}. Supported: react, streamlit");
            }
        }

        if (techStack.Backend != null && !string.IsNullOrEmpty(techStack.Backend.Framework))
        {
            var config = GetFrameworkConfig("backend", techStack.Backend.Framework);
            if (config == null)
            {
                errors.Add($"Unsupported backend framework: {techStack.Backend.Framework}. Supported: fastapi, flask");
            }
        }

        if (errors.Count > 0)
        {
            return Task.FromResult<(bool, string?)>((false, string.Join("; ", errors)));
        }

        return Task.FromResult<(bool, string?)>((true, null));
    }

    #region Private Methods - Boilerplate Generation

    private List<ScaffoldFile> GenerateBoilerplateFiles(FrameworkConfig config, string component, string? appName)
    {
        var files = new List<ScaffoldFile>();

        if (config.SpecialFiles == null) return files;

        foreach (var specialFile in config.SpecialFiles)
        {
            var content = GenerateTemplateContent(specialFile.Template, appName, config);
            if (content != null)
            {
                files.Add(new ScaffoldFile
                {
                    Path = $"{component}/{specialFile.Path}",
                    ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
                    Category = "boilerplate",
                    Description = specialFile.Description,
                    OverwriteIfExists = false
                });
            }
        }

        return files;
    }

    private string? GenerateTemplateContent(string template, string? appName, FrameworkConfig config)
    {
        return template switch
        {
            "react_index_html" => GenerateReactIndexHtml(appName),
            "react_env_js" => GenerateEnvJs(),
            "nginx_spa_conf" => GenerateNginxConf(config.DefaultPort),
            "entrypoint_sh" => GenerateEntrypointSh(),
            "empty_favicon" => "", // Empty placeholder
            _ => null
        };
    }

    private static string GenerateReactIndexHtml(string? appName)
    {
        var title = appName ?? "Mozaiks App";
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <link rel=""icon"" href=""%PUBLIC_URL%/favicon.ico"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <meta name=""theme-color"" content=""#000000"" />
    <meta name=""description"" content=""{title} - Built with Mozaiks"" />
    <link rel=""apple-touch-icon"" href=""%PUBLIC_URL%/logo192.png"" />
    <link rel=""manifest"" href=""%PUBLIC_URL%/manifest.json"" />
    <!-- Runtime environment variables -->
    <script src=""%PUBLIC_URL%/env.js""></script>
    <title>{title}</title>
</head>
<body>
    <noscript>You need to enable JavaScript to run this app.</noscript>
    <div id=""root""></div>
</body>
</html>";
    }

    private static string GenerateEnvJs()
    {
        return @"// Runtime environment variables - injected at container start
// This file is generated by Mozaiks and replaced by entrypoint.sh
window._env_ = {
    REACT_APP_BACKEND_URL: ""__BACKEND_URL__"",
    REACT_APP_API_URL: ""__API_URL__""
};";
    }

    private static string GenerateNginxConf(int port)
    {
        return $@"server {{
    listen {port};
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;

    # SPA routing - serve index.html for all routes
    location / {{
        try_files $uri $uri/ /index.html;
    }}

    # Cache static assets
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {{
        expires 1y;
        add_header Cache-Control ""public, immutable"";
    }}

    # Don't cache index.html or env.js
    location = /index.html {{
        expires -1;
        add_header Cache-Control ""no-store, no-cache, must-revalidate"";
    }}

    location = /env.js {{
        expires -1;
        add_header Cache-Control ""no-store, no-cache, must-revalidate"";
    }}

    # Gzip compression
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml text/javascript;
}}";
    }

    private static string GenerateEntrypointSh()
    {
        return @"#!/bin/sh
# Entrypoint script for React container
# Replaces placeholders in env.js with actual environment variables at runtime

# Replace placeholders in env.js
sed -i ""s|__BACKEND_URL__|${BACKEND_URL:-http://localhost:8000}|g"" /usr/share/nginx/html/env.js
sed -i ""s|__API_URL__|${API_URL:-http://localhost:8000/api}|g"" /usr/share/nginx/html/env.js

# Start nginx
exec nginx -g 'daemon off;'";
    }

    #endregion

    #region Private Methods - Init Files

    private List<ScaffoldFile> GenerateInitFiles(FrameworkConfig config, string component)
    {
        var files = new List<ScaffoldFile>();

        if (config.DirectoryStructure == null) return files;

        // Generate __init__.py for each directory in the structure
        foreach (var (folder, _) in config.DirectoryStructure)
        {
            files.Add(new ScaffoldFile
            {
                Path = $"{component}/{folder}/__init__.py",
                ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"# {folder} module\n")),
                Category = "init",
                Description = $"Python init file for {folder} module",
                OverwriteIfExists = false
            });
        }

        // Also add root __init__.py
        files.Add(new ScaffoldFile
        {
            Path = $"{component}/__init__.py",
            ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("# Backend root module\n")),
            Category = "init",
            Description = "Python init file for backend root",
            OverwriteIfExists = false
        });

        return files;
    }

    #endregion

    #region Private Methods - Dependency Files

    private ScaffoldFile GenerateDependencyFile(FrameworkConfig config, List<string> additionalDeps)
    {
        // Start with base packages
        var allDeps = new HashSet<string>(config.BasePackages, StringComparer.OrdinalIgnoreCase);

        // Add additional dependencies
        foreach (var dep in additionalDeps)
        {
            allDeps.Add(dep);
        }

        // Handle Python-specific filtering
        if (config.Language == "python")
        {
            allDeps = FilterPythonDependencies(allDeps, config);
        }

        string content;
        if (config.ImportFile == "package.json")
        {
            content = GeneratePackageJson(allDeps);
        }
        else if (config.ImportFile == "requirements.txt")
        {
            content = GenerateRequirementsTxt(allDeps);
        }
        else
        {
            content = string.Join("\n", allDeps);
        }

        return new ScaffoldFile
        {
            Path = $"{config.Type}/{config.ImportFile}",
            ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            Category = "dependencies",
            Description = $"{config.DisplayName} dependencies file",
            OverwriteIfExists = true
        };
    }

    private HashSet<string> FilterPythonDependencies(HashSet<string> deps, FrameworkConfig config)
    {
        var filtered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dep in deps)
        {
            // Skip Python built-ins
            var baseDep = dep.Split('.')[0]; // Handle submodules like os.path
            if (PythonBuiltins.Contains(baseDep))
            {
                _logger.LogDebug("Filtering out Python builtin: {Dep}", dep);
                continue;
            }

            filtered.Add(dep);
        }

        // Handle conflicts
        var conflicts = config.ConflictingPackages ?? KnownConflicts;
        foreach (var (conflictPkg, supersedingPkg) in conflicts)
        {
            if (filtered.Contains(conflictPkg) && filtered.Contains(supersedingPkg))
            {
                _logger.LogInformation(
                    "Removing conflicting package {Conflict} (superseded by {Superseding})",
                    conflictPkg, supersedingPkg);
                filtered.Remove(conflictPkg);
            }
        }

        // Ensure pymongo is included if motor is present
        if (filtered.Contains("motor") && !filtered.Contains("pymongo"))
        {
            _logger.LogInformation("Adding 'pymongo' because 'motor' requires it");
            filtered.Add("pymongo");
        }

        return filtered;
    }

    private static string GeneratePackageJson(HashSet<string> deps)
    {
        var dependencies = new Dictionary<string, string>();
        
        foreach (var dep in deps.OrderBy(d => d))
        {
            // Use specific versions for react-router-dom to avoid breaking changes
            var version = dep == "react-router-dom" ? "^6.0.0" : "latest";
            dependencies[dep] = version;
        }

        var packageJson = new
        {
            name = "frontend",
            version = "1.0.0",
            @private = true,
            dependencies = dependencies,
            scripts = new
            {
                start = "react-scripts start",
                build = "react-scripts build",
                test = "react-scripts test",
                eject = "react-scripts eject"
            },
            browserslist = new
            {
                production = new[] { ">0.2%", "not dead", "not op_mini all" },
                development = new[] { "last 1 chrome version", "last 1 firefox version", "last 1 safari version" }
            }
        };

        return JsonSerializer.Serialize(packageJson, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string GenerateRequirementsTxt(HashSet<string> deps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Generated by Mozaiks");
        sb.AppendLine("# Add version constraints as needed (e.g., package==1.0.0)");
        sb.AppendLine();

        foreach (var dep in deps.OrderBy(d => d))
        {
            sb.AppendLine(dep);
        }

        return sb.ToString();
    }

    #endregion

    #region Private Methods - Helpers

    private static FrameworkConfig? GetFrameworkConfig(string type, string framework)
    {
        return DefaultFrameworkConfigs.FirstOrDefault(c => 
            c.Type == type && 
            c.Framework.Equals(framework, StringComparison.OrdinalIgnoreCase));
    }

    private static TechStackSpec GetDefaultTechStack()
    {
        return new TechStackSpec
        {
            Frontend = new FrameworkSpec { Framework = "react", Language = "javascript", Port = 3000 },
            Backend = new FrameworkSpec { Framework = "fastapi", Language = "python", Port = 8000 },
            Database = new DatabaseSpec { Type = "mongodb", Provider = "Atlas" }
        };
    }

    private static TechStackInput MapToTechStackInput(TechStackSpec spec)
    {
        return new TechStackInput
        {
            Frontend = spec.Frontend != null ? new FrameworkInput
            {
                Framework = spec.Frontend.Framework,
                Language = spec.Frontend.Language,
                Port = spec.Frontend.Port ?? 3000
            } : null,
            Backend = spec.Backend != null ? new FrameworkInput
            {
                Framework = spec.Backend.Framework,
                Language = spec.Backend.Language,
                Port = spec.Backend.Port ?? 8000
            } : null,
            Database = spec.Database != null ? new DatabaseInput
            {
                Type = spec.Database.Type,
                Provider = spec.Database.Provider
            } : null
        };
    }

    #endregion
}
