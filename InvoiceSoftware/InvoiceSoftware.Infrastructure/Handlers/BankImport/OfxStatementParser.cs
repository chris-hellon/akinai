using System.Globalization;
using System.Text.RegularExpressions;

namespace InvoiceSoftware.Infrastructure.Handlers.BankImport;

/// <summary>
/// Parser for OFX / QFX bank statements (the Quicken/Money export standard). Handles both the
/// closed-tag XML variant and the older SGML variant where leaf tags aren't closed. Each
/// transaction carries a stable FITID for reliable de-duplication, plus a signed amount
/// (negative = money out) and the statement's default currency.
/// </summary>
public static class OfxStatementParser
{
    public record OfxTransaction(string? Fitid, DateOnly Date, decimal Amount, string Currency, string Description);

    public static bool LooksLikeOfx(string content)
        => content.Contains("<OFX>", StringComparison.OrdinalIgnoreCase)
           || content.Contains("OFXHEADER", StringComparison.OrdinalIgnoreCase)
           || content.Contains("<STMTTRN>", StringComparison.OrdinalIgnoreCase);

    public static (List<OfxTransaction> Transactions, List<string> Errors) Parse(string content, string fallbackCurrency)
    {
        var errors = new List<string>();
        var transactions = new List<OfxTransaction>();

        var currency = Leaf(content, "CURDEF");
        currency = string.IsNullOrWhiteSpace(currency) ? fallbackCurrency : currency.Trim().ToUpperInvariant();
        if (currency.Length > 3) currency = currency[..3];

        var index = 0;
        foreach (var block in Blocks(content))
        {
            index++;
            var dtRaw = Leaf(block, "DTPOSTED");
            var amtRaw = Leaf(block, "TRNAMT");

            if (!TryParseDate(dtRaw, out var date))
            {
                errors.Add($"Transaction {index}: couldn't read a date.");
                continue;
            }
            if (!decimal.TryParse(amtRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            {
                errors.Add($"Transaction {index}: couldn't read the amount.");
                continue;
            }

            var name = Leaf(block, "NAME");
            var memo = Leaf(block, "MEMO");
            var description = FirstNonEmpty(name, memo) ?? "(no description)";
            var fitid = Leaf(block, "FITID");

            transactions.Add(new OfxTransaction(
                string.IsNullOrWhiteSpace(fitid) ? null : fitid.Trim(),
                date,
                Math.Round(amount, 2),
                currency,
                description));
        }

        return (transactions, errors);
    }

    // Each <STMTTRN>…</STMTTRN> aggregate. Aggregates are closed even in SGML OFX; if a file
    // omits the closing tag we still split cleanly on the opening tag.
    private static IEnumerable<string> Blocks(string content)
    {
        var parts = Regex.Split(content, "<STMTTRN>", RegexOptions.IgnoreCase);
        for (var i = 1; i < parts.Length; i++)
        {
            var chunk = parts[i];
            var close = chunk.IndexOf("</STMTTRN>", StringComparison.OrdinalIgnoreCase);
            yield return close >= 0 ? chunk[..close] : chunk;
        }
    }

    // Value after <TAG>, up to the next tag or line break — works for closed and unclosed leafs.
    private static string? Leaf(string block, string tag)
    {
        var m = Regex.Match(block, $"<{tag}>\\s*([^<\r\n]*)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static bool TryParseDate(string? raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        // OFX dates: YYYYMMDD[HHMMSS[.XXX]][gmt]. Take the leading 8 digits.
        var digits = new string(raw.Where(char.IsDigit).Take(8).ToArray());
        if (digits.Length != 8) return false;
        return DateOnly.TryParseExact(digits, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
