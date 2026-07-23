using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Account;

namespace Akinai.Shared.Api.Account;

[Route("api/account/2fa/authenticator-key")]
public class GetAuthenticatorKey : IGet<AuthenticatorKeyDto>;
