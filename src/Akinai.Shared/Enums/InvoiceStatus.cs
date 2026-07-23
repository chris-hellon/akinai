namespace Akinai.Shared.Enums;

public enum InvoiceStatus
{
    Draft,
    Sent,
    Paid,
    Overdue,
    Void
}

public static class InvoiceStatusExtensions
{
    public static string GetBadgeClass(this InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "badge-neutral",
        InvoiceStatus.Sent => "badge-info",
        InvoiceStatus.Paid => "badge-success",
        InvoiceStatus.Overdue => "badge-danger",
        InvoiceStatus.Void => "badge-neutral",
        _ => "badge-neutral"
    };

    public static InvoiceStatus Parse(string status) => status switch
    {
        "Draft" => InvoiceStatus.Draft,
        "Sent" => InvoiceStatus.Sent,
        "Paid" => InvoiceStatus.Paid,
        "Overdue" => InvoiceStatus.Overdue,
        "Void" => InvoiceStatus.Void,
        _ => InvoiceStatus.Draft
    };
}
