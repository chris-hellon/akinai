using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Domain.Entities;
using Akinai.Domain.Enums;
using Akinai.Infrastructure.Data;
using Akinai.Shared.Api.Signatures;
using Microsoft.AspNetCore.Http;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Signatures;

public class SaveSignatureHandler(IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService, IHttpContextAccessor httpContextAccessor)
    : IHandle<SaveSignature, Guid>
{
    public async Task<HttpResult<Guid>> Handle(SaveSignature request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LinkedEntityType>(request.LinkedEntityType, out var entityType))
            return HttpResult<Guid>.NotFound();

        // Get signer's IP address
        var ipAddress = httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<Guid>.Unauthorized();

        if (!await OwnsLinkedEntityAsync(db, entityType, request.LinkedEntityId, userId, cancellationToken))
            return HttpResult<Guid>.NotFound();

        try
        {
            var signature = DigitalSignature.Create(
                entityType,
                request.LinkedEntityId,
                request.SignatureData,
                request.SignerName,
                request.SignerEmail,
                ipAddress);

            db.DigitalSignatures.Add(signature);
            await db.SaveChangesAsync(cancellationToken);
            return HttpResult<Guid>.Created(signature.Id);
        }
        catch (Domain.Exceptions.DomainException)
        {
            return HttpResult<Guid>.NotFound();
        }
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
