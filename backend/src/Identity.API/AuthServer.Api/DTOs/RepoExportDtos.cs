using System.Text.Json;

namespace AuthServer.Api.DTOs
{
    public sealed class RepoManifestRequest
    {
        public string? RepoUrl { get; set; }
        public string? UserId { get; set; }
        public string? WorkflowType { get; set; }
    }

    public sealed class RepoManifestFileEntry
    {
        public string Path { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    public sealed class RepoManifestResponse
    {
        public string BaseCommitSha { get; set; } = string.Empty;
        public List<RepoManifestFileEntry> Files { get; set; } = new();
    }

    public sealed class RepoFileChange
    {
        public string? Path { get; set; }

        /// <summary>
        /// "add" | "modify" | "delete"
        /// </summary>
        public string? Operation { get; set; }

        /// <summary>
        /// Base64-encoded file bytes (preferred for add/modify).
        /// </summary>
        public string? ContentBase64 { get; set; }

        /// <summary>
        /// UTF-8 text content (fallback for add/modify).
        /// </summary>
        public string? Content { get; set; }
    }

    public sealed class CreatePullRequestRequest
    {
        public string? RepoUrl { get; set; }
        public string? UserId { get; set; }

        public string? BaseCommitSha { get; set; }
        public string? BranchName { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }

        public string? PatchId { get; set; }
        public string? WorkflowType { get; set; }

        public List<RepoFileChange> Changes { get; set; } = new();

        /// <summary>
        /// Optional array of conflict objects/strings from MozaiksAI; included in PR body (not auto-resolved).
        /// </summary>
        public JsonElement? Conflicts { get; set; }
    }

    public sealed class CreatePullRequestResponse
    {
        public string PrUrl { get; set; } = string.Empty;
    }
}

