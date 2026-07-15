using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Entities;
using InvoiceSoftware.Infrastructure.Banking;
using InvoiceSoftware.Infrastructure.Data;
using InvoiceSoftware.Shared.Api.Banking;
using InvoiceSoftware.Shared.Dtos.Banking;
using InvoiceSoftware.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace InvoiceSoftware.Infrastructure.Handlers.Banking;

internal static class BankingMap
{
    public static BankConnectionDto ToDto(BankConnection c) => new(
        c.Id, c.Provider, c.InstitutionName, c.Status.ToString(), c.ConsentExpiresAt, c.LastSyncedAt, c.LastError);
}

public class GetBankInstitutionsHandler(IBankProviderRegistry registry, ICurrentUserService currentUserService)
    : IHandle<GetBankInstitutions, List<BankInstitutionDto>>
{
    public async Task<HttpResult<List<BankInstitutionDto>>> Handle(GetBankInstitutions request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUserService.UserId))
            return HttpResult<List<BankInstitutionDto>>.Unauthorized();

        var provider = registry.ForCountry(request.Country);
        if (provider is null)
            return HttpResult<List<BankInstitutionDto>>.Ok(new());

        try
        {
            var list = await provider.GetInstitutionsAsync(request.Country, cancellationToken);
            return HttpResult<List<BankInstitutionDto>>.Ok(
                list.Select(i => new BankInstitutionDto(i.Id, i.Name, i.LogoUrl)).ToList());
        }
        catch
        {
            return HttpResult<List<BankInstitutionDto>>.Ok(new());
        }
    }
}

public class StartBankConnectionHandler(
    IBankProviderRegistry registry,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<StartBankConnection, BankLinkDto>
{
    public async Task<HttpResult<BankLinkDto>> Handle(StartBankConnection request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<BankLinkDto>.Unauthorized();

        var provider = registry.ForCountry(request.Country);
        var redirect = provider?.RedirectUri;
        if (provider is null || string.IsNullOrWhiteSpace(redirect))
            return HttpResult<BankLinkDto>.BadRequest(new BankLinkDto("", ""));

        var reference = Guid.NewGuid().ToString("N");
        try
        {
            var session = await provider.CreateLinkAsync(request.InstitutionId, request.Country, reference, redirect, cancellationToken);
            if (string.IsNullOrEmpty(session.AuthUrl))
                return HttpResult<BankLinkDto>.BadRequest(new BankLinkDto("", ""));

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            db.BankConnections.Add(BankConnection.Create(
                userId, provider.Name, request.InstitutionId, request.InstitutionName, reference, session.RequisitionId));
            await db.SaveChangesAsync(cancellationToken);

            return HttpResult<BankLinkDto>.Ok(new BankLinkDto(session.AuthUrl, reference));
        }
        catch
        {
            return HttpResult<BankLinkDto>.BadRequest(new BankLinkDto("", ""));
        }
    }
}

public class FinalizeBankConnectionHandler(
    IBankProviderRegistry registry,
    BankSyncService syncService,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<FinalizeBankConnection, BankConnectionDto?>
{
    public async Task<HttpResult<BankConnectionDto?>> Handle(FinalizeBankConnection request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<BankConnectionDto?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var conn = await db.BankConnections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Reference == request.Reference, cancellationToken);
        if (conn is null)
            return HttpResult<BankConnectionDto?>.NotFound();

        var provider = registry.ByName(conn.Provider);
        if (provider is null)
        {
            conn.MarkError($"Provider '{conn.Provider}' is not available.");
            await db.SaveChangesAsync(cancellationToken);
            return HttpResult<BankConnectionDto?>.Ok(BankingMap.ToDto(conn));
        }

        try
        {
            var callback = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(request.Code)) callback["code"] = request.Code;

            var link = await provider.CompleteLinkAsync(conn, callback, cancellationToken);
            if (link.Linked && link.AccountId is not null)
            {
                conn.MarkLinked(link.AccountId, link.ConsentExpiresAt ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90));
                conn.SetTokens(link.AccessToken, link.RefreshToken, link.TokenExpiresAt);
                await db.SaveChangesAsync(cancellationToken);
                try { await syncService.SyncAsync(conn, db, cancellationToken); } catch { /* status already marked */ }
            }
        }
        catch
        {
            conn.MarkError("Could not resolve the bank link.");
            await db.SaveChangesAsync(cancellationToken);
        }

        return HttpResult<BankConnectionDto?>.Ok(BankingMap.ToDto(conn));
    }
}

public class GetBankConnectionsHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<GetBankConnections, List<BankConnectionDto>>
{
    public async Task<HttpResult<List<BankConnectionDto>>> Handle(GetBankConnections request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<List<BankConnectionDto>>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.BankConnections
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return HttpResult<List<BankConnectionDto>>.Ok(items.Select(BankingMap.ToDto).ToList());
    }
}

public class SyncBankConnectionHandler(
    BankSyncService syncService,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<SyncBankConnection, BankSyncResultDto>
{
    public async Task<HttpResult<BankSyncResultDto>> Handle(SyncBankConnection request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<BankSyncResultDto>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var conn = await db.BankConnections
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == userId, cancellationToken);
        if (conn is null)
            return HttpResult<BankSyncResultDto>.NotFound();

        try
        {
            var imported = await syncService.SyncAsync(conn, db, cancellationToken);
            return HttpResult<BankSyncResultDto>.Ok(new BankSyncResultDto(imported, conn.Status.ToString()));
        }
        catch
        {
            return HttpResult<BankSyncResultDto>.Ok(new BankSyncResultDto(0, conn.Status.ToString()));
        }
    }
}

public class DeleteBankConnectionHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService)
    : IHandle<DeleteBankConnection>
{
    public async Task<HttpResult> Handle(DeleteBankConnection request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.BankConnections
            .Where(c => c.Id == request.Id && c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return HttpResult.Ok();
    }
}
