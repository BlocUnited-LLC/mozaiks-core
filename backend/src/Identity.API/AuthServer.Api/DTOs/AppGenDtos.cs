using System.Text.Json.Serialization;

namespace AuthServer.Api.DTOs;

/// <summary>
/// AppGen spec response - contains ONLY non-sensitive data needed for code generation.
/// NEVER include: API keys, GitHub tokens, Azure credentials, DockerHub tokens, connection strings.
/// </summary>
public sealed class AppGenSpecResponse
{
    [JsonPropertyName("appId")]
    public string AppId { get; set; } = string.Empty;

    [JsonPropertyName("appName")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("industry")]
    public string? Industry { get; set; }

    [JsonPropertyName("ownerUserId")]
    public string? OwnerUserId { get; set; }

    /// <summary>
    /// Technology stack configuration (frontend/backend frameworks, languages).
    /// </summary>
    [JsonPropertyName("techStack")]
    public TechStackSpec? TechStack { get; set; }

    /// <summary>
    /// Feature flags enabled for this app.
    /// </summary>
    [JsonPropertyName("featureFlags")]
    public Dictionary<string, bool>? FeatureFlags { get; set; }

    /// <summary>
    /// GitHub repo full name if already provisioned (e.g., "org/repo-name").
    /// </summary>
    [JsonPropertyName("gitHubRepoFullName")]
    public string? GitHubRepoFullName { get; set; }

    /// <summary>
    /// Database name if already provisioned.
    /// </summary>
    [JsonPropertyName("databaseName")]
    public string? DatabaseName { get; set; }

    /// <summary>
    /// App status (Draft, Active, Paused, etc.).
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public sealed class TechStackSpec
{
    [JsonPropertyName("frontend")]
    public FrameworkSpec? Frontend { get; set; }

    [JsonPropertyName("backend")]
    public FrameworkSpec? Backend { get; set; }

    [JsonPropertyName("database")]
    public DatabaseSpec? Database { get; set; }

    [JsonPropertyName("services")]
    public List<string>? Services { get; set; }
}

public sealed class FrameworkSpec
{
    [JsonPropertyName("framework")]
    public string? Framework { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("entryPoint")]
    public string? EntryPoint { get; set; }
}

public sealed class DatabaseSpec
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }
}

/// <summary>
/// Request to export initial code files to a new or existing repo.
/// </summary>
public sealed class InitialExportRequest
{
    [JsonPropertyName("repoUrl")]
    public string? RepoUrl { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    /// <summary>
    /// If true, create a new repository. Otherwise, use existing.
    /// </summary>
    [JsonPropertyName("createRepo")]
    public bool CreateRepo { get; set; }

    /// <summary>
    /// Repository name (used when createRepo=true).
    /// </summary>
    [JsonPropertyName("repoName")]
    public string? RepoName { get; set; }

    /// <summary>
    /// Files to export (add/modify).
    /// </summary>
    [JsonPropertyName("files")]
    public List<RepoFileChange> Files { get; set; } = new();

    /// <summary>
    /// Commit message for the initial export.
    /// </summary>
    [JsonPropertyName("commitMessage")]
    public string? CommitMessage { get; set; }
}

public sealed class InitialExportResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("repoUrl")]
    public string RepoUrl { get; set; } = string.Empty;

    [JsonPropertyName("repoFullName")]
    public string RepoFullName { get; set; } = string.Empty;

    [JsonPropertyName("baseCommitSha")]
    public string BaseCommitSha { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Request to generate deployment templates (Dockerfiles, workflows).
/// </summary>
public sealed class GenerateTemplatesRequest
{
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    /// <summary>
    /// Technology stack for template generation.
    /// </summary>
    [JsonPropertyName("techStack")]
    public TechStackSpec? TechStack { get; set; }

    /// <summary>
    /// If true, include GitHub Actions workflow.
    /// </summary>
    [JsonPropertyName("includeWorkflow")]
    public bool IncludeWorkflow { get; set; } = true;

    /// <summary>
    /// If true, include Dockerfiles.
    /// </summary>
    [JsonPropertyName("includeDockerfiles")]
    public bool IncludeDockerfiles { get; set; } = true;

    /// <summary>
    /// Output format: "files" returns file list, "patchset" returns changes for PR.
    /// </summary>
    [JsonPropertyName("outputFormat")]
    public string OutputFormat { get; set; } = "files";
}

public sealed class GenerateTemplatesResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("files")]
    public List<GeneratedTemplateFile> Files { get; set; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class GeneratedTemplateFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("contentBase64")]
    public string ContentBase64 { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
