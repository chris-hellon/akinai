namespace Akinai.Shared.Dtos.ClientPortal;

public record ClientPortalInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    DateOnly IssueDate,
    DateOnly DueDate,
    string Status,
    decimal Total,
    string Currency,
    DateOnly? PaidDate,
    Guid? PublicAccessToken);
