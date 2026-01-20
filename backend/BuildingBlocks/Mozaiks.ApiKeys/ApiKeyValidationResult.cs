namespace Mozaiks.ApiKeys;

public sealed class ApiKeyValidationResult
{
    public bool IsValid { get; set; }
    public string? AppId { get; set; }
    public string? OwnerUserId { get; set; }
    public string? ErrorMessage { get; set; }
}

