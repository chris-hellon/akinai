using BlazorUtils.EasyApi;
using Akinai.Shared.Dtos.Localization;

namespace Akinai.Shared.Api.Localization;

[Route("api/localization/languages")]
public class GetSupportedLanguages : IGet<List<LanguageDto>>
{
}
