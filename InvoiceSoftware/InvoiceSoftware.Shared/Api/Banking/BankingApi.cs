using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Banking;

namespace InvoiceSoftware.Shared.Api.Banking;

[Route("api/banking/institutions")]
public class GetBankInstitutions : IGet<List<BankInstitutionDto>>
{
    [QueryStringParam]
    public string Country { get; init; } = "gb";
}

/// <summary>Begin a bank connection — returns the auth URL to redirect the user to.</summary>
[Route("api/banking/connections/start")]
public class StartBankConnection : IPost<BankLinkDto>
{
    [BodyParam]
    public string InstitutionId { get; init; } = string.Empty;

    [BodyParam]
    public string InstitutionName { get; init; } = string.Empty;

    [BodyParam]
    public string Country { get; init; } = "gb";
}

/// <summary>Complete a connection after the bank redirect (by our correlation reference).</summary>
[Route("api/banking/connections/finalize")]
public class FinalizeBankConnection : IPost<BankConnectionDto?>
{
    [BodyParam]
    public string Reference { get; init; } = string.Empty;

    /// <summary>OAuth authorization code from the redirect (for token-based providers like TrueLayer).</summary>
    [BodyParam]
    public string? Code { get; init; }
}

[Route("api/banking/connections")]
public class GetBankConnections : IGet<List<BankConnectionDto>>;

[Route("api/banking/connections/{Id}/sync")]
public class SyncBankConnection : IPost<BankSyncResultDto>
{
    [RouteParam]
    public Guid Id { get; init; }
}

[Route("api/banking/connections/{Id}")]
public class DeleteBankConnection : IDelete
{
    [RouteParam]
    public Guid Id { get; init; }
}
