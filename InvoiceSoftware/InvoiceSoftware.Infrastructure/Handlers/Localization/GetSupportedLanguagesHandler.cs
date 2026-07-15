using BlazorUtils.EasyApi;
using BlazorUtils.EasyApi.Server;
using InvoiceSoftware.Domain.Enums;
using InvoiceSoftware.Shared.Api.Localization;
using InvoiceSoftware.Shared.Dtos.Localization;

namespace InvoiceSoftware.Infrastructure.Handlers.Localization;

public class GetSupportedLanguagesHandler : IHandle<GetSupportedLanguages, List<LanguageDto>>
{
    private static readonly Dictionary<SupportedLanguage, (string Name, string NativeName)> LanguageInfo = new()
    {
        [SupportedLanguage.English] = ("English", "English"),
        [SupportedLanguage.Spanish] = ("Spanish", "Español"),
        [SupportedLanguage.French] = ("French", "Français"),
        [SupportedLanguage.German] = ("German", "Deutsch"),
        [SupportedLanguage.Chinese] = ("Chinese (Simplified)", "简体中文"),
        [SupportedLanguage.Vietnamese] = ("Vietnamese", "Tiếng Việt"),
        [SupportedLanguage.Russian] = ("Russian", "Русский")
    };

    public Task<HttpResult<List<LanguageDto>>> Handle(GetSupportedLanguages request, CancellationToken cancellationToken)
    {
        var languages = Enum.GetValues<SupportedLanguage>()
            .Select(lang =>
            {
                var info = LanguageInfo.GetValueOrDefault(lang, (lang.ToString(), lang.ToString()));
                return new LanguageDto(lang.ToString(), info.Item1, info.Item2);
            })
            .ToList();

        return Task.FromResult(HttpResult<List<LanguageDto>>.Ok(languages));
    }
}
