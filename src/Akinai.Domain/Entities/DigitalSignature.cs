using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Entities;

public class DigitalSignature : AggregateRoot, IAuditableEntity
{
    public LinkedEntityType LinkedEntityType { get; private set; }
    public Guid LinkedEntityId { get; private set; }
    public byte[] SignatureData { get; private set; } = null!;
    public string SignerName { get; private set; } = null!;
    public string? SignerEmail { get; private set; }
    public string? SignerIpAddress { get; private set; }
    public DateTime SignedAt { get; private set; }
    public bool IsValid { get; private set; } = true;
    public string? InvalidationReason { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private DigitalSignature() { }

    public static DigitalSignature Create(
        LinkedEntityType linkedEntityType,
        Guid linkedEntityId,
        byte[] signatureData,
        string signerName,
        string? signerEmail = null,
        string? signerIpAddress = null)
    {
        if (signatureData.Length == 0)
            throw new DomainException("Signature data cannot be empty");
        if (string.IsNullOrWhiteSpace(signerName))
            throw new DomainException("Signer name is required");

        return new DigitalSignature
        {
            LinkedEntityType = linkedEntityType,
            LinkedEntityId = linkedEntityId,
            SignatureData = signatureData,
            SignerName = signerName,
            SignerEmail = signerEmail,
            SignerIpAddress = signerIpAddress,
            SignedAt = DateTime.UtcNow
        };
    }

    public void Invalidate(string reason)
    {
        if (!IsValid)
            throw new DomainException("Signature is already invalidated");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Invalidation reason is required");

        IsValid = false;
        InvalidationReason = reason;
    }

    public string GetSignatureAsBase64() => Convert.ToBase64String(SignatureData);
}
