namespace Akinai.Shared.Dtos.InvoiceTemplates;

public record TemplatePreviewDto(
    byte[] PdfBytes,
    string ContentType);
