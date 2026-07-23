namespace Akinai.Shared.Api;

public interface IPaginatedRequest
{
    int Page { get; set; }
    int PageSize { get; set; }
    string? Search { get; set; }
}
