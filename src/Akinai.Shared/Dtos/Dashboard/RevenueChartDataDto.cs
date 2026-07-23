namespace Akinai.Shared.Dtos.Dashboard;

public record RevenueChartDataDto(
    List<RevenueDataPoint> DataPoints,
    string Currency,
    string Period);

public record RevenueDataPoint(
    string Label,
    decimal Revenue,
    decimal Expenses,
    decimal Profit);
