using System.Globalization;

namespace GolfProgress.Api.Contracts;

/// <summary>
/// Dates in the API are always ISO 8601 calendar dates, yyyy-MM-dd (#62).
///
/// DateOnly.TryParse follows the server's culture: on a Dutch server "04-05-2026" is
/// 4 May, on an American one 5 April, and both accept forms the contract never
/// promised. Parsing exactly and culture-independently means a date means the same
/// thing wherever the API runs.
/// </summary>
public static class IsoDate
{
    public const string Format = "yyyy-MM-dd";

    public static bool TryParse(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>For values already validated with <see cref="TryParse"/>.</summary>
    public static DateOnly Parse(string value) =>
        DateOnly.ParseExact(value, Format, CultureInfo.InvariantCulture);
}
