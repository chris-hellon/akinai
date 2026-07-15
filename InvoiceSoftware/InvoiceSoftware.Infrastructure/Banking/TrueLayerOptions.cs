namespace InvoiceSoftware.Infrastructure.Banking;

public class TrueLayerOptions
{
    public const string SectionName = "TrueLayer";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Use the free sandbox (mock banks) while developing.</summary>
    public bool UseSandbox { get; set; } = true;

    /// <summary>Redirect back to the app after auth — must be whitelisted in the TrueLayer console.</summary>
    public string RedirectUri { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public string AuthBase => UseSandbox ? "https://auth.truelayer-sandbox.com" : "https://auth.truelayer.com";
    public string ApiBase => UseSandbox ? "https://api.truelayer-sandbox.com" : "https://api.truelayer.com";

    /// <summary>Provider filter for the auth dialog — the mock bank in sandbox, real UK banks live.</summary>
    public string Providers => UseSandbox ? "uk-cs-mock" : "uk-ob-all uk-oauth-all";
}
