namespace InvoiceSoftware.Infrastructure.Banking;

public class GoCardlessOptions
{
    public const string SectionName = "GoCardless";

    /// <summary>Secret ID from the GoCardless Bank Account Data portal.</summary>
    public string SecretId { get; set; } = string.Empty;

    /// <summary>Secret key from the GoCardless Bank Account Data portal.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Bank Account Data API base URL.</summary>
    public string BaseUrl { get; set; } = "https://bankaccountdata.gocardless.com";

    /// <summary>
    /// Where the bank sends the user back after authenticating — must be whitelisted in the
    /// GoCardless app. Typically "{your-app-origin}/bank-import".
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretId) && !string.IsNullOrWhiteSpace(SecretKey);
}
