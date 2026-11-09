namespace Akinai.Shared.Payments;

/// <summary>One Vietnamese bank that accepts inbound VietQR transfers.</summary>
/// <param name="Code">The NAPAS short code, which is what settings store.</param>
/// <param name="Bin">The six-digit acquirer ID a VietQR payload routes on.</param>
/// <param name="Name">The bank's short name, for a picker.</param>
public sealed record VietQrBank(string Code, string Bin, string Name);

/// <summary>
/// NAPAS acquirer IDs ("BINs") for the Vietnamese banks that accept inbound transfers.
///
/// A VietQR payload identifies the receiving bank by a six-digit BIN, not by the short code
/// a user picks in settings, which is why this table exists. Before it, three separate
/// places fetched the QR as an image from <c>img.vietqr.io</c> with the account number and
/// the holder's name in the query string — the invoice view, the dashboard card, and the
/// PDF the client is emailed. So the account details went to a third party on every send,
/// and because two of those fetches ran in a browser, that third party also learned the
/// viewer's IP address. A fourth call fetched this list at runtime, which meant the bank
/// picker was empty whenever that service was down.
///
/// <para>
/// A code this table does not know produces no QR code at all, rather than a guessed BIN.
/// Getting one of these digits wrong points a payment at the wrong institution, which is a
/// far worse failure than a missing QR with the bank details printed beside it.
/// </para>
/// </summary>
public static class VietQrBanks
{
    /// <summary>
    /// Transfer-capable banks, from api.vietqr.io/v2/banks on 2026-11-09. Regenerate from
    /// the same source if a bank is missing; these short codes are NAPAS's own.
    /// </summary>
    private static readonly VietQrBank[] Banks =
    [
        new("ABB", "970425", "ABBANK"),
        new("ACB", "970416", "ACB"),
        new("VBA", "970405", "Agribank"),
        new("BIDV", "970418", "BIDV"),
        new("BAB", "970409", "BacABank"),
        new("BVB", "970438", "BaoVietBank"),
        new("CAKE", "546034", "CAKE"),
        new("CBB", "970444", "CBBank"),
        new("CIMB", "422589", "CIMB"),
        new("COOPBANK", "970446", "COOPBANK"),
        new("EIB", "970431", "Eximbank"),
        new("HDB", "970437", "HDBank"),
        new("IVB", "970434", "IndovinaBank"),
        new("KBank", "668888", "KBank"),
        new("KLB", "970452", "KienLongBank"),
        new("KBHCM", "970463", "KookminHCM"),
        new("KBHN", "970462", "KookminHN"),
        new("LPB", "970449", "LPBank"),
        new("MB", "970422", "MBBank"),
        new("MBV", "970414", "MBV"),
        new("MSB", "970426", "MSB"),
        new("MVAS", "971032", "MVAS"),
        new("momo", "971025", "MoMo"),
        new("NCB", "970419", "NCB"),
        new("NAB", "970428", "NamABank"),
        new("OCB", "970448", "OCB"),
        new("PGB", "970430", "PGBank"),
        new("PVCB", "970412", "PVcomBank"),
        new("PVDB", "971133", "PVcomBank Pay"),
        new("SCB", "970429", "SCB"),
        new("SHB", "970443", "SHB"),
        new("STB", "970403", "Sacombank"),
        new("SGICB", "970400", "SaigonBank"),
        new("SEAB", "970440", "SeABank"),
        new("SHBVN", "970424", "ShinhanBank"),
        new("TPB", "970423", "TPBank"),
        new("TCB", "970407", "Techcombank"),
        new("TIMO", "963388", "Timo"),
        new("Ubank", "546035", "Ubank"),
        new("VIB", "970441", "VIB"),
        new("VPB", "970432", "VPBank"),
        new("VAB", "970427", "VietABank"),
        new("VIETBANK", "970433", "VietBank"),
        new("VCCB", "970454", "VietCapitalBank"),
        new("VCB", "970436", "Vietcombank"),
        new("ICB", "970415", "VietinBank"),
        new("WVN", "970457", "Woori"),
        new("ZALOPAY", "971101", "ZaloPay"),
    ];

    private static readonly Dictionary<string, VietQrBank> ByCode =
        Banks.ToDictionary(b => b.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>Every bank, by name, for a settings picker.</summary>
    public static IReadOnlyList<VietQrBank> All => Banks;

    /// <summary>
    /// The six-digit BIN for a stored bank code, or null when the code is unknown or blank.
    /// Null means "no QR code", never "guess".
    /// </summary>
    public static string? BinFor(string? bankCode)
        => string.IsNullOrWhiteSpace(bankCode) ? null
         : ByCode.TryGetValue(bankCode.Trim(), out var bank) ? bank.Bin
         : null;

    /// <summary>True when a QR code can be built for this bank code.</summary>
    public static bool IsKnown(string? bankCode) => BinFor(bankCode) is not null;

    /// <summary>Every code this table knows.</summary>
    public static IReadOnlyCollection<string> KnownCodes => ByCode.Keys;
}
