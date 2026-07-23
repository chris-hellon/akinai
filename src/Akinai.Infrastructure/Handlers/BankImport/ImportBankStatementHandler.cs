using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.BankImport;
using Akinai.Shared.Dtos.BankImport;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Akinai.Infrastructure.Handlers.BankImport;

public class ImportBankStatementHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService, IEntitlementService entitlementService)
    : IHandle<ImportBankStatement, ImportResultDto>
{
    public async Task<HttpResult<ImportResultDto>> Handle(
        ImportBankStatement request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<ImportResultDto>.Unauthorized();

        // Bank import is a paid feature. Already-imported transactions stay readable.
        var entitlements = await entitlementService.GetAsync(userId, cancellationToken);
        if (!entitlements.BankImport) return HttpResult<ImportResultDto>.Forbidden();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bytes = request.FileContent;

        // Excel (.xls/.xlsx) is binary — detect from the file signature before decoding as text.
        if (IsExcel(bytes))
        {
            var (excelLines, excelCurrency) = ExcelToLines(bytes);
            return await ImportDelimitedAsync(db, userId, excelLines, request.FileName, excelCurrency, cancellationToken);
        }

        var fileContent = Encoding.UTF8.GetString(bytes);

        // OFX / QFX statements are structured (stable FITID + signed amount + currency) — parse
        // those directly rather than as delimited text.
        if (OfxStatementParser.LooksLikeOfx(fileContent))
            return await HandleOfxAsync(db, userId, fileContent, request.FileName, cancellationToken);

        var lines = fileContent
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Trim().Length > 0)
            .ToList();

        return await ImportDelimitedAsync(db, userId, lines, request.FileName, null, cancellationToken);
    }

    // Shared column-mapped import for delimited text (CSV/TSV) and Excel (converted to rows).
    private async Task<HttpResult<ImportResultDto>> ImportDelimitedAsync(
        ApplicationDbContext db, string userId, List<string> lines, string fileName, string? detectedCurrency, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var importedCount = 0;
        var skippedDuplicates = 0;
        const int autoMatched = 0;

        if (lines.Count < 2)
            return HttpResult<ImportResultDto>.Ok(new ImportResultDto(0, 0, 0, 0,
                new List<string> { "File is empty or has no data rows." }));

        // Detect delimiter from the header row (comma, tab, semicolon or pipe).
        var delimiter = DetectDelimiter(lines[0]);
        var headers = ParseLine(lines[0], delimiter);

        // Map columns by header name so any bank's column order/layout works.
        var idxDate = FindColumn(headers, "completed date", "date completed", "transaction date",
            "posted date", "posting date", "value date", "booking date", "date", "started date", "date started");
        var idxDesc = FindColumn(headers, "description", "details", "narrative", "name", "payee",
            "merchant", "memo", "particulars", "counterparty");
        var idxAmount = FindColumn(headers, "amount", "transaction amount", "value", "net amount", "money");
        var idxIn = FindColumn(headers, "paid in", "money in", "amount in", "credit amount", "credit", "deposit");
        var idxOut = FindColumn(headers, "paid out", "money out", "amount out", "debit amount", "debit", "withdrawal");
        var idxCurrency = FindColumn(headers, "currency", "ccy", "curr");
        var idxReference = FindColumn(headers, "reference", "payment reference", "ref");
        var idxType = FindColumn(headers, "type", "category");

        // A "Credit Amount" / "Debit Amount" pair (money-in/out columns) must not also be
        // treated as a single signed amount column — otherwise debit rows have an empty
        // "amount" and fail. Prefer the in/out pair.
        if (idxAmount >= 0 && (idxAmount == idxIn || idxAmount == idxOut)) idxAmount = -1;

        if (idxDate < 0)
            errors.Add("Could not find a date column in the header row.");
        if (idxAmount < 0 && idxIn < 0 && idxOut < 0)
            errors.Add("Could not find an amount column (or paid in / paid out columns) in the header row.");
        if (idxDate < 0 || (idxAmount < 0 && idxIn < 0 && idxOut < 0))
        {
            errors.Add($"Detected columns: {string.Join(", ", headers)}");
            return HttpResult<ImportResultDto>.Ok(new ImportResultDto(0, 0, 0, 0, errors));
        }

        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.UserId == userId, cancellationToken);
        // A currency detected from the statement itself wins over the business default.
        var fallbackCurrency = detectedCurrency
            ?? businessProfile?.DefaultCurrency ?? "GBP";

        // Content-based dedup — hashed the SAME way for existing rows and new rows
        // (in C#, not SQL) so re-importing the same statement skips everything.
        var existingRows = await db.BankTransactions
            .Where(bt => bt.UserId == userId)
            .Select(bt => new { bt.Description, bt.TransactionDate, bt.Amount })
            .ToListAsync(cancellationToken);
        var existingHashes = existingRows
            .Select(r => Hash(r.Description, r.TransactionDate, r.Amount))
            .ToHashSet();

        // Every column that looks like a date, most-authoritative first, so a pending
        // row with an empty "Completed Date" falls back to "Started Date".
        var dateIndices = headers
            .Select((h, idx) => (name: h.Trim().ToLowerInvariant(), idx))
            .Where(x => x.name.Contains("date"))
            .OrderBy(x => x.name.Contains("completed") ? 0
                : (x.name.Contains("transaction") || x.name.Contains("posted") || x.name.Contains("posting") || x.name.Contains("value") || x.name.Contains("booking")) ? 1
                : 2)
            .Select(x => x.idx)
            .ToList();
        if (dateIndices.Count == 0 && idxDate >= 0) dateIndices.Add(idxDate);

        var dataRows = 0;
        for (var i = 1; i < lines.Count; i++)
        {
            var columns = ParseLine(lines[i], delimiter);
            if (columns.All(string.IsNullOrWhiteSpace)) continue;

            var date = default(DateOnly);
            var hasDate = false;
            foreach (var di in dateIndices)
            {
                if (TryParseDate(Get(columns, di), out date)) { hasDate = true; break; }
            }
            var hasAmount = TryResolveAmount(columns, idxAmount, idxIn, idxOut, out var amount);

            // A row that yields neither a date nor an amount is almost certainly a
            // summary/blank line, so skip it silently rather than reporting an error.
            if (!hasDate && !hasAmount) continue;

            dataRows++;

            if (!hasDate)
            {
                errors.Add($"Row {i + 1}: couldn't read a date.");
                continue;
            }
            if (!hasAmount)
            {
                errors.Add($"Row {i + 1}: couldn't read the amount.");
                continue;
            }

            var description = FirstNonEmpty(Get(columns, idxDesc), Get(columns, idxReference), Get(columns, idxType));
            if (string.IsNullOrWhiteSpace(description)) description = "(no description)";

            var currency = FirstNonEmpty(Get(columns, idxCurrency), fallbackCurrency);
            var reference = idxReference >= 0 ? NullIfEmpty(Get(columns, idxReference)) : null;

            var hash = Hash(description, date, amount);
            if (existingHashes.Contains(hash))
            {
                skippedDuplicates++;
                continue;
            }

            try
            {
                var transaction = BankTransaction.Create(
                    userId, date, description.Trim(), amount, currency, fileName, i + 1, reference);
                db.BankTransactions.Add(transaction);
                existingHashes.Add(hash);
                importedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row {i + 1}: {ex.Message}");
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return HttpResult<ImportResultDto>.Ok(new ImportResultDto(
            dataRows, importedCount, skippedDuplicates, autoMatched, errors));
    }

    // xlsx/xlsm = ZIP (PK..), xls = OLE2 compound file. CSV/OFX won't match either.
    private static bool IsExcel(byte[] b)
    {
        if (b.Length < 8) return false;
        var zip = b[0] == 0x50 && b[1] == 0x4B && b[2] == 0x03 && b[3] == 0x04;
        var ole2 = b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0
                   && b[4] == 0xA1 && b[5] == 0xB1 && b[6] == 0x1A && b[7] == 0xE1;
        return zip || ole2;
    }

    // Read the first worksheet into tab-joined lines so the delimited importer handles it,
    // skipping any title/metadata rows above the real header that bank exports often add.
    private static (List<string> Lines, string? Currency) ExcelToLines(byte[] bytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // legacy .xls needs code pages
        using var stream = new MemoryStream(bytes);
        using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);

        var rows = new List<string[]>();
        while (reader.Read())
        {
            var row = new string[reader.FieldCount];
            var any = false;
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var v = reader.GetValue(i);
                var cell = v switch
                {
                    null => "",
                    DateTime dt => dt.ToString("yyyy-MM-dd"),
                    double d => d.ToString(CultureInfo.InvariantCulture),
                    _ => v.ToString() ?? ""
                };
                cell = cell.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
                if (cell.Length > 0) any = true;
                row[i] = cell;
            }
            if (any) rows.Add(row);
        }

        if (rows.Count == 0) return (new List<string>(), null);
        var headerIdx = FindHeaderRow(rows);
        // The statement currency usually sits in a metadata row above the table.
        var currency = DetectCurrency(rows.Take(headerIdx));
        var lines = rows.Skip(headerIdx).Select(r => string.Join('\t', r)).ToList();
        return (lines, currency);
    }

    // Every ISO 4217 currency code the runtime knows — so any uploaded currency is recognised,
    // while non-currency 3-letter tokens (CIF, REF, …) are ignored.
    private static readonly HashSet<string> KnownCurrencies = BuildIsoCurrencies();

    private static HashSet<string> BuildIsoCurrencies()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try { set.Add(new RegionInfo(culture.Name).ISOCurrencySymbol); }
            catch { /* culture without a region */ }
        }
        return set;
    }

    private static string? DetectCurrency(IEnumerable<string[]> metadataRows)
    {
        foreach (var row in metadataRows)
            foreach (var cell in row)
            {
                var c = cell.Trim();
                if (c.Length == 3 && KnownCurrencies.Contains(c)) return c.ToUpperInvariant();
            }
        return null;
    }

    private static int FindHeaderRow(List<string[]> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var cells = rows[i].Select(c => c.ToLowerInvariant()).ToList();
            var hasDate = cells.Any(c => c.Contains("date"));
            var hasMoney = cells.Any(c => c.Contains("amount") || c.Contains("description")
                || c.Contains("paid") || c.Contains("debit") || c.Contains("credit"));
            if (hasDate && hasMoney) return i;
        }
        return 0;
    }

    private static char DetectDelimiter(string headerLine)
    {
        var candidates = new[] { ',', '\t', ';', '|' };
        return candidates
            .OrderByDescending(c => headerLine.Count(ch => ch == c))
            .First();
    }

    /// <summary>Finds the column whose header matches an alias — exact match first (by alias priority), then a "contains" match.</summary>
    private static int FindColumn(IReadOnlyList<string> headers, params string[] aliases)
    {
        var norm = headers.Select(h => h.Trim().ToLowerInvariant()).ToArray();
        foreach (var alias in aliases)
            for (var i = 0; i < norm.Length; i++)
                if (norm[i] == alias) return i;
        foreach (var alias in aliases)
            for (var i = 0; i < norm.Length; i++)
                if (norm[i].Contains(alias)) return i;
        return -1;
    }

    private static string Get(IReadOnlyList<string> columns, int index)
        => index >= 0 && index < columns.Count ? columns[index].Trim() : "";

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<HttpResult<ImportResultDto>> HandleOfxAsync(
        ApplicationDbContext db, string userId, string fileContent, string fileName, CancellationToken cancellationToken)
    {
        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.UserId == userId, cancellationToken);
        var fallbackCurrency = businessProfile?.DefaultCurrency ?? "USD";

        var (transactions, errors) = OfxStatementParser.Parse(fileContent, fallbackCurrency);

        // Dedup by the provider's stable id (FITID) where present, else by content hash.
        var existingRows = await db.BankTransactions
            .Where(bt => bt.UserId == userId)
            .Select(bt => new { bt.ExternalId, bt.Description, bt.TransactionDate, bt.Amount })
            .ToListAsync(cancellationToken);
        var existingIds = existingRows
            .Where(r => r.ExternalId != null)
            .Select(r => r.ExternalId!)
            .ToHashSet(StringComparer.Ordinal);
        var existingHashes = existingRows
            .Select(r => Hash(r.Description, r.TransactionDate, r.Amount))
            .ToHashSet();

        var imported = 0;
        var skipped = 0;
        foreach (var t in transactions)
        {
            if (t.Fitid is not null)
            {
                if (!existingIds.Add(t.Fitid)) { skipped++; continue; }
                db.BankTransactions.Add(BankTransaction.CreateFromFeed(
                    userId, t.Fitid, t.Date, t.Description, t.Amount, t.Currency, null, fileName));
            }
            else
            {
                var hash = Hash(t.Description, t.Date, t.Amount);
                if (!existingHashes.Add(hash)) { skipped++; continue; }
                db.BankTransactions.Add(BankTransaction.Create(
                    userId, t.Date, t.Description.Trim(), t.Amount, t.Currency, fileName, 0));
            }
            imported++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return HttpResult<ImportResultDto>.Ok(new ImportResultDto(
            transactions.Count, imported, skipped, 0, errors));
    }

    private static string Hash(string description, DateOnly date, decimal amount)
        => $"{description.Trim().ToLowerInvariant()}|{date:yyyy-MM-dd}|{amount:0.00}";

    /// <summary>Resolves the signed amount from either a single amount column or a paid-in / paid-out pair.</summary>
    private static bool TryResolveAmount(IReadOnlyList<string> columns, int idxAmount, int idxIn, int idxOut, out decimal amount)
    {
        amount = 0m;
        if (idxAmount >= 0 && TryParseAmount(Get(columns, idxAmount), out amount))
            return true;

        decimal credit = 0m, debit = 0m;
        var hasIn = idxIn >= 0 && TryParseAmount(Get(columns, idxIn), out credit);
        var hasOut = idxOut >= 0 && TryParseAmount(Get(columns, idxOut), out debit);
        if (hasIn || hasOut)
        {
            amount = Math.Abs(credit) - Math.Abs(debit);
            return true;
        }
        return false;
    }

    private static bool TryParseAmount(string raw, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = raw.Trim();
        var negative = false;
        if (s.StartsWith("(") && s.EndsWith(")")) { negative = true; s = s[1..^1]; }

        // Keep digits, separators and sign; drop currency symbols, letters and spaces.
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            if (char.IsDigit(c) || c == '.' || c == ',' || c == '-') sb.Append(c);
        }
        s = sb.ToString();
        if (s.StartsWith('-')) { negative = true; }
        s = s.Replace("-", "");
        if (s.Length == 0) return false;

        // Normalise decimal/thousands separators.
        var lastDot = s.LastIndexOf('.');
        var lastComma = s.LastIndexOf(',');
        if (lastDot >= 0 && lastComma >= 0)
        {
            // Both present: the right-most separator is the decimal point; the other groups thousands.
            var decimalSep = lastDot > lastComma ? '.' : ',';
            var thousandsSep = decimalSep == '.' ? ',' : '.';
            s = s.Replace(thousandsSep.ToString(), "").Replace(decimalSep, '.');
        }
        else
        {
            // A single separator type. Decide thousands vs decimal by how it's used:
            //   - repeated (e.g. "1,234,567"), or a single one grouping exactly 3 trailing
            //     digits (VND "3,190" / "393,000", or "1.000") -> thousands separator.
            //   - otherwise a decimal point ("3.30", "12,50").
            var sep = lastDot >= 0 ? '.' : (lastComma >= 0 ? ',' : '\0');
            if (sep != '\0')
            {
                var count = s.Count(ch => ch == sep);
                var trailing = s.Length - s.LastIndexOf(sep) - 1;
                if (count > 1 || trailing == 3)
                    s = s.Replace(sep.ToString(), "");
                else
                    s = s.Replace(sep, '.');
            }
        }

        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            return false;

        if (negative) amount = -amount;
        return true;
    }

    private static bool TryParseDate(string raw, out DateOnly date)
    {
        date = default;
        raw = raw.Trim();
        if (raw.Length == 0) return false;

        string[] formats =
        {
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd",
            "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy", "d/M/yyyy",
            "MM/dd/yyyy HH:mm:ss", "MM/dd/yyyy",
            "dd-MM-yyyy", "dd.MM.yyyy", "yyyy/MM/dd",
            "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy"
        };
        foreach (var f in formats)
            if (DateTime.TryParseExact(raw, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                date = DateOnly.FromDateTime(dt);
                return true;
            }

        // Fall back to culture-aware parsing (en-GB first so d/M/y wins over M/d/y).
        if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.None, out var g))
        {
            date = DateOnly.FromDateTime(g);
            return true;
        }
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var inv))
        {
            date = DateOnly.FromDateTime(inv);
            return true;
        }
        return false;
    }

    private static List<string> ParseLine(string line, char delimiter)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = new StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
