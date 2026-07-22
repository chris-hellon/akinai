using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Signatures;
using InvoiceSoftware.Shared.Dtos.Signatures;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Signatures;

public class GetSignatureHandler(IDbContextFactory<ApplicationDbContext> dbFactory, ICurrentUserService currentUserService)
    : IHandle<GetSignature, SignatureDto?>
{
    public async Task<HttpResult<SignatureDto?>> Handle(GetSignature request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<SignatureDto?>.Unauthorized();

        if (!Enum.TryParse<LinkedEntityType>(request.LinkedEntityType, out var entityType))
            return HttpResult<SignatureDto?>.Ok(null);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await OwnsLinkedEntityAsync(db, entityType, request.LinkedEntityId, userId, cancellationToken))
            return HttpResult<SignatureDto?>.NotFound();

        var signature = await db.DigitalSignatures
            .Where(s => s.LinkedEntityType == entityType && s.LinkedEntityId == request.LinkedEntityId)
            .OrderByDescending(s => s.SignedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (signature == null)
            return HttpResult<SignatureDto?>.Ok(null);

        var dto = new SignatureDto(
            signature.Id,
            signature.LinkedEntityType.ToString(),
            signature.LinkedEntityId,
            signature.SignerName,
            signature.SignerEmail,
            signature.SignedAt,
            signature.IsValid,
            signature.InvalidationReason,
            signature.GetSignatureAsBase64());

        return HttpResult<SignatureDto?>.Ok(dto);
    }

    // A signature points at an entity by id + type, with no owner of its own.
    // Ownership has to be resolved per type, or any signed-in user could read
    // or forge a signature against someone else's invoice by guessing an id.
    private static async Task<bool> OwnsLinkedEntityAsync(
        ApplicationDbContext db, LinkedEntityType type, Guid id, string userId, CancellationToken ct)
        => type switch
        {
            LinkedEntityType.Invoice  => await db.Invoices.AnyAsync(i => i.Id == id && i.Client.UserId == userId, ct),
            LinkedEntityType.Estimate => await db.Estimates.AnyAsync(e => e.Id == id && e.UserId == userId, ct),
            LinkedEntityType.Client   => await db.Clients.AnyAsync(c => c.Id == id && c.UserId == userId, ct),
            LinkedEntityType.Project  => await db.Projects.AnyAsync(p => p.Id == id && p.Client.UserId == userId, ct),
            // Expense has no owner column yet — deny until it does.
            _ => false
        };
}
