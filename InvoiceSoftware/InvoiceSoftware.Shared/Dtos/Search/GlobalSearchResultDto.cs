namespace InvoiceSoftware.Shared.Dtos.Search;

public record GlobalSearchItemDto(string Type, string Title, string? Subtitle, string Url);

public record GlobalSearchResultDto(
    List<GlobalSearchItemDto> Clients,
    List<GlobalSearchItemDto> Invoices,
    List<GlobalSearchItemDto> Projects,
    List<GlobalSearchItemDto> Estimates)
{
    public int TotalCount => Clients.Count + Invoices.Count + Projects.Count + Estimates.Count;

    public static GlobalSearchResultDto Empty() => new([], [], [], []);
}
