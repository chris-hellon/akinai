using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Account;

namespace Akinai.Shared.Api.Account;

[Route("api/account/2fa/recovery-codes")]
public class GenerateRecoveryCodes : IPost<RecoveryCodesDto>;
