namespace InvoiceSoftware.Domain.Enums;

public enum NotificationType
{
    General = 0,
    PaymentReceived = 1,
    InvoiceSent = 2,
    InvoicePaid = 3,
    InvoiceOverdue = 4,
    EstimateSent = 5,
    EstimateAccepted = 6,
    EstimateViewed = 7,
    EstimateDeclined = 8,
    ReminderSent = 9,
    RecurringInvoiceGenerated = 10,
    BankTransactionMatched = 11,
    ClientAdded = 12
}
