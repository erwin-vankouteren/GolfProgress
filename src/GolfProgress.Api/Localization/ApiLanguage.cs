using System.Globalization;
using GolfProgress.Domain.Enums;
using Microsoft.AspNetCore.Localization;

namespace GolfProgress.Api.Localization;

/// <summary>
/// Which language a request is answered in: its Accept-Language, matched against the
/// languages the apps come in, else English. The apps send the language they show, the
/// user's choice or the device's, so the API's texts match the screen around them.
/// </summary>
public static class ApiLanguage
{
    private static readonly Dictionary<AppLanguage, string> Codes = new()
    {
        [AppLanguage.English] = "en",
        [AppLanguage.Dutch] = "nl",
    };

    public static string Code(AppLanguage language) => Codes[language];

    public static AppLanguage FromCulture(CultureInfo culture) =>
        Codes.FirstOrDefault(c => c.Value == culture.TwoLetterISOLanguageName).Key;

    /// <summary>
    /// Sets only the UI culture from Accept-Language. The culture stays invariant, so
    /// nothing that parses or formats data starts reading "1,5" the Dutch way; texts
    /// format their own numbers (<see cref="ApiTexts.Culture"/>).
    /// </summary>
    public static void Configure(RequestLocalizationOptions options)
    {
        var languages = Codes.Values.Select(CultureInfo.GetCultureInfo).ToList();
        options.DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture, languages[0]);
        options.SupportedCultures = new List<CultureInfo> { CultureInfo.InvariantCulture };
        options.SupportedUICultures = languages;
        // nl-NL, nl-BE → nl; en-GB → en.
        options.FallBackToParentUICultures = true;
        options.RequestCultureProviders = new List<IRequestCultureProvider> { new AcceptLanguageHeaderRequestCultureProvider() };
    }
}
