using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using Akinai.Infrastructure.Data;
using Akinai.Infrastructure.Services;
using Akinai.Shared.Api.Estimates;
using Akinai.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Infrastructure.Handlers.Estimates;

public class GetEstimatePdfHandler(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ICurrentUserService currentUserService,
    PdfGeneratorService pdfGenerator)
    : IHandle<GetEstimatePdf, byte[]?>
{
    public async Task<HttpResult<byte[]?>> Handle(GetEstimatePdf request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return HttpResult<byte[]?>.Unauthorized();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var estimate = await db.Estimates
            .Include(e => e.Client)
            .Include(e => e.LineItems.OrderBy(li => li.Order))
            .Include(e => e.Template)
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.UserId == userId, cancellationToken);

        if (estimate == null)
            return HttpResult<byte[]?>.NotFound();

        var businessProfile = await db.BusinessProfiles
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        var pdfBytes = await pdfGenerator.GenerateEstimatePdfAsync(estimate, businessProfile);
        return HttpResult<byte[]?>.Ok(pdfBytes);
    }
}
