using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Account;

namespace Akinai.Shared.Api.Account;

[Route("api/account/2fa")]
public class GetTwoFactorStatus : IGet<TwoFactorStatusDto>;
