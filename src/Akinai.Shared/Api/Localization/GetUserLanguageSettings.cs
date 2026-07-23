using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Localization;

namespace Akinai.Shared.Api.Localization;

[Route("api/localization/settings")]
public class GetUserLanguageSettings : IGet<UserLanguageSettingsDto?>
{
}
