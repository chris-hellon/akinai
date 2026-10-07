using Akinai.Shared.Geography;

namespace Akinai.Shared.Compliance;

/// <summary>
/// What an invoice must say, given who is billing whom and from where.
///
/// The corridor — seller country, buyer country, business or consumer — decides the tax
/// treatment and, with it, wording the invoice is legally required to carry. An EU B2B
/// cross-border invoice without a reverse-charge statement and both VAT numbers is not a
/// valid invoice: the client's accountant rejects it, and the client cannot reclaim.
///
/// <para>
/// <b>This advises; it does not compute tax.</b> It will tell you the treatment it
/// believes applies and the wording that treatment requires, and it will point out when
/// the tax rate on the invoice looks inconsistent with that treatment — but it never
/// alters a rate. Tax rules carry real consequences and have exceptions this table does
/// not model (place-of-supply specials, OSS registration, digital-services rules,
/// domestic reverse charge in construction). A tool that silently zero-rated an invoice
/// on a guess would be worse than one that said nothing.
/// </para>
/// </summary>
public static class CorridorRules
{
    public enum Treatment
    {
        /// <summary>Not enough information — at minimum a country is missing.</summary>
        Unknown,
        /// <summary>Seller and buyer in the same country.</summary>
        Domestic,
        /// <summary>Both in the EU VAT area, different countries, buyer is a VAT-registered business.</summary>
        EuReverseCharge,
        /// <summary>Both in the EU VAT area, different countries, buyer is a consumer.</summary>
        EuConsumer,
        /// <summary>Seller in the EU VAT area, buyer outside it.</summary>
        ExportFromEu,
        /// <summary>Seller outside the EU VAT area, buyer in a different country.</summary>
        CrossBorderNonEu,
    }

    public enum Severity { Blocking, Warning, Info }

    public sealed record Finding(Severity Severity, string Message);

    /// <param name="RequiredStatements">
    /// Wording the invoice must carry. Rendered on the document, not shown as advice.
    /// </param>
    public sealed record Assessment(
        Treatment Treatment,
        IReadOnlyList<string> RequiredStatements,
        IReadOnlyList<Finding> Findings)
    {
        public bool HasBlockingFindings => Findings.Any(f => f.Severity == Severity.Blocking);
    }

    /// <summary>
    /// The statement EU law requires on a cross-border B2B invoice where VAT is accounted
    /// for by the recipient. The wording is prescribed, which is why it is a constant.
    /// </summary>
    public const string ReverseChargeStatement =
        "Reverse charge — VAT to be accounted for by the recipient.";

    public static Assessment Assess(
        string? sellerCountryCode,
        string? buyerCountryCode,
        bool buyerIsBusiness,
        string? sellerTaxNumber,
        string? buyerTaxNumber,
        decimal taxRate)
    {
        var statements = new List<string>();
        var findings = new List<Finding>();

        if (sellerCountryCode is null || buyerCountryCode is null)
        {
            if (sellerCountryCode is null)
                findings.Add(new Finding(Severity.Warning,
                    "Your own country is not set on your business profile, so the tax treatment of this invoice cannot be determined."));
            if (buyerCountryCode is null)
                findings.Add(new Finding(Severity.Warning,
                    "This client has no country set, so the tax treatment of this invoice cannot be determined."));

            return new Assessment(Treatment.Unknown, statements, findings);
        }

        var sellerInEu = Countries.IsEuVatArea(sellerCountryCode);
        var buyerInEu = Countries.IsEuVatArea(buyerCountryCode);
        var sameCountry = string.Equals(sellerCountryCode, buyerCountryCode, StringComparison.OrdinalIgnoreCase);

        if (sameCountry)
            return new Assessment(Treatment.Domestic, statements, findings);

        if (sellerInEu && buyerInEu)
        {
            if (buyerIsBusiness)
            {
                statements.Add(ReverseChargeStatement);

                // Both numbers are mandatory on the document, not merely desirable: the
                // buyer's accountant needs them to account for the VAT, and the seller
                // needs them to justify not charging it.
                if (string.IsNullOrWhiteSpace(sellerTaxNumber))
                    findings.Add(new Finding(Severity.Blocking,
                        "An EU reverse-charge invoice must show your VAT number. Add it to your business profile."));

                if (string.IsNullOrWhiteSpace(buyerTaxNumber))
                    findings.Add(new Finding(Severity.Blocking,
                        $"An EU reverse-charge invoice must show the client's VAT number. Add it to the client record, or treat them as a consumer if they are not VAT registered."));

                if (taxRate != 0)
                    findings.Add(new Finding(Severity.Warning,
                        $"Reverse charge normally means no VAT is charged, but this invoice has a tax rate of {taxRate:0.##}%. Check this is what you intend."));

                return new Assessment(Treatment.EuReverseCharge, statements, findings);
            }

            findings.Add(new Finding(Severity.Info,
                $"A consumer in {Countries.NameOf(buyerCountryCode)}. VAT normally applies — which country's depends on what you are supplying and whether you are registered for OSS. Worth confirming with your accountant."));

            return new Assessment(Treatment.EuConsumer, statements, findings);
        }

        if (sellerInEu && !buyerInEu)
        {
            findings.Add(new Finding(Severity.Info,
                $"A customer outside the EU VAT area ({Countries.NameOf(buyerCountryCode)}). Supplies of services here are usually outside the scope of EU VAT, but the place-of-supply rules vary by service."));

            if (taxRate != 0)
                findings.Add(new Finding(Severity.Warning,
                    $"This invoice charges {taxRate:0.##}% tax on a supply outside the EU VAT area. Check this is what you intend."));

            return new Assessment(Treatment.ExportFromEu, statements, findings);
        }

        // Seller outside the EU. The UK is the common case and the one worth naming,
        // because post-Brexit a UK seller invoicing the EU is making an export.
        findings.Add(new Finding(Severity.Info,
            $"A cross-border supply from {Countries.NameOf(sellerCountryCode)} to {Countries.NameOf(buyerCountryCode)}. Whether you charge tax depends on your own country's place-of-supply rules."));

        return new Assessment(Treatment.CrossBorderNonEu, statements, findings);
    }
}
