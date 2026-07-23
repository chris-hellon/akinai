using Akinai.Domain.Common;
using Akinai.Domain.Enums;
using Akinai.Domain.Exceptions;

namespace Akinai.Domain.Entities;

public class Attachment : AggregateRoot, IAuditableEntity
{
    private static readonly string[] AllowedContentTypes =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "text/csv"
    ];

    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    public string UserId { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public long FileSize { get; private set; }
    public string ContentType { get; private set; } = null!;
    public byte[] FileData { get; private set; } = null!;
    public LinkedEntityType LinkedEntityType { get; private set; }
    public Guid LinkedEntityId { get; private set; }
    public string? Description { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    private Attachment() { }

    public static Attachment Create(
        string userId,
        string fileName,
        string contentType,
        byte[] fileData,
        LinkedEntityType linkedEntityType,
        Guid linkedEntityId,
        string? description = null)
    {
        ValidateFileName(fileName);
        ValidateContentType(contentType);
        ValidateFileSize(fileData.Length);

        return new Attachment
        {
            UserId = userId,
            FileName = SanitizeFileName(fileName),
            ContentType = contentType,
            FileSize = fileData.Length,
            FileData = fileData,
            LinkedEntityType = linkedEntityType,
            LinkedEntityId = linkedEntityId,
            Description = description
        };
    }

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("File name is required");

        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.IndexOfAny(invalidChars) >= 0)
            throw new DomainException("File name contains invalid characters");
    }

    private static void ValidateContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new DomainException("Content type is required");

        if (!AllowedContentTypes.Contains(contentType.ToLowerInvariant()))
            throw new DomainException($"Content type '{contentType}' is not allowed");
    }

    private static void ValidateFileSize(long size)
    {
        if (size <= 0)
            throw new DomainException("File cannot be empty");

        if (size > MaxFileSize)
            throw new DomainException($"File size exceeds maximum allowed size of {MaxFileSize / (1024 * 1024)} MB");
    }

    private static string SanitizeFileName(string fileName)
    {
        return Path.GetFileName(fileName);
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    public string GetFileExtension() => Path.GetExtension(FileName);

    public string GetFormattedFileSize()
    {
        if (FileSize < 1024)
            return $"{FileSize} B";
        if (FileSize < 1024 * 1024)
            return $"{FileSize / 1024.0:F1} KB";
        return $"{FileSize / (1024.0 * 1024):F1} MB";
    }
}
