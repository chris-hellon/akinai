using System.Globalization;
using Akinai.Domain.Entities;
using Akinai.Shared.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Akinai.Infrastructure.Services;

public class PdfGeneratorService
{
    // Modern color palette matching PublicInvoiceView
    private const string IndigoColor = "#0e5c66";
    private const string EmeraldColor = "#059669";
    private const string EmeraldLight = "#ecfdf5";
    private const string RedColor = "#dc2626";
    private const string RedLight = "#fef2f2";
    private const string BlueColor = "#2563eb";
    private const string BlueLight = "#eff6ff";
    private const string AmberColor = "#d97706";
    private const string AmberLight = "#fffbeb";
    private const string PurpleColor = "#7c3aed";
    private const string PurpleLight = "#f5f3ff";
    private const string DarkText = "#111827";
    private const string MediumText = "#374151";
    private const string LightText = "#6b7280";
    /// <summary>
    /// Fonts registered with QuestPDF once per process.
    ///
    /// Without this the PDF embeds only QuestPDF's built-in Lato, which has no
    /// CJK glyphs. Chinese then depended on whatever font the *viewer* happened
    /// to have — it looked fine on a Mac and rendered as boxes on a Linux server
    /// or a client machine without CJK installed. Registering here embeds the
    /// glyphs at generation time, so the file renders the same everywhere.
    ///
    /// The files are fetched by fetch-fonts.sh rather than committed. If they
    /// are missing we carry on with Lato: an English-only PDF beats no PDF.
    /// </summary>
    private static readonly bool FontsRegistered = RegisterFonts();

    private static bool RegisterFonts()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fonts");
        if (!Directory.Exists(dir)) return false;

