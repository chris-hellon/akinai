using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Account;

namespace Akinai.Shared.Api.Account;

[Route("api/account/profile")]
public class GetProfile : IGet<ProfileDto>;
