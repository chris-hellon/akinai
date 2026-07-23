using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Akinai.Infrastructure.Services.Pdf;

/// <summary>
/// Prints an application page to PDF with headless Chromium.
///
/// This replaced a second, hand-built rendering of the invoice. Two
/// implementations of one document drift the moment either is touched — every
/// label fix, every date format, every new field had to be made twice, and the
/// two disagreed on fonts, dates and the business name until someone noticed.
/// Rendering the page the client already sees removes the possibility.
///
/// The browser is launched once and reused. Launching Chromium per request
/// costs a second or more and a hundred megabytes; a single instance with a
/// short-lived page per render is the usual trade.
/// </summary>
public interface IHtmlPdfRenderer
{
    /// <summary>
    /// Renders <paramref name="url"/> to PDF bytes.
    ///
    /// Returns null rather than throwing when the browser is unavailable, so a
    /// caller can fall back instead of failing the request outright.
    /// </summary>
    Task<byte[]?> RenderAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class HtmlPdfRenderer(ILogger<HtmlPdfRenderer> logger) : IHtmlPdfRenderer, IAsyncDisposable
{
    private readonly SemaphoreSlim _launchLock = new(1, 1);
    private IBrowser? _browser;

    public async Task<byte[]?> RenderAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var browser = await GetBrowserAsync(cancellationToken);
            if (browser is null) return null;

            await using var page = await browser.NewPageAsync();

            // A4 at 96dpi. Setting it explicitly stops Chromium laying the page
            // out at its default 800px and then scaling, which shifts every
            // breakpoint the invoice uses.
            await page.SetViewportAsync(new ViewPortOptions { Width = 794, Height = 1123 });

            // The app falls back to the OS colour scheme when the user has no
            // stored preference, and headless Chromium reports dark. That put
            // dark-mode text colours on a white sheet — pale grey on white.
            // An invoice is a paper document; it is always light.
            await page.EmulateMediaFeaturesAsync(
            [
                new MediaFeatureValue { MediaFeature = MediaFeature.PrefersColorScheme, Value = "light" },
            ]);

            var response = await page.GoToAsync(url, new NavigationOptions
            {
                // The print route serves static HTML with no Blazor runtime and
                // self-hosted fonts, so the DOM is complete in the first
                // response. Networkidle0 here meant waiting 500ms for silence
                // after everything had already arrived.
                WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
                Timeout = 30_000,
            });

            if (response is null || !response.Ok)
            {
                logger.LogWarning("Print page returned {Status} for {Url}", response?.Status, url);
                return null;
            }

            // The page sets this marker once the invoice has rendered. Waiting
            // for the document to say it is ready beats guessing with a delay.
            try
            {
                await page.WaitForSelectorAsync("#print-ready", new WaitForSelectorOptions { Timeout = 10_000 });
            }
            catch (WaitTaskTimeoutException)
            {
                logger.LogWarning("Print page never signalled ready for {Url}; printing anyway.", url);
            }

            // Fonts are local, but the layout is still measured against them —
            // print before they are applied and every column width is wrong.
            await page.EvaluateExpressionAsync("document.fonts.ready");

            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                // Backgrounds are the invoice's structure, not decoration —
                // without this the header block and status banners print white.
                PrintBackground = true,
                // Margins come from the @page rule in the stylesheet, so the
                // screen and print layouts stay described in one place.
                PreferCSSPageSize = true,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PDF render failed for {Url}", url);
            return null;
        }
    }

    private async Task<IBrowser?> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsClosed: false }) return _browser;

        await _launchLock.WaitAsync(cancellationToken);
        try
        {
            if (_browser is { IsClosed: false }) return _browser;

            // Downloads Chromium on first use and caches it. Doing it lazily
            // rather than at startup keeps a deploy that never renders a PDF
            // from paying for a ~150 MB download.
            var fetcher = new BrowserFetcher();
            await fetcher.DownloadAsync();

            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                // --no-sandbox is required in most containers, which run as a
                // non-root user without the kernel namespaces the sandbox needs.
                // Safe here because the only pages loaded are our own, over
                // loopback, never user-supplied URLs.
                Args = ["--no-sandbox", "--disable-dev-shm-usage", "--font-render-hinting=none"],
            });

            logger.LogInformation("Headless Chromium launched for PDF rendering.");
            return _browser;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not launch headless Chromium; PDF rendering is unavailable.");
            return null;
        }
        finally
        {
            _launchLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _launchLock.Dispose();
    }
}
