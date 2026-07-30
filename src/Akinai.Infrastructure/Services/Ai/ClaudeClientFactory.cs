using Anthropic;
using Microsoft.Extensions.Options;

namespace Akinai.Infrastructure.Services.Ai;

/// <summary>
/// Holds the one <see cref="AnthropicClient"/> for the process.
///
/// Exists because the client owns an HttpClient and must be long-lived, while
/// the services that use it need per-request dependencies and so must be
/// scoped. Putting the cache here lets the summariser stay scoped without a
/// singleton capturing a scoped service — the standard captive-dependency trap.
/// </summary>
public sealed class ClaudeClientFactory(IOptions<ClaudeOptions> options)
{
    private readonly ClaudeOptions _options = options.Value;
    private readonly Lock _gate = new();

    private AnthropicClient? _client;

    public bool IsConfigured => _options.IsConfigured;

    /// <summary>Null when no API key is configured, so callers degrade rather than throw.</summary>
    public AnthropicClient? Get()
    {
        if (!_options.IsConfigured) return null;
        if (_client is not null) return _client;

        lock (_gate)
        {
            return _client ??= new AnthropicClient { ApiKey = _options.ApiKey };
        }
    }
}
