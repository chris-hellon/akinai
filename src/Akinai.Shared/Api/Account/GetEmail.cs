using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Account;

namespace Akinai.Shared.Api.Account;

[Route("api/account/email")]
public class GetEmail : IGet<EmailDto>;
