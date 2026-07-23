using BlazorUtils.EasyApi;

namespace Akinai.Shared.Api.Account;

[Route("api/account/personal-data")]
public class GetPersonalData : IGet<byte[]>;
