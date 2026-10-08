using System.Security.Cryptography;

namespace Akinai.Domain.Services;

/// <summary>
/// A fingerprint of the exact document a client was sent.
///
/// Invoices get disputed, and across jurisdictions the dispute is often not about the
/// amount but about what was sent and when: the client says the invoice said something
/// else, or that it never arrived. Storing a hash of the bytes that actually went out
/// turns that into a question of fact. It proves the document was not altered afterwards
/// — including by the sender, which is the half that makes it worth anything to the
/// client as well.
///
/// SHA-256 because it is the thing an accountant or a court will recognise, and because
/// the hash is stored next to the record it describes rather than used as a secret.
/// </summary>
public static class DocumentFingerprint
{
    /// <summary>Lower-case hex SHA-256 of the document bytes, or null for an empty document.</summary>
    public static string? Of(byte[]? document)
    {
        if (document is null || document.Length == 0) return null;
        return Convert.ToHexStringLower(SHA256.HashData(document));
    }

    /// <summary>
    /// The first and last few characters, for showing a human a fingerprint they can
    /// compare at a glance without reading 64 hex characters.
    /// </summary>
    public static string Abbreviate(string? fingerprint)
        => string.IsNullOrWhiteSpace(fingerprint) || fingerprint.Length < 16
            ? fingerprint ?? ""
            : $"{fingerprint[..8]}…{fingerprint[^8..]}";
}
