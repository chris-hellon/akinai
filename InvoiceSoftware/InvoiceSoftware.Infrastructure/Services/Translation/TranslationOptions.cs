namespace InvoiceSoftware.Infrastructure.Services.Translation;

/// <summary>
/// Azure Translator configuration.
///
/// The key is Akinai's, not the user's — translation is a Pro feature we pay
/// for on their behalf, so there is nothing for them to configure. Spend is
/// bounded per account by the AI budget rather than by asking freelancers to
/// hold their own Azure subscription.
/// </summary>
public class TranslationOptions
{
    public const string SectionName = "Translation";

    /// <summary>Azure Translator API key. Empty disables translation entirely.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Azure region the Translator resource lives in, e.g. "uksouth".</summary>
    public string Region { get; set; } = "uksouth";

    /// <summary>Global endpoint unless the resource is pinned to one region.</summary>
    public string Endpoint { get; set; } = "https://api.cognitive.microsofttranslator.com";

    /// <summary>Whether to attempt translation at all.</summary>
    public bool Enabled { get; set; } = true;
}
