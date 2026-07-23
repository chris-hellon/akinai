using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Akinai.Shared.Enums;
using Akinai.Shared.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Akinai.Infrastructure.Services.Translation;

/// <summary>
/// Azure Cognitive Services Translator.
///
/// Ported from the DeepDrive project, where the same service translates tour
/// content at save time. The pattern carries over: translate once when the
/// invoice is written and store the result, rather than on every view — an
/// invoice's text does not change after it is sent, so the cost is bounded per
/// invoice rather than per read.
/// </summary>
public class AzureTranslatorService(
    HttpClient httpClient,
    IOptions<TranslationOptions> options,
    ILogger<AzureTranslatorService> logger) : ITranslationService
{
    private readonly TranslationOptions _options = options.Value;

    /// <summary>
    /// Akinai's <see cref="SupportedLanguage"/> to Azure's BCP-47 codes.
    ///
    /// Chinese is the one that bites: Azure wants the script-qualified
    /// "zh-Hans", not a bare "zh", and silently returns nothing for the latter.
    /// </summary>
    private static readonly IReadOnlyDictionary<SupportedLanguage, string> AzureCodes =
        new Dictionary<SupportedLanguage, string>
        {
            [SupportedLanguage.English] = "en",
            [SupportedLanguage.Spanish] = "es",
            [SupportedLanguage.French] = "fr",
            [SupportedLanguage.German] = "de",
            [SupportedLanguage.Chinese] = "zh-Hans",
            [SupportedLanguage.Vietnamese] = "vi",
            [SupportedLanguage.Russian] = "ru",
            [SupportedLanguage.Korean] = "ko",
        };

    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<string?> TranslateAsync(
        string text,
        SupportedLanguage target,
        SupportedLanguage source = SupportedLanguage.English,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldTranslate(text, target, source)) return null;

        var results = await PostAsync([text], target, source, cancellationToken);
        return results.Count > 0 ? results[0] : null;
    }

    public async Task<IReadOnlyDictionary<string, string>> TranslateBatchAsync(
        IReadOnlyDictionary<string, string> textsByKey,
        SupportedLanguage target,
        SupportedLanguage source = SupportedLanguage.English,
        CancellationToken cancellationToken = default)
    {
        var empty = new Dictionary<string, string>();
        if (!IsConfigured || target == source) return empty;

        // Blank entries are dropped before the request — Azure returns a result
        // per input, so sending them would misalign every subsequent index.
        var pairs = textsByKey.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToList();
        if (pairs.Count == 0) return empty;

        var translated = await PostAsync(
            pairs.Select(kv => kv.Value).ToList(), target, source, cancellationToken);

        var result = new Dictionary<string, string>();
        for (var i = 0; i < translated.Count && i < pairs.Count; i++)
        {
            result[pairs[i].Key] = translated[i];
        }
        return result;
    }

    private bool ShouldTranslate(string text, SupportedLanguage target, SupportedLanguage source)
        => IsConfigured && !string.IsNullOrWhiteSpace(text) && target != source;

    /// <summary>
    /// One request, N texts, one target. Returns translations in request order;
    /// an empty list means the call failed and the caller keeps the original.
    /// </summary>
    private async Task<List<string>> PostAsync(
        IReadOnlyList<string> texts,
        SupportedLanguage target,
        SupportedLanguage source,
        CancellationToken cancellationToken)
    {
        if (!AzureCodes.TryGetValue(target, out var toCode) ||
            !AzureCodes.TryGetValue(source, out var fromCode))
        {
            logger.LogWarning("No Azure language code for {Source} -> {Target}.", source, target);
            return [];
        }

        try
        {
            var url = $"{_options.Endpoint.TrimEnd('/')}/translate?api-version=3.0&from={fromCode}&to={toCode}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(texts.Select(t => new { Text = t }).ToArray()),
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
            request.Headers.Add("Ocp-Apim-Subscription-Region", _options.Region);

            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content
                .ReadFromJsonAsync<TranslationResponse[]>(cancellationToken);

            return payload?
                .Select(p => p.Translations.FirstOrDefault()?.Text ?? string.Empty)
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            // Never fail the caller: an invoice must still save if Azure is down.
            logger.LogError(ex, "Translation to {Target} failed; falling back to source text.", target);
            return [];
        }
    }

    private record TranslationResponse(TranslationResult[] Translations);
    private record TranslationResult(string Text, string To);
}
