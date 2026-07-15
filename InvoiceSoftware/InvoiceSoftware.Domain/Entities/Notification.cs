using InvoiceSoftware.Domain.Common;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Domain.Exceptions;

namespace InvoiceSoftware.Domain.Entities;

public class Notification : AggregateRoot, IAuditableEntity
{
    public string UserId { get; private set; } = null!;
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    /// <summary>Optional in-app route the notification links to, e.g. "/invoices/{id}".</summary>
    public string? Link { get; private set; }

    public LinkedEntityType? RelatedEntityType { get; private set; }
    public Guid? RelatedEntityId { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private Notification() { }

    public static Notification Create(
        string userId,
        NotificationType type,
        string title,
        string message,
        string? link = null,
        LinkedEntityType? relatedEntityType = null,
        Guid? relatedEntityId = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("A notification requires a recipient user");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("A notification requires a title");

        return new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message ?? string.Empty,
            Link = link,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            IsRead = false
        };
    }

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
