using BlazorUtils.EasyApi;
using InvoiceSoftware.Shared.Dtos.Account;

namespace InvoiceSoftware.Shared.Api.Account;

/// <summary>Lists the passkeys registered to the signed-in account.</summary>
[Route("api/account/passkeys")]
public class GetPasskeys : IGet<List<PasskeyDto>>;
