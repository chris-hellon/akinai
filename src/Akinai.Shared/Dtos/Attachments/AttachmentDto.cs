namespace Akinai.Shared.Dtos.Attachments;

public record AttachmentDto(
    Guid Id,
    string FileName,
    long FileSize,
    string FormattedFileSize,
    string ContentType,
    string LinkedEntityType,
    Guid LinkedEntityId,
    string? Description,
    DateTime CreatedAt);
