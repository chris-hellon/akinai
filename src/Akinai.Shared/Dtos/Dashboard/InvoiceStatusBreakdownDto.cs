namespace Akinai.Shared.Dtos.Dashboard;

public record InvoiceStatusBreakdownDto(
    int Draft,
    int Sent,
    int Paid,
    int Overdue,
    int Void,
    decimal DraftTotal,
    decimal SentTotal,
    decimal PaidTotal,
    decimal OverdueTotal);
