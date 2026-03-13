namespace JiraProxy;

public class JiraTokenStore
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? CloudId { get; set; }

    public bool IsConnected =>
        AccessToken != null &&
        ExpiresAt.HasValue &&
        DateTime.UtcNow < ExpiresAt.Value;
}