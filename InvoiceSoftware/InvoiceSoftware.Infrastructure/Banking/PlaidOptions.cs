namespace InvoiceSoftware.Infrastructure.Banking;

public class PlaidOptions
{
    public const string SectionName = "Plaid";

    public string ClientId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;

    /// <summary>"sandbox" (free) or "production".</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>Redirect back to the app after Hosted Link completes.</summary>
    public string RedirectUri { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(Secret);

    public string BaseUrl => Environment.ToLowerInvariant() switch
    {
        "production" => "https://production.plaid.com",
        _ => "https://sandbox.plaid.com"
    };
}