        var any = false;
        foreach (var file in Directory.GetFiles(dir, "*.ttf"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                QuestPDF.Drawing.FontManager.RegisterFont(stream);
                any = true;
            }
            catch
            {
                // A corrupt or partially-downloaded font must not stop the
                // others registering, nor take down invoice generation.
            }
        }
        return any;
    }

    /// <summary>
    /// Document font, with fallbacks for scripts the primary lacks.
    ///
    /// Noto Sans first: Latin, Vietnamese and Cyrillic. Noto Sans SC picks up
    /// Simplified Chinese and Hangul. Lato is the last resort when the font
    /// files were never fetched — English still renders.
    ///
    /// Not the web app's Zen Kaku Gothic New, despite the mismatch that
    /// introduces. Zen Kaku has no Vietnamese Extended block but claims enough
    /// coverage that per-glyph fallback never fires, so "Số tiền" printed as
    /// "Sô´tiêǹ". Its Japanese coverage buys nothing here either — Japanese is
    /// not an invoice language, only branding on the web.
    /// </summary>
    private static TextStyle BaseTextStyle(TextStyle style)
        => FontsRegistered
            ? style.FontFamily("Noto Sans", "Noto Sans SC", "Lato")
            : style.FontFamily("Lato");

    private const string VeryLightText = "#9ca3af";
    private const string BorderColor = "#e5e7eb";
    private const string BackgroundLight = "#f9fafb";

    public Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice, List<TimeEntry> timeEntries, List<Expense> expenses, List<InvoiceLineItem>? productLineItems = null, BusinessProfile? businessProfile = null, CurrencyPaymentSettings? currencySettings = null, InvoiceTemplate? template = null, decimal? vndToHomeCurrencyRate = null,
        string language = "English",
        IReadOnlyDictionary<string, string>? translations = null,
        IReadOnlyList<string>? requiredStatements = null)
    {
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // The document's fixed wording and its date/number formatting. Passed
            // down rather than held in a field so the Compose methods stay pure
            // and safe to call from anywhere.
            var L = InvoiceLabels.For(language);
            var culture = L.Culture;

            // Freeform text the caller already translated, keyed by the original.
            // Anything absent falls through untouched, so a translation outage
            // yields an English invoice rather than a failed one.
            string Tr(string? s) => s is not null && translations is not null
                && translations.TryGetValue(s, out var v) && !string.IsNullOrWhiteSpace(v) ? v : s ?? "";

            // Use template colors and settings if available, otherwise use defaults
            var primaryColor = template?.PrimaryColor ?? IndigoColor;
            var accentColor = template?.AccentColor ?? EmeraldColor;
            var textColor = template?.TextColor ?? DarkText;
            var showLogo = template?.ShowLogo ?? true;
            var showPaymentQR = template?.ShowPaymentQR ?? true;
            var showBankDetails = template?.ShowBankDetails ?? true;
            var showItemDescriptions = template?.ShowItemDescriptions ?? true;

            // Layout settings
            var headerLayout = template?.HeaderLayout.ToString().ToLowerInvariant() ?? "standard";
            var itemsLayout = template?.ItemsLayout.ToString().ToLowerInvariant() ?? "standard";
            var footerLayout = template?.FooterLayout.ToString().ToLowerInvariant() ?? "standard";

            // Calculate totals from time entries (rounded to quarter hours for billing)
            var timeEntriesSubtotal = timeEntries.Sum(e => e.BillableHours * e.BillingRate);

            // Calculate totals from expenses
            var expensesSubtotal = expenses.Sum(e => e.GetTotalAmount());

            // Calculate totals from product line items
            var productsSubtotal = productLineItems?.Sum(p => p.LineTotal.Amount) ?? 0;

            var subtotal = timeEntriesSubtotal + expensesSubtotal + productsSubtotal;
            var taxAmount = subtotal * (invoice.TaxRate / 100);
            var total = subtotal + taxAmount;

            // Build line items from time entries (rounded to quarter hours for billing)
            var lineItems = timeEntries.Select(e =>
            {
                var jobName = Tr(e.Job.Name);
                var projectName = e.Job.Project?.Name;
                var sectionName = e.Job.Section?.Name;
                var hourlyRate = e.BillingRate;
                var billableHours = e.BillableHours;
                var lineTotal = billableHours * hourlyRate;

                return new PdfLineItem(jobName, projectName, sectionName, billableHours, hourlyRate, lineTotal, e.Date);
            }).ToList();

            var currencySymbol = GetCurrencySymbol(invoice.Currency);
            var isOverdue = invoice.Status is Domain.Enums.InvoiceStatus.Sent or Domain.Enums.InvoiceStatus.Overdue &&
                           invoice.DueDate < DateOnly.FromDateTime(DateTime.Today);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => BaseTextStyle(x.FontSize(10)).FontColor(DarkText));

                    page.Content().Column(mainColumn =>
                    {
                        // Status Banner
                        ComposeStatusBanner(mainColumn, invoice, isOverdue, L, culture);

                        // Main content with padding
                        mainColumn.Item().Padding(40).Column(contentColumn =>
                        {
                            // Header - layout-aware
                            ComposeHeader(contentColumn, invoice, businessProfile, isOverdue, showLogo, primaryColor, headerLayout, total, currencySymbol, L, culture);

                            // Bill To & Amount Due - layout-aware
                            ComposeBillToSection(contentColumn, invoice, total, currencySymbol, primaryColor, headerLayout, L, culture);

                            // Services Table (Time Entries) - layout-aware
                            if (lineItems.Any())
                            {
                                ComposeServicesTable(contentColumn, lineItems, currencySymbol, accentColor, itemsLayout, showItemDescriptions, L, culture);
                            }

                            // Expenses Table - layout-aware
                            if (expenses.Any())
                            {
                                ComposeExpensesTable(contentColumn, expenses, currencySymbol, itemsLayout, showItemDescriptions, L, culture, language);
                            }

                            // Products Table - layout-aware
                            if (productLineItems?.Any() == true)
                            {
                                ComposeProductsTable(contentColumn, productLineItems, currencySymbol, itemsLayout, showItemDescriptions, L, culture, Tr);
                            }

                            // Totals - layout-aware
                            ComposeTotals(contentColumn, invoice, subtotal, taxAmount, total, currencySymbol, primaryColor, footerLayout, L, culture);

                            // Wording the corridor legally requires. Printed before the
                            // notes and never translated — the reverse-charge statement in
                            // particular has prescribed wording.
                            ComposeRequiredStatements(contentColumn, requiredStatements);

                            // Notes
                            ComposeNotes(contentColumn, invoice, businessProfile, L, Tr);

                            // Payment Options
                            ComposePaymentOptions(contentColumn, invoice, businessProfile, currencySettings, total, showPaymentQR, showBankDetails, businessProfile?.DefaultCurrency, vndToHomeCurrencyRate, L);
                            // Rate comes from the live service via the caller — the old
                            // hand-entered setting is gone. Null simply means no converted
                            // amount is offered.
                        });
                    });

                    // Footer at bottom of page
                    page.Footer().Background(BackgroundLight).BorderTop(1).BorderColor(BorderColor).Padding(16)
                        .AlignCenter().Text($"Thank you for your business. Payment is due by {invoice.DueDate:MMMM d, yyyy}.")
                        .FontSize(10).FontColor(VeryLightText);
                });
            });

            var bytes = document.GeneratePdf();
            return Task.FromResult(bytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to generate PDF: {ex.Message}", ex);
        }
    }

    private record PdfLineItem(string JobName, string? ProjectName, string? SectionName, decimal Quantity, decimal UnitPrice, decimal LineTotal, DateOnly? Date);


    // Delegates rather than keeping a second table: this copy had already
    // drifted, missing the ruble, so a Russian invoice printed "RUB 1,200".
    private static string GetCurrencySymbol(string currencyCode)
        => Shared.Enums.CurrencyExtensions.GetSymbol(currencyCode);

    private static void ComposeStatusBanner(ColumnDescriptor column, Invoice invoice, bool isOverdue, InvoiceLabelSet L, CultureInfo culture)
    {
        if (invoice.Status == Domain.Enums.InvoiceStatus.Paid)
        {
            column.Item().Background(EmeraldLight).Padding(16).Row(row =>
            {
                row.AutoItem().AlignMiddle().PaddingRight(12)
                    .Width(24).Height(24).Background(EmeraldColor)
                    .AlignCenter().AlignMiddle()
                    .Text("✓").FontSize(14).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.AutoItem().Text(L.StatusPaid).FontSize(12).Bold().FontColor(EmeraldColor);
                        if (invoice.PaidDate.HasValue)
                        {
                            r.AutoItem().PaddingLeft(8).Text(invoice.PaidDate.Value.ToString("d MMMM yyyy", culture)).FontSize(11).FontColor(EmeraldColor);
                        }
                    });
                });
            });
        }
        else if (isOverdue)
        {
            var daysPastDue = DateOnly.FromDateTime(DateTime.Today).DayNumber - invoice.DueDate.DayNumber;
            column.Item().Background(RedLight).Padding(16).Row(row =>
            {
                row.AutoItem().AlignMiddle().PaddingRight(12)
                    .Width(24).Height(24).Background(RedColor)
                    .AlignCenter().AlignMiddle()
                    .Text("!").FontSize(14).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.AutoItem().Text(L.StatusOverdue).FontSize(12).Bold().FontColor(RedColor);
                        r.AutoItem().PaddingLeft(8).Text($"{daysPastDue} days past due").FontSize(11).FontColor(RedColor);
                    });
                });
            });
        }
        else if (invoice.Status == Domain.Enums.InvoiceStatus.Sent)
        {
            column.Item().Background(BlueLight).Padding(16).Row(row =>
            {
                row.AutoItem().AlignMiddle().PaddingRight(12)
                    .Width(24).Height(24).Background(BlueColor)
                    .AlignCenter().AlignMiddle()
                    .Text("$").FontSize(14).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.AutoItem().Text(L.StatusPaymentDue).FontSize(12).Bold().FontColor(BlueColor);
                        r.AutoItem().PaddingLeft(8).Text($"by {invoice.DueDate:MMMM d, yyyy}").FontSize(11).FontColor(BlueColor);
                    });
                });
            });
        }
        else if (invoice.Status == Domain.Enums.InvoiceStatus.Draft)
        {
            column.Item().Background(AmberLight).Padding(16).Row(row =>
            {
                row.AutoItem().AlignMiddle().PaddingRight(12)
                    .Width(24).Height(24).Background(AmberColor)
                    .AlignCenter().AlignMiddle()
                    .Text("✎").FontSize(14).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.AutoItem().Text(L.StatusDraft).FontSize(12).Bold().FontColor(AmberColor);
                        r.AutoItem().PaddingLeft(8).Text(L.DraftNotSent).FontSize(11).FontColor(AmberColor);
                    });
                });
            });
        }
    }

    private static void ComposeHeader(ColumnDescriptor column, Invoice invoice, BusinessProfile? businessProfile, bool isOverdue, bool showLogo, string primaryColor, string headerLayout, decimal total, string currencySymbol, InvoiceLabelSet L, CultureInfo culture)
    {
        if (headerLayout == "centered")
        {
            // Centered Header Layout with colored background - matching web component
            var hasLogo = showLogo && businessProfile is { Logo.Length: > 0 };

            column.Item().Background(primaryColor).Padding(24).Column(centerCol =>
            {
                if (hasLogo)
                {
                    try
                    {
                        centerCol.Item().AlignCenter().PaddingBottom(16).Height(48).Image(businessProfile!.Logo).FitHeight();
                    }
                    catch { /* Skip logo if image data is invalid */ }
                }

                var companyName = !string.IsNullOrWhiteSpace(businessProfile?.TradingName)
                    ? businessProfile.TradingName
                    : !string.IsNullOrWhiteSpace(businessProfile?.CompanyName)
                        ? businessProfile.CompanyName
                        : null;

                if (!string.IsNullOrEmpty(companyName))
                {
                    centerCol.Item().AlignCenter().Text(companyName).FontSize(18).Bold().FontColor(Colors.White);
                }

                if (businessProfile?.Address != null)
                {
                    // Only add top padding if there was a company name or logo before
                    var needsTopPadding = hasLogo || !string.IsNullOrEmpty(companyName);
                    var addressContainer = needsTopPadding ? centerCol.Item().PaddingTop(8) : centerCol.Item();

                    addressContainer.Column(addressCol =>
                    {
                        if (!string.IsNullOrEmpty(businessProfile.Address.Street1))
                            addressCol.Item().AlignCenter().Text(businessProfile.Address.Street1).FontSize(10).FontColor("#ffffffcc");
                        var cityLine = string.Join(", ", new[] { businessProfile.Address.City, businessProfile.Address.State, businessProfile.Address.PostalCode }.Where(s => !string.IsNullOrEmpty(s)));
                        if (!string.IsNullOrEmpty(cityLine))
                            addressCol.Item().AlignCenter().Text(cityLine).FontSize(10).FontColor("#ffffffcc");
                        if (!string.IsNullOrEmpty(businessProfile.Address.Country))
                            addressCol.Item().AlignCenter().Text(businessProfile.Address.Country).FontSize(10).FontColor("#ffffffcc");
                    });
                }
            });

            // Invoice number and dates below colored header
            column.Item().PaddingTop(20).AlignCenter().Column(infoCol =>
            {
                infoCol.Item().AlignCenter().Text(invoice.InvoiceNumber).FontSize(28).Bold().FontColor(DarkText);
                infoCol.Item().AlignCenter().Text(L.Invoice).FontSize(11).FontColor(LightText);

                infoCol.Item().PaddingTop(12).AlignCenter().Row(dateRow =>
                {
                    dateRow.AutoItem().Text($"{L.Issued}:  ").FontSize(10).FontColor(LightText);
                    dateRow.AutoItem().Text(invoice.IssueDate.ToString("d MMM yyyy", culture)).FontSize(10).Bold().FontColor(DarkText);
                    dateRow.AutoItem().PaddingHorizontal(12).Text("|").FontSize(10).FontColor(LightText);
                    dateRow.AutoItem().Text($"{L.Due}:  ").FontSize(10).FontColor(LightText);
                    dateRow.AutoItem().Text(invoice.DueDate.ToString("d MMM yyyy", culture)).FontSize(10).Bold().FontColor(isOverdue ? RedColor : DarkText);
                });
            });

            column.Item().PaddingTop(24);
        }
        else if (headerLayout == "minimal")
        {
            // Minimal Header Layout
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignMiddle().Row(leftRow =>
                {
                    if (showLogo && businessProfile is { Logo.Length: > 0 })
                    {
                        try
                        {
                            leftRow.AutoItem().AlignMiddle().Height(40).Image(businessProfile.Logo).FitHeight();
                            leftRow.AutoItem().PaddingLeft(12);
                        }
                        catch { /* Skip logo if image data is invalid */ }
                    }

                    leftRow.RelativeItem().AlignMiddle().Column(infoCol =>
                    {
                        infoCol.Item().Text(invoice.InvoiceNumber).FontSize(24).Bold().FontColor(DarkText);
                        infoCol.Item().Text($"{invoice.IssueDate:MMM d, yyyy} — Due {invoice.DueDate:MMM d, yyyy}").FontSize(9).FontColor(LightText);
                    });
                });

                // Right side - Total amount (minimal shows total in header)
                var totalColor = invoice.Status == Domain.Enums.InvoiceStatus.Paid ? EmeraldColor : primaryColor;
                row.ConstantItem(150).AlignRight().AlignMiddle().Text($"{currencySymbol}{total:N2}").FontSize(28).Bold().FontColor(totalColor);
            });

            column.Item().PaddingTop(24);
        }
        else
        {
            // Standard Header Layout (default) with colored background
            var hasLogo = showLogo && businessProfile is { Logo.Length: > 0 };

            column.Item().Background(primaryColor).Padding(20).Column(leftCol =>
            {
                if (hasLogo)
                {
                    try
                    {
                        leftCol.Item().Height(48).Image(businessProfile!.Logo).FitHeight();
                        leftCol.Item().PaddingTop(12);
                    }
                    catch { /* Skip logo if image data is invalid */ }
                }

                var companyName = !string.IsNullOrWhiteSpace(businessProfile?.TradingName)
                    ? businessProfile.TradingName
                    : !string.IsNullOrWhiteSpace(businessProfile?.CompanyName)
                        ? businessProfile.CompanyName
                        : null;

                if (!string.IsNullOrEmpty(companyName))
                {
                    leftCol.Item().Text(companyName).FontSize(18).Bold().FontColor(Colors.White);
                }

                if (businessProfile?.Address != null)
                {
                    var needsTopPadding = hasLogo || !string.IsNullOrEmpty(companyName);
                    if (needsTopPadding)
                        leftCol.Item().PaddingTop(8);

                    if (!string.IsNullOrEmpty(businessProfile.Address.Street1))
                        leftCol.Item().Text(businessProfile.Address.Street1).FontSize(10).FontColor(Colors.White);
                    var cityLine = string.Join(", ", new[] { businessProfile.Address.City, businessProfile.Address.State, businessProfile.Address.PostalCode }.Where(s => !string.IsNullOrEmpty(s)));
                    if (!string.IsNullOrEmpty(cityLine))
                        leftCol.Item().Text(cityLine).FontSize(10).FontColor(Colors.White);
                    if (!string.IsNullOrEmpty(businessProfile.Address.Country))
                        leftCol.Item().Text(businessProfile.Address.Country).FontSize(10).FontColor(Colors.White);
                }
            });

            // Contact info and invoice details below colored header
            column.Item().PaddingTop(16).Row(row =>
            {
                row.RelativeItem().Column(leftCol =>
                {
                    if (businessProfile != null)
                    {
                        leftCol.Item().Text(businessProfile.Email.Value).FontSize(10).FontColor(LightText);
                        if (businessProfile.Phone != null)
                            leftCol.Item().Text(businessProfile.Phone.Value).FontSize(10).FontColor(LightText);
                    }

                    if (businessProfile != null && (!string.IsNullOrEmpty(businessProfile.TaxNumber) || !string.IsNullOrEmpty(businessProfile.RegistrationNumber)))
                    {
                        leftCol.Item().PaddingTop(6);
                        if (!string.IsNullOrEmpty(businessProfile.TaxNumber))
                            leftCol.Item().Text($"Tax/VAT: {businessProfile.TaxNumber}").FontSize(9).FontColor(VeryLightText);
                        if (!string.IsNullOrEmpty(businessProfile.RegistrationNumber))
                            leftCol.Item().Text($"Reg: {businessProfile.RegistrationNumber}").FontSize(9).FontColor(VeryLightText);
                    }
                });

                // Right side - Invoice details
                row.ConstantItem(180).AlignRight().Column(rightCol =>
                {
                    rightCol.Item().AlignRight().Text(invoice.InvoiceNumber).FontSize(28).Bold().FontColor(DarkText);
                    rightCol.Item().AlignRight().Text(L.Invoice).FontSize(11).FontColor(LightText);

                    rightCol.Item().PaddingTop(16);
                    rightCol.Item().Row(dateRow =>
                    {
                        dateRow.RelativeItem().AlignRight().Column(dc =>
                        {
                            dc.Item().AlignRight().Text($"{L.Issued}:  ").FontSize(10).FontColor(LightText);
                            dc.Item().AlignRight().Text(invoice.IssueDate.ToString("d MMM yyyy", culture)).FontSize(10).Bold().FontColor(DarkText);
                        });
                        dateRow.ConstantItem(16);
                        dateRow.RelativeItem().AlignRight().Column(dc =>
                        {
                            dc.Item().AlignRight().Text($"{L.Due}:  ").FontSize(10).FontColor(LightText);
                            dc.Item().AlignRight().Text(invoice.DueDate.ToString("d MMM yyyy", culture)).FontSize(10).Bold().FontColor(isOverdue ? RedColor : DarkText);
                        });
                    });
                });
            });

            column.Item().PaddingTop(24);
        }
    }

    private void ComposeBillToSection(ColumnDescriptor column, Invoice invoice, decimal total, string currencySymbol, string primaryColor, string headerLayout, InvoiceLabelSet L, CultureInfo culture)
    {
        column.Item().BorderTop(1).BorderColor(BorderColor).PaddingTop(20);

        if (headerLayout == "centered")
        {
            // Centered layout - Bill To and Amount side by side, centered alignment
            column.Item().Row(row =>
            {
                // Bill To
                row.RelativeItem().Column(billToCol =>
                {
                    billToCol.Item().Text(L.BillTo).FontSize(9).Bold().FontColor(VeryLightText).LetterSpacing(0.5f);
                    billToCol.Item().PaddingTop(6);
                    billToCol.Item().Text(invoice.Client.Name).FontSize(14).Bold().FontColor(DarkText);

                    if (!string.IsNullOrEmpty(invoice.Client.CompanyName))
                        billToCol.Item().Text(invoice.Client.CompanyName).FontSize(10).FontColor(MediumText);

                    if (invoice.Client.BillingAddress != null)
                    {
                        billToCol.Item().PaddingTop(6);
                        billToCol.Item().Text(invoice.Client.BillingAddress.Street1).FontSize(10).FontColor(MediumText);
                        var cityLine = $"{invoice.Client.BillingAddress.City}, {invoice.Client.BillingAddress.State} {invoice.Client.BillingAddress.PostalCode}";
                        billToCol.Item().Text(cityLine).FontSize(10).FontColor(MediumText);
                        if (!string.IsNullOrEmpty(invoice.Client.BillingAddress.Country))
                            billToCol.Item().Text(invoice.Client.BillingAddress.Country).FontSize(10).FontColor(MediumText);
                    }
                });

                // Amount Due
                row.ConstantItem(180).AlignRight().Column(amountCol =>
                {
                    amountCol.Item().AlignRight().Text(L.AmountDue).FontSize(9).Bold().FontColor(VeryLightText).LetterSpacing(0.5f);
                    amountCol.Item().PaddingTop(6);
                    var totalColor = invoice.Status == Domain.Enums.InvoiceStatus.Paid ? EmeraldColor : primaryColor;
                    amountCol.Item().AlignRight().Text($"{currencySymbol}{total:N2}").FontSize(32).Bold().FontColor(totalColor);
                    if (invoice.Status == Domain.Enums.InvoiceStatus.Paid)
                    {
                        amountCol.Item().AlignRight().Text(L.PaidInFull).FontSize(10).FontColor(EmeraldColor);
                    }
                });
            });
        }
        else if (headerLayout == "minimal")
        {
            // Minimal layout - just Bill To, amount is already shown in header
            column.Item().Column(billToCol =>
            {
                billToCol.Item().Text(L.BillTo).FontSize(9).Bold().FontColor(VeryLightText).LetterSpacing(0.5f);
                billToCol.Item().PaddingTop(6);
                billToCol.Item().Text(invoice.Client.Name).FontSize(14).Bold().FontColor(DarkText);

                if (!string.IsNullOrEmpty(invoice.Client.CompanyName))
                    billToCol.Item().Text(invoice.Client.CompanyName).FontSize(10).FontColor(MediumText);

                if (invoice.Client.BillingAddress != null)
                {
                    billToCol.Item().PaddingTop(6);
                    billToCol.Item().Text(invoice.Client.BillingAddress.Street1).FontSize(10).FontColor(MediumText);
                    var cityLine = $"{invoice.Client.BillingAddress.City}, {invoice.Client.BillingAddress.State} {invoice.Client.BillingAddress.PostalCode}";
                    billToCol.Item().Text(cityLine).FontSize(10).FontColor(MediumText);
                    if (!string.IsNullOrEmpty(invoice.Client.BillingAddress.Country))
                        billToCol.Item().Text(invoice.Client.BillingAddress.Country).FontSize(10).FontColor(MediumText);
                }

                if (!string.IsNullOrEmpty(invoice.Client.Email?.Value))
                {
                    billToCol.Item().PaddingTop(4).Text(invoice.Client.Email.Value).FontSize(10).FontColor(LightText);
                }
            });
        }
        else
        {
            // Standard layout
            column.Item().Row(row =>
            {
                // Bill To
                row.RelativeItem().Column(billToCol =>
                {
                    billToCol.Item().Text(L.BillTo).FontSize(9).Bold().FontColor(VeryLightText).LetterSpacing(0.5f);
                    billToCol.Item().PaddingTop(6);
                    billToCol.Item().Text(invoice.Client.Name).FontSize(14).Bold().FontColor(DarkText);

                    if (!string.IsNullOrEmpty(invoice.Client.CompanyName))
                        billToCol.Item().Text(invoice.Client.CompanyName).FontSize(10).FontColor(MediumText);

                    if (invoice.Client.BillingAddress != null)
                    {
                        billToCol.Item().PaddingTop(6);
                        billToCol.Item().Text(invoice.Client.BillingAddress.Street1).FontSize(10).FontColor(MediumText);
                        var cityLine = $"{invoice.Client.BillingAddress.City}, {invoice.Client.BillingAddress.State} {invoice.Client.BillingAddress.PostalCode}";
                        billToCol.Item().Text(cityLine).FontSize(10).FontColor(MediumText);
                        if (!string.IsNullOrEmpty(invoice.Client.BillingAddress.Country))
                            billToCol.Item().Text(invoice.Client.BillingAddress.Country).FontSize(10).FontColor(MediumText);
                    }
                });

                // Amount Due
                row.ConstantItem(180).AlignRight().Column(amountCol =>
                {
                    amountCol.Item().AlignRight().Text(L.AmountDue).FontSize(9).Bold().FontColor(VeryLightText).LetterSpacing(0.5f);
                    amountCol.Item().PaddingTop(6);
                    var totalColor = invoice.Status == Domain.Enums.InvoiceStatus.Paid ? EmeraldColor : primaryColor;
                    amountCol.Item().AlignRight().Text($"{currencySymbol}{total:N2}").FontSize(32).Bold().FontColor(totalColor);
                    if (invoice.Status == Domain.Enums.InvoiceStatus.Paid)
                    {
                        amountCol.Item().AlignRight().Text(L.PaidInFull).FontSize(10).FontColor(EmeraldColor);
                    }
                });
            });
        }

        column.Item().PaddingTop(24);
    }

    private void ComposeServicesTable(ColumnDescriptor column, List<PdfLineItem> lineItems, string currencySymbol, string accentColor, string itemsLayout, bool showItemDescriptions, InvoiceLabelSet L, CultureInfo culture)
    {
        // Compute a light version of the accent color for the section header background
        var accentLight = GetLightVariant(accentColor);

        // Section header with vertically centered icon and text
        column.Item().Background(accentLight).Padding(8).Row(row =>
        {
            row.AutoItem().AlignMiddle().PaddingRight(8).Text("◉").FontSize(12).FontColor(accentColor);
            row.AutoItem().AlignMiddle().Text(L.Services).FontSize(9).Bold().FontColor(accentColor).LetterSpacing(0.5f);
        });

        // Table
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.ConstantColumn(70);
                columns.ConstantColumn(70);
                columns.ConstantColumn(80);
            });

            // Header - bordered layout adds borders to header cells
            var headerBorder = itemsLayout == "bordered";
            table.Header(header =>
            {
                if (headerBorder)
                {
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .Text(L.Description).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .AlignCenter().Text(L.Hours).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Rate).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Amount).FontSize(9).Bold().FontColor(LightText);
                }
                else
                {
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .Text(L.Description).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .AlignCenter().Text(L.Hours).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Rate).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Amount).FontSize(9).Bold().FontColor(LightText);
                }
            });

            // Rows - apply layout styling
            var rowIndex = 0;
            foreach (var item in lineItems)
            {
                // Determine row background for striped layout
                var isStriped = itemsLayout == "striped" && rowIndex % 2 == 1;
                var isBordered = itemsLayout == "bordered";

                // Description cell - using fluent chaining
                // Render cell content helper
                void RenderDescriptionContent(IContainer container)
                {
                    container.Column(descCol =>
                    {
                        descCol.Item().Text(item.JobName).FontSize(10).FontColor(DarkText);
                        if (showItemDescriptions)
                        {
                            descCol.Item().PaddingTop(2).Row(detailRow =>
                            {
                                if (!string.IsNullOrEmpty(item.ProjectName))
                                {
                                    detailRow.AutoItem().Text(item.ProjectName).FontSize(9).FontColor(MediumText);
                                    detailRow.AutoItem().PaddingHorizontal(4).Text(" ").FontSize(9);
                                }
                                if (item.Date.HasValue)
                                {
                                    detailRow.AutoItem().Text(item.Date.Value.ToString("d MMM yyyy", culture)).FontSize(9).FontColor(LightText);
                                }
                                if (!string.IsNullOrEmpty(item.SectionName))
                                {
                                    detailRow.AutoItem().PaddingLeft(4).Text($"[{item.SectionName}]").FontSize(9).FontColor(IndigoColor);
                                }
                            });
                        }
                    });
                }

                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).Element(RenderDescriptionContent);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).Element(RenderDescriptionContent);
                }
                else if (isStriped)
                {
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).Element(RenderDescriptionContent);
                }
                else
                {
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).Element(RenderDescriptionContent);
                }

                // Hours cell
                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).AlignCenter().Text($"{item.Quantity:N2}").FontSize(10).FontColor(MediumText);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).AlignCenter().Text($"{item.Quantity:N2}").FontSize(10).FontColor(MediumText);
                }
                else if (isStriped)
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignCenter().Text($"{item.Quantity:N2}").FontSize(10).FontColor(MediumText);
                else
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignCenter().Text($"{item.Quantity:N2}").FontSize(10).FontColor(MediumText);

                // Rate cell
                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.UnitPrice:N2}").FontSize(10).FontColor(MediumText);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.UnitPrice:N2}").FontSize(10).FontColor(MediumText);
                }
                else if (isStriped)
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.UnitPrice:N2}").FontSize(10).FontColor(MediumText);
                else
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.UnitPrice:N2}").FontSize(10).FontColor(MediumText);

                // Amount cell
                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.LineTotal:N2}").FontSize(10).Bold().FontColor(DarkText);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.LineTotal:N2}").FontSize(10).Bold().FontColor(DarkText);
                }
                else if (isStriped)
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.LineTotal:N2}").FontSize(10).Bold().FontColor(DarkText);
                else
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.LineTotal:N2}").FontSize(10).Bold().FontColor(DarkText);

                rowIndex++;
            }
        });

        column.Item().PaddingTop(16);
    }

    private void ComposeExpensesTable(ColumnDescriptor column, List<Expense> expenses, string currencySymbol, string itemsLayout, bool showItemDescriptions, InvoiceLabelSet L, CultureInfo culture, string language)
    {
        // Section header with vertically centered icon and text
        column.Item().Background(AmberLight).Padding(8).Row(row =>
        {
            row.AutoItem().AlignMiddle().PaddingRight(8).Text("▣").FontSize(12).FontColor(AmberColor);
            row.AutoItem().AlignMiddle().Text(L.Expenses).FontSize(9).Bold().FontColor(AmberColor).LetterSpacing(0.5f);
        });

        // Table
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
            });

            // Header - bordered layout adds borders to header cells
            var headerBorder = itemsLayout == "bordered";
            table.Header(header =>
            {
                if (headerBorder)
                {
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .Text(L.Description).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .Text(L.Category).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Date).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Amount).FontSize(9).Bold().FontColor(LightText);
                }
                else
                {
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .Text(L.Description).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .Text(L.Category).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Date).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Amount).FontSize(9).Bold().FontColor(LightText);
                }
            });

            // Rows - apply layout styling
            var rowIndex = 0;
            foreach (var expense in expenses)
            {
                // Determine row background for striped layout
                var isStriped = itemsLayout == "striped" && rowIndex % 2 == 1;
                var isBordered = itemsLayout == "bordered";

                // Description cell - using fluent chaining
                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).Column(descCol =>
                        {
                            descCol.Item().Text(expense.MerchantName).FontSize(10).FontColor(DarkText);
                            if (showItemDescriptions && expense.Project != null)
                                descCol.Item().PaddingTop(2).Text(expense.Project.Name).FontSize(9).FontColor(LightText);
                        });
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).Column(descCol =>
                        {
                            descCol.Item().Text(expense.MerchantName).FontSize(10).FontColor(DarkText);
                            if (showItemDescriptions && expense.Project != null)
                                descCol.Item().PaddingTop(2).Text(expense.Project.Name).FontSize(9).FontColor(LightText);
                        });
                }
                else if (isStriped)
                {
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).Column(descCol =>
                    {
                        descCol.Item().Text(expense.MerchantName).FontSize(10).FontColor(DarkText);
                        if (showItemDescriptions && expense.Project != null)
                            descCol.Item().PaddingTop(2).Text(expense.Project.Name).FontSize(9).FontColor(LightText);
                    });
                }
                else
                {
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).Column(descCol =>
                    {
                        descCol.Item().Text(expense.MerchantName).FontSize(10).FontColor(DarkText);
                        if (showItemDescriptions && expense.Project != null)
                            descCol.Item().PaddingTop(2).Text(expense.Project.Name).FontSize(9).FontColor(LightText);
                    });
                }

                // Category cell
                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).Text(InvoiceLabels.ExpenseCategory(language, expense.Category.ToString())).FontSize(10).FontColor(MediumText);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).Text(InvoiceLabels.ExpenseCategory(language, expense.Category.ToString())).FontSize(10).FontColor(MediumText);
                }
                else if (isStriped)
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).Text(InvoiceLabels.ExpenseCategory(language, expense.Category.ToString())).FontSize(10).FontColor(MediumText);
                else
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).Text(InvoiceLabels.ExpenseCategory(language, expense.Category.ToString())).FontSize(10).FontColor(MediumText);

                // Date cell
                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text(expense.ExpenseDate.ToString("d MMM yyyy", culture)).FontSize(10).FontColor(MediumText);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text(expense.ExpenseDate.ToString("d MMM yyyy", culture)).FontSize(10).FontColor(MediumText);
                }
                else if (isStriped)
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text(expense.ExpenseDate.ToString("d MMM yyyy", culture)).FontSize(10).FontColor(MediumText);
                else
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text(expense.ExpenseDate.ToString("d MMM yyyy", culture)).FontSize(10).FontColor(MediumText);

                // Amount cell
                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{expense.GetTotalAmount():N2}").FontSize(10).Bold().FontColor(DarkText);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{expense.GetTotalAmount():N2}").FontSize(10).Bold().FontColor(DarkText);
                }
                else if (isStriped)
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{expense.GetTotalAmount():N2}").FontSize(10).Bold().FontColor(DarkText);
                else
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{expense.GetTotalAmount():N2}").FontSize(10).Bold().FontColor(DarkText);

                rowIndex++;
            }
        });

        column.Item().PaddingTop(16);
    }

    private void ComposeProductsTable(ColumnDescriptor column, List<InvoiceLineItem> productLineItems, string currencySymbol, string itemsLayout, bool showItemDescriptions, InvoiceLabelSet L, CultureInfo culture, Func<string?, string> Tr)
    {
        // Section header with vertically centered icon and text (blue theme)
        column.Item().Background(BlueLight).Padding(8).Row(row =>
        {
            row.AutoItem().AlignMiddle().PaddingRight(8).Text("▢").FontSize(12).FontColor(BlueColor);
            row.AutoItem().AlignMiddle().Text(L.Products).FontSize(9).Bold().FontColor(BlueColor).LetterSpacing(0.5f);
        });

        // Table
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.ConstantColumn(70);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
            });

            // Header - bordered layout adds borders to header cells
            var headerBorder = itemsLayout == "bordered";
            table.Header(header =>
            {
                if (headerBorder)
                {
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .Text(L.Description).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .AlignCenter().Text(L.Qty).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Rate).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Amount).FontSize(9).Bold().FontColor(LightText);
                }
                else
                {
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .Text(L.Description).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .AlignCenter().Text(L.Qty).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Rate).FontSize(9).Bold().FontColor(LightText);
                    header.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12)
                        .AlignRight().Text(L.Amount).FontSize(9).Bold().FontColor(LightText);
                }
            });

            // Rows - apply layout styling
            var rowIndex = 0;
            foreach (var item in productLineItems.OrderBy(p => p.Order))
            {
                // Determine row background for striped layout
                var isStriped = itemsLayout == "striped" && rowIndex % 2 == 1;
                var isBordered = itemsLayout == "bordered";

                // Description cell - show product name if linked
                void RenderDescriptionContent(IContainer container)
                {
                    container.Column(descCol =>
                    {
                        descCol.Item().Text(Tr(item.Description)).FontSize(10).FontColor(DarkText);
                        if (showItemDescriptions && item.Product != null)
                        {
                            descCol.Item().PaddingTop(2).Text($"Product: {item.Product.Name}").FontSize(9).FontColor(LightText);
                        }
                    });
                }

                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).Element(RenderDescriptionContent);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).Element(RenderDescriptionContent);
                }
                else if (isStriped)
                {
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).Element(RenderDescriptionContent);
                }
                else
                {
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).Element(RenderDescriptionContent);
                }

                // Quantity cell
                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).AlignCenter().Text($"{item.Quantity:N2}").FontSize(10).FontColor(MediumText);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).AlignCenter().Text($"{item.Quantity:N2}").FontSize(10).FontColor(MediumText);
                }
                else if (isStriped)
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignCenter().Text($"{item.Quantity:N2}").FontSize(10).FontColor(MediumText);
                else
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignCenter().Text($"{item.Quantity:N2}").FontSize(10).FontColor(MediumText);

                // Rate cell
                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.UnitPrice.Amount:N2}").FontSize(10).FontColor(MediumText);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.UnitPrice.Amount:N2}").FontSize(10).FontColor(MediumText);
                }
                else if (isStriped)
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.UnitPrice.Amount:N2}").FontSize(10).FontColor(MediumText);
                else
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.UnitPrice.Amount:N2}").FontSize(10).FontColor(MediumText);

                // Amount cell
                if (isBordered)
                {
                    if (isStriped)
                        table.Cell().Background(BackgroundLight).Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.LineTotal.Amount:N2}").FontSize(10).Bold().FontColor(DarkText);
                    else
                        table.Cell().Border(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.LineTotal.Amount:N2}").FontSize(10).Bold().FontColor(DarkText);
                }
                else if (isStriped)
                    table.Cell().Background(BackgroundLight).BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.LineTotal.Amount:N2}").FontSize(10).Bold().FontColor(DarkText);
                else
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(12).AlignRight().Text($"{currencySymbol}{item.LineTotal.Amount:N2}").FontSize(10).Bold().FontColor(DarkText);

                rowIndex++;
            }
        });

        column.Item().PaddingTop(16);
    }

    private void ComposeTotals(ColumnDescriptor column, Invoice invoice, decimal subtotal, decimal taxAmount, decimal total, string currencySymbol, string primaryColor, string footerLayout, InvoiceLabelSet L, CultureInfo culture)
    {
        // Totals section with light gray background matching the web view
        column.Item().Background(BackgroundLight).BorderTop(1).BorderColor(BorderColor).Padding(20).AlignRight().Width(240).Column(totalsCol =>
        {
            if (footerLayout == "detailed")
            {
                // Detailed footer: always show subtotal, tax, and total
                totalsCol.Item().Row(row =>
                {
                    row.RelativeItem().Text(L.Subtotal).FontSize(10).FontColor(LightText);
                    row.ConstantItem(100).AlignRight().Text($"{currencySymbol}{subtotal:N2}").FontSize(10).FontColor(DarkText);
                });

                if (invoice.TaxRate > 0)
                {
                    totalsCol.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Text($"Tax ({invoice.TaxRate:N1}%)").FontSize(10).FontColor(LightText);
                        row.ConstantItem(100).AlignRight().Text($"{currencySymbol}{taxAmount:N2}").FontSize(10).FontColor(DarkText);
                    });
                }
                else
                {
                    totalsCol.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Text(L.Tax).FontSize(10).FontColor(LightText);
                        row.ConstantItem(100).AlignRight().Text($"{currencySymbol}0.00").FontSize(10).FontColor(DarkText);
                    });
                }

                totalsCol.Item().PaddingTop(12).BorderTop(1).BorderColor(BorderColor).PaddingTop(12);
                totalsCol.Item().Row(row =>
                {
                    row.RelativeItem().Text(L.Total).FontSize(12).Bold().FontColor(DarkText);
                    var totalColor = invoice.Status == Domain.Enums.InvoiceStatus.Paid ? EmeraldColor : primaryColor;
                    row.ConstantItem(120).AlignRight().Text($"{currencySymbol}{total:N2}").FontSize(20).Bold().FontColor(totalColor);
                });
            }
            else
            {
                // Standard footer: show subtotal/tax only if tax applies, otherwise just total
                if (invoice.TaxRate > 0)
                {
                    totalsCol.Item().Row(row =>
                    {
                        row.RelativeItem().Text(L.Subtotal).FontSize(10).FontColor(LightText);
                        row.ConstantItem(100).AlignRight().Text($"{currencySymbol}{subtotal:N2}").FontSize(10).FontColor(DarkText);
                    });

                    totalsCol.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Text($"Tax ({invoice.TaxRate:N1}%)").FontSize(10).FontColor(LightText);
                        row.ConstantItem(100).AlignRight().Text($"{currencySymbol}{taxAmount:N2}").FontSize(10).FontColor(DarkText);
                    });

                    totalsCol.Item().PaddingTop(12).BorderTop(1).BorderColor(BorderColor).PaddingTop(12);
                }

                totalsCol.Item().Row(row =>
                {
                    row.RelativeItem().Text(L.Total).FontSize(12).Bold().FontColor(DarkText);
                    var totalColor = invoice.Status == Domain.Enums.InvoiceStatus.Paid ? EmeraldColor : primaryColor;
                    row.ConstantItem(120).AlignRight().Text($"{currencySymbol}{total:N2}").FontSize(20).Bold().FontColor(totalColor);
                });
            }
        });
    }

    private void ComposeRequiredStatements(ColumnDescriptor column, IReadOnlyList<string>? statements)
    {
        if (statements is null || statements.Count == 0) return;

        column.Item().PaddingTop(10).Column(col =>
        {
            foreach (var statement in statements)
                col.Item().Text(statement).FontSize(9).Bold();
        });
    }

    private void ComposeNotes(ColumnDescriptor column, Invoice invoice, BusinessProfile? businessProfile, InvoiceLabelSet L, Func<string?, string> Tr)
    {
        var notes = Tr(invoice.Notes) is { Length: > 0 } n ? n : businessProfile?.InvoiceNotes;
        if (string.IsNullOrEmpty(notes)) return;

        column.Item().Background(BackgroundLight).Padding(16).Row(row =>
        {
            row.AutoItem().AlignTop().PaddingRight(10).Text("📝").FontSize(14);
            row.RelativeItem().Column(notesCol =>
            {
                notesCol.Item().Text(L.Notes).FontSize(9).Bold().FontColor(LightText);
                notesCol.Item().PaddingTop(4).Text(notes).FontSize(10).FontColor(MediumText);
            });
        });

        column.Item().PaddingTop(16);
    }

    private void ComposePaymentOptions(ColumnDescriptor column, Invoice invoice, BusinessProfile? businessProfile, CurrencyPaymentSettings? currencySettings, decimal total, bool showPaymentQR, bool showBankDetails, string? defaultCurrency, decimal? vndToDefaultCurrencyRate, InvoiceLabelSet L)
    {
        if (invoice.Status == Domain.Enums.InvoiceStatus.Paid || invoice.Status == Domain.Enums.InvoiceStatus.Void)
            return;

        var hasOnlinePayments = businessProfile != null &&
            (!string.IsNullOrEmpty(businessProfile.PayPalMeUsername) ||
             !string.IsNullOrEmpty(businessProfile.WiseEmail) ||
             !string.IsNullOrEmpty(businessProfile.RevolutUsername));
        var hasBankDetails = showBankDetails && currencySettings != null && !string.IsNullOrEmpty(currencySettings.BankAccountNumber);
        var hasVietQR = showPaymentQR && invoice.Currency == "VND" && currencySettings != null &&
                        !string.IsNullOrEmpty(currencySettings.VietQrBankCode) &&
                        !string.IsNullOrEmpty(currencySettings.BankAccountNumber);

        // For VND invoices, only show online payments if conversion rate is set
        var canShowOnlinePayments = hasOnlinePayments && (invoice.Currency != "VND" || vndToDefaultCurrencyRate.HasValue);

        if (!canShowOnlinePayments && !hasBankDetails && !hasVietQR) return;

        // Calculate payment amount (convert VND if needed)
        var paymentAmount = total;
        var paymentCurrency = invoice.Currency;
        if (invoice.Currency == "VND" && vndToDefaultCurrencyRate.HasValue && vndToDefaultCurrencyRate.Value > 0)
        {
            paymentAmount = Math.Round(total / vndToDefaultCurrencyRate.Value, 2);
            paymentCurrency = defaultCurrency ?? "USD";
        }

        // Keep payment options together - move to new page if can't fit
        column.Item().ShowEntire().Background(BlueLight).Padding(16).Column(paymentCol =>
        {
            paymentCol.Item().Text(L.PaymentOptions).FontSize(9).Bold().FontColor(LightText).LetterSpacing(0.5f);
            paymentCol.Item().PaddingTop(12);

            var showedOnlinePayments = false;

            // Online payment links (show for non-VND, or VND with conversion rate)
            if (canShowOnlinePayments)
            {
                showedOnlinePayments = true;
                paymentCol.Item().Row(row =>
                {
                    if (!string.IsNullOrEmpty(businessProfile?.PayPalMeUsername))
                    {
                        var amount = paymentAmount.ToString("F2", CultureInfo.InvariantCulture);
                        var payPalLink = $"https://paypal.me/{businessProfile.PayPalMeUsername}/{amount}";
                        row.AutoItem().PaddingRight(16).Column(ppCol =>
                        {
                            var label = invoice.Currency == "VND" ? $"PayPal ({paymentCurrency})" : "PayPal";
                            ppCol.Item().Text(label).FontSize(9).Bold().FontColor(DarkText);
                            ppCol.Item().Text(payPalLink).FontSize(9).FontColor(BlueColor);
                        });
                    }
                    if (!string.IsNullOrEmpty(businessProfile?.WiseEmail))
                    {
                        var amount = paymentAmount.ToString("F2", CultureInfo.InvariantCulture);
                        var email = Uri.EscapeDataString(businessProfile.WiseEmail);
                        var wiseLink = $"https://wise.com/pay/me?email={email}&amount={amount}&currency={paymentCurrency}";
                        row.AutoItem().PaddingRight(16).Column(wiseCol =>
                        {
                            var label = invoice.Currency == "VND" ? $"Wise ({paymentCurrency})" : "Wise";
                            wiseCol.Item().Text(label).FontSize(9).Bold().FontColor(DarkText);
                            wiseCol.Item().Text(wiseLink).FontSize(9).FontColor(BlueColor);
                        });
                    }
                    if (!string.IsNullOrEmpty(businessProfile?.RevolutUsername))
                    {
                        // Revolut payment link with amount in smallest currency unit (pence/cents)
                        var amountInSmallestUnit = (long)(paymentAmount * 100);
                        var revolutLink = $"https://revolut.me/{businessProfile.RevolutUsername}?currency={paymentCurrency}&amount={amountInSmallestUnit}";
                        row.AutoItem().Column(revCol =>
                        {
                            var label = invoice.Currency == "VND" ? $"Revolut ({paymentCurrency})" : "Revolut";
                            revCol.Item().Text(label).FontSize(9).Bold().FontColor(DarkText);
                            revCol.Item().Text(revolutLink).FontSize(9).FontColor(BlueColor);
                        });
                    }
                });
            }

            // VietQR for VND
            if (hasVietQR)
            {
                var qrImage = FetchVietQRImage(currencySettings!, invoice, total);
                if (qrImage != null)
                {
                    if (showedOnlinePayments) paymentCol.Item().PaddingTop(16);
                    paymentCol.Item().Text($"{L.BankTransfer} (VietQR)").FontSize(9).Bold().FontColor(DarkText);
                    paymentCol.Item().PaddingTop(8).Row(row =>
                    {
                        row.ConstantItem(100).Image(qrImage);
                        row.RelativeItem().PaddingLeft(12).Column(infoCol =>
                        {
                            infoCol.Item().Text(L.ScanWithBankingApp).FontSize(9).FontColor(DarkText);
                            infoCol.Item().PaddingTop(4).Text($"Amount: {(long)total:N0} VND").FontSize(9).FontColor(MediumText);
                            if (!string.IsNullOrEmpty(currencySettings.BankName))
                                infoCol.Item().Text($"Bank: {currencySettings.BankName}").FontSize(9).FontColor(MediumText);
                            if (!string.IsNullOrEmpty(currencySettings.BankAccountName))
                                infoCol.Item().Text($"Account: {currencySettings.BankAccountName}").FontSize(9).FontColor(MediumText);
                            if (!string.IsNullOrEmpty(currencySettings.BankAccountNumber))
                                infoCol.Item().Text($"Account No: {currencySettings.BankAccountNumber}").FontSize(9).FontColor(MediumText);
                            infoCol.Item().Text($"Reference: Invoice {invoice.InvoiceNumber}").FontSize(9).FontColor(MediumText);
                        });
                    });
                }
            }
            // Bank transfer for non-VND
            else if (hasBankDetails && invoice.Currency != "VND")
            {
                if (showedOnlinePayments) paymentCol.Item().PaddingTop(12).BorderTop(1).BorderColor(BorderColor).PaddingTop(12);
                paymentCol.Item().Text(L.BankTransfer).FontSize(9).Bold().FontColor(DarkText);
                paymentCol.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Column(bankCol =>
                    {
                        if (!string.IsNullOrEmpty(currencySettings?.BankName))
                            bankCol.Item().Text($"Bank: {currencySettings.BankName}").FontSize(9).FontColor(MediumText);
                        if (!string.IsNullOrEmpty(currencySettings?.BankAccountName))
                            bankCol.Item().Text($"Account Name: {currencySettings.BankAccountName}").FontSize(9).FontColor(MediumText);
                        if (!string.IsNullOrEmpty(currencySettings?.BankAccountNumber))
                            bankCol.Item().Text($"Account No: {currencySettings.BankAccountNumber}").FontSize(9).FontColor(MediumText);
                    });
                    row.RelativeItem().Column(bankCol2 =>
                    {
                        if (!string.IsNullOrEmpty(currencySettings?.BankSortCode))
                            bankCol2.Item().Text($"Sort Code: {currencySettings.BankSortCode}").FontSize(9).FontColor(MediumText);
                        if (!string.IsNullOrEmpty(currencySettings?.BankIban))
                            bankCol2.Item().Text($"IBAN: {currencySettings.BankIban}").FontSize(9).FontColor(MediumText);
                        if (!string.IsNullOrEmpty(currencySettings?.BankSwift))
                            bankCol2.Item().Text($"SWIFT/BIC: {currencySettings.BankSwift}").FontSize(9).FontColor(MediumText);

                        // Correspondent routing, printed only when supplied. A wire
                        // from Asia is commonly returned without it.
                        if (!string.IsNullOrEmpty(currencySettings?.BankAddress))
                            bankCol2.Item().Text($"Bank address: {currencySettings.BankAddress}").FontSize(9).FontColor(MediumText);
                        if (!string.IsNullOrEmpty(currencySettings?.IntermediaryBankName))
                            bankCol2.Item().Text($"Correspondent bank: {currencySettings.IntermediaryBankName}").FontSize(9).FontColor(MediumText);
                        if (!string.IsNullOrEmpty(currencySettings?.IntermediaryBankSwift))
                            bankCol2.Item().Text($"Correspondent SWIFT/BIC: {currencySettings.IntermediaryBankSwift}").FontSize(9).FontColor(MediumText);
                        if (!string.IsNullOrEmpty(currencySettings?.IntermediaryAccountNumber))
                            bankCol2.Item().Text($"Account at correspondent: {currencySettings.IntermediaryAccountNumber}").FontSize(9).FontColor(MediumText);
                        if (!string.IsNullOrEmpty(currencySettings?.PaymentPurposeCode))
                            bankCol2.Item().Text($"Payment purpose: {currencySettings.PaymentPurposeCode}").FontSize(9).FontColor(MediumText);
                    });
                });
                paymentCol.Item().PaddingTop(4).Text($"Reference: Invoice {invoice.InvoiceNumber}").FontSize(9).FontColor(DarkText);
            }
        });

        column.Item().PaddingTop(16);
    }

    private static byte[]? FetchVietQRImage(CurrencyPaymentSettings currencySettings, Invoice invoice, decimal total)
    {
        try
        {
            var bankId = currencySettings.VietQrBankCode;
            var accountNo = currencySettings.BankAccountNumber;
            var amount = ((long)total).ToString();
            var description = Uri.EscapeDataString($"Invoice {invoice.InvoiceNumber}");
            var accountName = Uri.EscapeDataString(currencySettings.BankAccountName ?? "");

            var url = $"https://img.vietqr.io/image/{bankId}-{accountNo}-compact.png?amount={amount}&addInfo={description}&accountName={accountName}";

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            if (response.IsSuccessStatusCode)
            {
                return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            }
        }
        catch
        {
            // Silently fail - QR code is optional
        }
        return null;
    }

    /// <summary>
    /// Converts a hex color to a light variant (for backgrounds)
    /// </summary>
    private static string GetLightVariant(string hexColor)
    {
        // Remove # if present
        var hex = hexColor.TrimStart('#');

        // Parse RGB values
        if (hex.Length != 6) return BackgroundLight;

        try
        {
            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex[2..4], 16);
            var b = Convert.ToInt32(hex[4..6], 16);

            // Mix with white (light variant = 90% white, 10% color)
            r = (int)(r * 0.1 + 255 * 0.9);
            g = (int)(g * 0.1 + 255 * 0.9);
            b = (int)(b * 0.1 + 255 * 0.9);

            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch
        {
            return BackgroundLight;
        }
    }

    public Task<byte[]> GenerateEstimatePdfAsync(Estimate estimate, BusinessProfile? businessProfile = null)
    {
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var subtotal = estimate.CalculateSubtotal().Amount;
            var taxAmount = estimate.CalculateTax().Amount;
            var total = estimate.CalculateTotal().Amount;
            var currencySymbol = GetCurrencySymbol(estimate.Currency);

            // Use template colors and settings if available, otherwise use defaults
            var template = estimate.Template;
            var primaryColor = template?.PrimaryColor ?? IndigoColor;
            var accentColor = template?.AccentColor ?? EmeraldColor;
            var showLogo = template?.ShowLogo ?? true;
            var headerLayout = template?.HeaderLayout?.ToLowerInvariant() ?? "standard";
            var itemsLayout = template?.ItemsLayout?.ToLowerInvariant() ?? "standard";
            var footerLayout = template?.FooterLayout?.ToLowerInvariant() ?? "standard";

            var isExpired = estimate.Status != Domain.Enums.EstimateStatus.Accepted &&
                           estimate.Status != Domain.Enums.EstimateStatus.Rejected &&
                           estimate.Status != Domain.Enums.EstimateStatus.Converted &&
                           estimate.ExpiryDate.HasValue &&
                           DateOnly.FromDateTime(DateTime.Today) > estimate.ExpiryDate.Value;
            var isAccepted = estimate.Status == Domain.Enums.EstimateStatus.Accepted;
            var isRejected = estimate.Status == Domain.Enums.EstimateStatus.Rejected;
            var isConverted = estimate.Status == Domain.Enums.EstimateStatus.Converted;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(x => BaseTextStyle(x.FontSize(10)).FontColor(DarkText));

                    page.Content().Column(mainColumn =>
                    {
                        // Status Banner - matching EstimateViewContent exactly
                        ComposeEstimateStatusBannerV2(mainColumn, estimate, isExpired, isAccepted, isRejected, isConverted);

                        // Header Section with padding
                        mainColumn.Item().Padding(32).Column(contentColumn =>
                        {
                            // Header - matching EstimateViewContent layout
                            ComposeEstimateHeaderV2(contentColumn, estimate, businessProfile, isExpired, isAccepted, primaryColor, showLogo, headerLayout, currencySymbol, total);

                            // Prepared For Section
                            ComposeEstimatePreparedForV2(contentColumn, estimate, total, currencySymbol, primaryColor, isAccepted, headerLayout);
                        });

                        // Line Items Section - full width
                        ComposeEstimateLineItemsV2(mainColumn, estimate, currencySymbol, accentColor, itemsLayout);

                        // Totals Section
                        ComposeEstimateTotalsV2(mainColumn, estimate, subtotal, taxAmount, total, currencySymbol, primaryColor, isAccepted, footerLayout);

                        // Notes Section
                        if (!string.IsNullOrEmpty(estimate.Notes))
                        {
                            ComposeEstimateNotesV2(mainColumn, estimate.Notes);
                        }

                        // Terms Section
                        if (!string.IsNullOrEmpty(estimate.Terms))
                        {
                            ComposeEstimateTermsV2(mainColumn, estimate.Terms);
                        }
                    });

                    // Footer at bottom of page (repeating footer like invoices)
                    var footerText = estimate.ExpiryDate.HasValue
                        ? $"This estimate is valid until {estimate.ExpiryDate.Value:MMMM d, yyyy}."
                        : "Thank you for considering our services.";
                    page.Footer().Background(BackgroundLight).BorderTop(1).BorderColor(BorderColor).Padding(16)
                        .AlignCenter().Text(footerText)
                        .FontSize(10).FontColor(VeryLightText);
                });
            });

            var bytes = document.GeneratePdf();
            return Task.FromResult(bytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to generate estimate PDF: {ex.Message}", ex);
        }
    }

    private static void ComposeEstimateStatusBannerV2(ColumnDescriptor column, Estimate estimate, bool isExpired, bool isAccepted, bool isRejected, bool isConverted)
    {
        if (isAccepted)
        {
            column.Item().Background(EmeraldLight).Padding(16).Row(row =>
            {
                row.AutoItem().AlignMiddle().PaddingRight(12)
                    .Width(24).Height(24).Background(EmeraldColor)
                    .AlignCenter().AlignMiddle()
                    .Text("✓").FontSize(14).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Accepted").FontSize(12).Bold().FontColor(EmeraldColor);
                        if (estimate.AcceptedDate.HasValue)
                        {
                            text.Span("  ·  ").FontColor(EmeraldColor);
                            text.Span(estimate.AcceptedDate.Value.ToString("MMMM d, yyyy")).FontSize(11).FontColor(EmeraldColor);
                        }
                    });
                });
            });
        }
        else if (isRejected)
        {
            column.Item().Background(RedLight).Padding(16).Row(row =>
            {
                row.AutoItem().AlignMiddle().PaddingRight(12)
                    .Width(24).Height(24).Background(RedColor)
                    .AlignCenter().AlignMiddle()
                    .Text("✗").FontSize(14).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Rejected").FontSize(12).Bold().FontColor(RedColor);
                        if (estimate.RejectedDate.HasValue)
                        {
                            text.Span("  ·  ").FontColor(RedColor);
                            text.Span(estimate.RejectedDate.Value.ToString("MMMM d, yyyy")).FontSize(11).FontColor(RedColor);
                        }
                    });
                });
            });
        }
        else if (isConverted)
        {
            column.Item().Background(PurpleLight).Padding(16).Row(row =>
            {
                row.AutoItem().AlignMiddle().PaddingRight(12)
                    .Width(24).Height(24).Background(PurpleColor)
                    .AlignCenter().AlignMiddle()
                    .Text("→").FontSize(14).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignMiddle().Text("Converted to Invoice").FontSize(12).Bold().FontColor(PurpleColor);
            });
        }
        else if (isExpired)
        {
            var daysPastExpiry = DateOnly.FromDateTime(DateTime.Today).DayNumber - estimate.ExpiryDate!.Value.DayNumber;
            column.Item().Background(AmberLight).Padding(16).Row(row =>
            {
                row.AutoItem().AlignMiddle().PaddingRight(12)
                    .Width(24).Height(24).Background(AmberColor)
                    .AlignCenter().AlignMiddle()
                    .Text("!").FontSize(14).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Expired").FontSize(12).Bold().FontColor(AmberColor);
                        text.Span("  ·  ").FontColor(AmberColor);
                        text.Span($"{daysPastExpiry} days ago").FontSize(11).FontColor(AmberColor);
                    });
                });
            });
        }
        else if (estimate.Status == Domain.Enums.EstimateStatus.Sent)
        {
            column.Item().Background(BlueLight).Padding(16).Row(row =>
            {
                row.AutoItem().AlignMiddle().PaddingRight(12)
                    .Width(24).Height(24).Background(BlueColor)
                    .AlignCenter().AlignMiddle()
                    .Text("⏳").FontSize(12).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Awaiting Response").FontSize(12).Bold().FontColor(BlueColor);
                        if (estimate.ExpiryDate.HasValue)
                        {
                            text.Span("  ·  ").FontColor(BlueColor);
                            text.Span($"Valid until {estimate.ExpiryDate.Value:MMMM d, yyyy}").FontSize(11).FontColor(BlueColor);
                        }
                    });
                });
            });
        }
        else if (estimate.Status == Domain.Enums.EstimateStatus.Draft)
        {
            column.Item().Background(AmberLight).Padding(16).Row(row =>
            {
                row.AutoItem().AlignMiddle().PaddingRight(12)
                    .Width(24).Height(24).Background(AmberColor)
                    .AlignCenter().AlignMiddle()
                    .Text("✎").FontSize(14).Bold().FontColor(Colors.White);
                row.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("Draft").FontSize(12).Bold().FontColor(AmberColor);
                        text.Span("  ·  ").FontColor(AmberColor);
                        text.Span("Not yet sent").FontSize(11).FontColor(AmberColor);
                    });
                });
            });
        }
    }

    private static void ComposeEstimateHeaderV2(ColumnDescriptor column, Estimate estimate, BusinessProfile? businessProfile, bool isExpired, bool isAccepted, string primaryColor, bool showLogo, string headerLayout, string currencySymbol, decimal total)
    {
        var businessName = (string.IsNullOrWhiteSpace(businessProfile?.TradingName) ? businessProfile?.CompanyName : businessProfile.TradingName) ?? "Your Company";
        var addressStr = businessProfile?.Address?.ToString();

        if (headerLayout == "centered")
        {
            // Centered Header Layout with colored background
            column.Item().Background(primaryColor).Padding(20).Column(headerCol =>
            {
                if (showLogo && businessProfile?.Logo != null)
                {
                    headerCol.Item().AlignCenter().Height(40).Image(businessProfile.Logo).FitHeight();
                    headerCol.Item().PaddingTop(8);
                }
                headerCol.Item().AlignCenter().Text(businessName).FontSize(18).Bold().FontColor(Colors.White);
                if (!string.IsNullOrEmpty(addressStr))
                {
                    headerCol.Item().PaddingTop(4).AlignCenter().Text(addressStr).FontSize(10).FontColor(Colors.White.WithAlpha(200));
                }
            });

            // Estimate Number & Dates centered below
            column.Item().PaddingTop(20).AlignCenter().Column(centerCol =>
            {
                centerCol.Item().AlignCenter().Text(estimate.EstimateNumber).FontSize(28).Bold().FontColor(DarkText);
                centerCol.Item().AlignCenter().Text("ESTIMATE").FontSize(11).FontColor(LightText);
                centerCol.Item().PaddingTop(12).AlignCenter().Row(dateRow =>
                {
                    dateRow.AutoItem().Text(text =>
                    {
                        text.Span("Date: ").FontColor(LightText);
                        text.Span(estimate.EstimateDate.ToString("MMM d, yyyy")).Bold().FontColor(DarkText);
                    });
                    if (estimate.ExpiryDate.HasValue)
                    {
                        dateRow.AutoItem().PaddingLeft(24).Text(text =>
                        {
                            text.Span("Valid Until: ").FontColor(LightText);
                            var expiryColor = isExpired ? AmberColor : DarkText;
                            text.Span(estimate.ExpiryDate.Value.ToString("MMM d, yyyy")).Bold().FontColor(expiryColor);
                        });
                    }
                });
            });
        }
        else if (headerLayout == "minimal")
        {
            // Minimal Header Layout
            column.Item().Row(row =>
            {
                row.RelativeItem().Row(leftRow =>
                {
                    if (showLogo && businessProfile?.Logo != null)
                    {
                        leftRow.AutoItem().Height(40).Image(businessProfile.Logo).FitHeight();
                        leftRow.AutoItem().PaddingLeft(12);
                    }
                    leftRow.RelativeItem().Column(leftCol =>
                    {
                        leftCol.Item().Text(estimate.EstimateNumber).FontSize(24).Bold().FontColor(DarkText);
                        leftCol.Item().Text(text =>
                        {
                            text.Span(estimate.EstimateDate.ToString("MMM d, yyyy")).FontSize(10).FontColor(LightText);
                            if (estimate.ExpiryDate.HasValue)
                            {
                                text.Span($" — Valid until {estimate.ExpiryDate.Value:MMM d, yyyy}").FontSize(10).FontColor(LightText);
                            }
                        });
                    });
                });
                row.ConstantItem(150).AlignRight().Column(rightCol =>
                {
                    var totalColor = isAccepted ? EmeraldColor : primaryColor;
                    rightCol.Item().AlignRight().Text($"{currencySymbol}{total:N2}").FontSize(28).Bold().FontColor(totalColor);
                });
            });
        }
        else
        {
            // Standard Header Layout (default) with colored background block
            column.Item().Background(primaryColor).Padding(20).Column(headerCol =>
            {
                if (showLogo && businessProfile?.Logo != null)
                {
                    headerCol.Item().Height(40).Image(businessProfile.Logo).FitHeight();
                    headerCol.Item().PaddingTop(8);
                }
                headerCol.Item().Text(businessName).FontSize(18).Bold().FontColor(Colors.White);
                if (!string.IsNullOrEmpty(addressStr))
                {
                    headerCol.Item().PaddingTop(4).Text(addressStr).FontSize(10).FontColor(Colors.White.WithAlpha(200));
                }
            });

            // Contact info & Estimate Number below colored header
            column.Item().PaddingTop(16).Row(row =>
            {
                // Left side - Contact info
                row.RelativeItem().Column(leftCol =>
                {
                    if (!string.IsNullOrEmpty(businessProfile?.Email?.Value))
                        leftCol.Item().Text(businessProfile.Email.Value).FontSize(10).FontColor(LightText);
                    if (!string.IsNullOrEmpty(businessProfile?.Phone?.Value))
                        leftCol.Item().Text(businessProfile.Phone.Value).FontSize(10).FontColor(LightText);
                    if (!string.IsNullOrEmpty(businessProfile?.TaxNumber))
                        leftCol.Item().PaddingTop(4).Text($"Tax/VAT: {businessProfile.TaxNumber}").FontSize(9).FontColor(VeryLightText);
                    if (!string.IsNullOrEmpty(businessProfile?.RegistrationNumber))
                        leftCol.Item().Text($"Reg: {businessProfile.RegistrationNumber}").FontSize(9).FontColor(VeryLightText);
                });

                // Right side - Estimate Number & Dates
                row.ConstantItem(200).AlignRight().Column(rightCol =>
                {
                    rightCol.Item().AlignRight().Text(estimate.EstimateNumber).FontSize(28).Bold().FontColor(DarkText);
                    rightCol.Item().AlignRight().Text("ESTIMATE").FontSize(11).FontColor(LightText);
                    rightCol.Item().PaddingTop(12).AlignRight().Row(dateRow =>
                    {
                        dateRow.AutoItem().Text(text =>
                        {
                            text.Span("Date: ").FontColor(LightText);
                            text.Span(estimate.EstimateDate.ToString("MMM d, yyyy")).Bold().FontColor(DarkText);
                        });
                        if (estimate.ExpiryDate.HasValue)
                        {
                            dateRow.AutoItem().PaddingLeft(16).Text(text =>
                            {
                                text.Span("Valid Until: ").FontColor(LightText);
                                var expiryColor = isExpired ? AmberColor : DarkText;
                                text.Span(estimate.ExpiryDate.Value.ToString("MMM d, yyyy")).Bold().FontColor(expiryColor);
                            });
                        }
                    });
                });
            });
        }
    }

    private static void ComposeEstimatePreparedForV2(ColumnDescriptor column, Estimate estimate, decimal total, string currencySymbol, string primaryColor, bool isAccepted, string headerLayout)
    {
        column.Item().PaddingTop(24).BorderTop(1).BorderColor(BorderColor).PaddingTop(20);

        column.Item().Row(row =>
        {
            // Prepared For
            row.RelativeItem().Column(billToCol =>
            {
                billToCol.Item().Text("PREPARED FOR").FontSize(9).Bold().FontColor(VeryLightText).LetterSpacing(0.5f);
                billToCol.Item().PaddingTop(6);
                billToCol.Item().Text(estimate.Client.Name).FontSize(14).SemiBold().FontColor(DarkText);

                if (!string.IsNullOrEmpty(estimate.Client.CompanyName))
                    billToCol.Item().Text(estimate.Client.CompanyName).FontSize(10).FontColor(MediumText);

                if (estimate.Client.BillingAddress != null)
                {
                    billToCol.Item().PaddingTop(6);
                    if (!string.IsNullOrEmpty(estimate.Client.BillingAddress.Street1))
                        billToCol.Item().Text(estimate.Client.BillingAddress.Street1).FontSize(10).FontColor(MediumText);
                    var cityParts = new[] { estimate.Client.BillingAddress.City, estimate.Client.BillingAddress.State, estimate.Client.BillingAddress.PostalCode }
                        .Where(s => !string.IsNullOrEmpty(s));
                    var cityLine = string.Join(", ", cityParts);
                    if (!string.IsNullOrEmpty(cityLine))
                        billToCol.Item().Text(cityLine).FontSize(10).FontColor(MediumText);
                    if (!string.IsNullOrEmpty(estimate.Client.BillingAddress.Country))
                        billToCol.Item().Text(estimate.Client.BillingAddress.Country).FontSize(10).FontColor(MediumText);
                }

                if (!string.IsNullOrEmpty(estimate.Client.Email.Value))
                    billToCol.Item().PaddingTop(4).Text(estimate.Client.Email.Value).FontSize(10).FontColor(LightText);
            });

            // Estimated Total (hide for minimal layout - shown in header)
            if (headerLayout != "minimal")
            {
                row.ConstantItem(180).AlignRight().Column(amountCol =>
                {
                    amountCol.Item().AlignRight().Text("ESTIMATED TOTAL").FontSize(9).Bold().FontColor(VeryLightText).LetterSpacing(0.5f);
                    amountCol.Item().PaddingTop(6);
                    var totalColor = isAccepted ? EmeraldColor : primaryColor;
                    amountCol.Item().AlignRight().Text($"{currencySymbol}{total:N2}").FontSize(32).Bold().FontColor(totalColor);
                    if (isAccepted)
                    {
                        amountCol.Item().AlignRight().Text("Accepted").FontSize(10).FontColor(EmeraldColor);
                    }
                });
            }
        });
    }

    private static void ComposeEstimateLineItemsV2(ColumnDescriptor mainColumn, Estimate estimate, string currencySymbol, string accentColor, string itemsLayout)
    {
        if (!estimate.LineItems.Any()) return;

        // Items header bar - uses GetLightVariant for consistent color handling like invoices
        var accentLight = GetLightVariant(accentColor);
        mainColumn.Item().Background(accentLight).BorderBottom(1).BorderColor(BorderColor).PaddingVertical(10).PaddingHorizontal(32).Row(row =>
        {
            row.AutoItem().AlignMiddle().PaddingRight(8).Text("☐").FontSize(12).FontColor(accentColor);
            row.AutoItem().AlignMiddle().Text("ITEMS").FontSize(9).Bold().FontColor(accentColor).LetterSpacing(0.5f);
        });

        // Table
        mainColumn.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(32); // Left padding
                columns.RelativeColumn(4); // Description
                columns.ConstantColumn(60); // Qty
                columns.ConstantColumn(80); // Rate
                columns.ConstantColumn(80); // Amount
                columns.ConstantColumn(32); // Right padding
            });

            // Header row
            var headerBg = BackgroundLight;
            var borderClass = itemsLayout == "bordered";

            table.Header(header =>
            {
                header.Cell().Background(headerBg);
                header.Cell().Background(headerBg).BorderBottom(1).BorderColor(BorderColor).PaddingVertical(12)
                    .Text("DESCRIPTION").FontSize(9).Bold().FontColor(LightText);
                header.Cell().Background(headerBg).BorderBottom(1).BorderColor(BorderColor).PaddingVertical(12)
                    .AlignCenter().Text("QTY").FontSize(9).Bold().FontColor(LightText);
                header.Cell().Background(headerBg).BorderBottom(1).BorderColor(BorderColor).PaddingVertical(12)
                    .AlignRight().Text("RATE").FontSize(9).Bold().FontColor(LightText);
                header.Cell().Background(headerBg).BorderBottom(1).BorderColor(BorderColor).PaddingVertical(12)
                    .AlignRight().Text("AMOUNT").FontSize(9).Bold().FontColor(LightText);
                header.Cell().Background(headerBg);
            });

            var index = 0;
            foreach (var item in estimate.LineItems.OrderBy(li => li.Order))
            {
                var rowBg = itemsLayout == "striped" && index % 2 == 0 ? BackgroundLight : "#ffffff";

                table.Cell().Background(rowBg);
                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(BorderColor).PaddingVertical(16)
                    .Text(item.Description).FontSize(10).SemiBold().FontColor(DarkText);
                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(BorderColor).PaddingVertical(16)
                    .AlignCenter().Text($"{item.Quantity:N2}").FontSize(10).FontColor(MediumText);
                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(BorderColor).PaddingVertical(16)
                    .AlignRight().Text($"{currencySymbol}{item.UnitPrice.Amount:N2}").FontSize(10).FontColor(MediumText);
                table.Cell().Background(rowBg).BorderBottom(1).BorderColor(BorderColor).PaddingVertical(16)
                    .AlignRight().Text($"{currencySymbol}{item.LineTotal.Amount:N2}").FontSize(10).SemiBold().FontColor(DarkText);
                table.Cell().Background(rowBg);

                index++;
            }
        });
    }

    private static void ComposeEstimateTotalsV2(ColumnDescriptor mainColumn, Estimate estimate, decimal subtotal, decimal taxAmount, decimal total, string currencySymbol, string primaryColor, bool isAccepted, string footerLayout)
    {
        mainColumn.Item().BorderTop(1).BorderColor(BorderColor).Background(BackgroundLight).Padding(32).Row(row =>
        {
            row.RelativeItem(); // Spacer

            row.ConstantItem(240).Column(totalsCol =>
            {
                // Show subtotal and tax if detailed layout or if there's tax
                if (footerLayout == "detailed" || estimate.TaxRate > 0)
                {
                    totalsCol.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Subtotal").FontSize(10).FontColor(LightText);
                        r.ConstantItem(100).AlignRight().Text($"{currencySymbol}{subtotal:N2}").FontSize(10).FontColor(DarkText);
                    });

                    if (estimate.TaxRate > 0)
                    {
                        totalsCol.Item().PaddingTop(8).Row(r =>
                        {
                            r.RelativeItem().Text($"Tax ({estimate.TaxRate:N1}%)").FontSize(10).FontColor(LightText);
                            r.ConstantItem(100).AlignRight().Text($"{currencySymbol}{taxAmount:N2}").FontSize(10).FontColor(DarkText);
                        });
                    }
                    else if (footerLayout == "detailed")
                    {
                        totalsCol.Item().PaddingTop(8).Row(r =>
                        {
                            r.RelativeItem().Text("Tax").FontSize(10).FontColor(LightText);
                            r.ConstantItem(100).AlignRight().Text($"{currencySymbol}0.00").FontSize(10).FontColor(DarkText);
                        });
                    }

                    totalsCol.Item().PaddingTop(12).BorderTop(1).BorderColor(BorderColor);
                }

                // Total
                totalsCol.Item().PaddingTop(8).Row(r =>
                {
                    r.RelativeItem().AlignMiddle().Text("Total").FontSize(12).SemiBold().FontColor(DarkText);
                    var totalColor = isAccepted ? EmeraldColor : primaryColor;
                    r.ConstantItem(120).AlignRight().Text($"{currencySymbol}{total:N2}").FontSize(20).Bold().FontColor(totalColor);
                });
            });
        });
    }

    private static void ComposeEstimateNotesV2(ColumnDescriptor mainColumn, string notes)
    {
        mainColumn.Item().BorderTop(1).BorderColor(BorderColor).Background(BackgroundLight).Padding(32).Row(row =>
        {
            row.AutoItem().AlignTop().PaddingRight(12).Text("✎").FontSize(14).FontColor(LightText);
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("NOTES").FontSize(9).Bold().FontColor(VeryLightText);
                col.Item().PaddingTop(6).Text(notes).FontSize(10).FontColor(MediumText);
            });
        });
    }

    private static void ComposeEstimateTermsV2(ColumnDescriptor mainColumn, string terms)
    {
        mainColumn.Item().BorderTop(1).BorderColor(BorderColor).Background(BackgroundLight).Padding(32).Row(row =>
        {
            row.AutoItem().AlignTop().PaddingRight(12).Text("☐").FontSize(14).FontColor(LightText);
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("TERMS & CONDITIONS").FontSize(9).Bold().FontColor(VeryLightText);
                col.Item().PaddingTop(6).Text(terms).FontSize(10).FontColor(MediumText);
            });
        });
    }

}
