using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.BankImport;

namespace Akinai.Shared.Api.BankImport;

[Route("api/bank-import")]
public class ImportBankStatement : IPost<ImportResultDto>
{
    [BodyParam]
    public byte[] FileContent { get; init; } = null!;

    [BodyParam]
    public string FileName { get; init; } = null!;

    [BodyParam]
    public string FileFormat { get; init; } = null!; // CSV, OFX

    [BodyParam]
    public string Currency { get; init; } = "USD";
}
