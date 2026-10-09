namespace Akinai.Shared.Dtos.Timing;

/// <summary>
/// When this invoice is likely to be paid, derived from the user's own history rather
/// than from anything generic. "Your clients in this country take 34 days" is a fact
/// about their business that no competitor has, because it is their data.
/// </summary>
public record PaymentTimingDto(
    /// <summary>Client, Country, Global or None — what the estimate is actually based on.</summary>
    string Basis,
    string Confidence,
    int SampleSize,
    DateOnly? ExpectedPaymentDate,
    DateOnly? EarliestLikelyDate,
    DateOnly? LatestLikelyDate,
    int? MedianDaysAfterDue,
    bool PaysBeforeDue,
    bool IsAlreadyLaterThanUsual,
    int DaysLaterThanUsual,
    string Explanation)
{
    public bool HasPrediction => ExpectedPaymentDate is not null;
}
