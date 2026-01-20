namespace AuthServer.Api.Shared;

public sealed class GitHubOptions
{
    public string AccessToken { get; set; } = "replace_me";

    public string? OrganizationName { get; set; }

    public string MozaiksCoreRepoUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Branch or tag to use when downloading MozaiksCore files.
    /// Defaults to "main".
    /// </summary>
    public string MozaiksCoreBranch { get; set; } = "main";
    
    /// <summary>
    /// Subdirectory within MozaiksCore repo to include (e.g., "sdk" or "src/mozaikscore").
    /// If empty, includes all files.
    /// </summary>
    public string? MozaiksCoreSubdirectory { get; set; }
}

/// <summary>
/// Configuration for GitHub repository secrets.
/// Maps logical secret names to values from configuration.
/// </summary>
public sealed class GitHubSecretsOptions
{
    /// <summary>
    /// DockerHub username (not a secret, used in workflow env).
    /// </summary>
    public string DockerHubUsername { get; set; } = string.Empty;
    
    /// <summary>
    /// DockerHub token for pushing images.
    /// </summary>
    public string DockerHubToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure service principal credentials JSON for Azure Container Apps deployment.
    /// </summary>
    public string AzureCredentials { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure resource group name.
    /// </summary>
    public string AzureResourceGroup { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure Container Apps environment name.
    /// </summary>
    public string AzureContainerAppsEnvironment { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional static secrets to set on all repos.
    /// Key is secret name, value is secret value.
    /// </summary>
    public Dictionary<string, string> AdditionalSecrets { get; set; } = new();
    
    /// <summary>
    /// Get all secrets to set for a repository.
    /// Matches the secrets expected by deployment_manager.py GitHub Actions workflow.
    /// </summary>
    public Dictionary<string, string> GetAllSecrets(string containerAppName, string? databaseUri = null, string? appApiKey = null)
    {
        var secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Core deployment secrets (matching deployment_manager.py requirements)
        if (!string.IsNullOrWhiteSpace(DockerHubToken))
            secrets["DOCKERHUB_TOKEN"] = DockerHubToken;
            
        if (!string.IsNullOrWhiteSpace(AzureCredentials))
            secrets["AZURE_CREDENTIALS"] = AzureCredentials;
            
        if (!string.IsNullOrWhiteSpace(AzureResourceGroup))
        {
            // deployment_manager.py uses RESOURCE_GROUP
            secrets["RESOURCE_GROUP"] = AzureResourceGroup;
        }
            
        if (!string.IsNullOrWhiteSpace(AzureContainerAppsEnvironment))
        {
            // deployment_manager.py uses CONTAINER_APP_ENVIRONMENT
            secrets["CONTAINER_APP_ENVIRONMENT"] = AzureContainerAppsEnvironment;
        }
            
        // App-specific secrets
        if (!string.IsNullOrWhiteSpace(containerAppName))
            secrets["CONTAINER_APP_NAME"] = containerAppName;
            
        if (!string.IsNullOrWhiteSpace(databaseUri))
        {
            // deployment_manager.py uses DATABASE_URI
            secrets["DATABASE_URI"] = databaseUri;
        }
            
        if (!string.IsNullOrWhiteSpace(appApiKey))
            secrets["MOZAIKS_API_KEY"] = appApiKey;
        
        // Additional configured secrets
        foreach (var (key, value) in AdditionalSecrets)
        {
            if (!string.IsNullOrWhiteSpace(value))
                secrets[key] = value;
        }
        
        return secrets;
    }
}

