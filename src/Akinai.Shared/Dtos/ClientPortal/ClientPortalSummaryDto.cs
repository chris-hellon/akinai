namespace Akinai.Shared.Dtos.ClientPortal;

public record ClientPortalSummaryDto(
    string ClientName,
    string? CompanyName,
    string BusinessName,
    byte[]? BusinessLogo,
    string? BusinessLogoContentType,
    int TotalInvoices,
    int UnpaidInvoices,
    decimal TotalOutstanding,
    string Currency,
    int PendingEstimates);
