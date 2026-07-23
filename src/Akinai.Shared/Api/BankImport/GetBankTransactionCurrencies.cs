using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.BankImport;

/// <summary>Distinct currencies present in the user's imported transactions (for the filter).</summary>
[Route("api/bank-import/currencies")]
public class GetBankTransactionCurrencies : IGet<List<string>>;
