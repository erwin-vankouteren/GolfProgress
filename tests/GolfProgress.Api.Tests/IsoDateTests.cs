using GolfProgress.Api.Contracts;
using System.Globalization;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for the date parsing fixed in #62: dates are exactly yyyy-MM-dd and
/// mean the same thing whatever the server's culture. DateOnly.TryParse read
/// "04-05-2026" as 4 May on a Dutch server and as 5 April on an American one.
/// </summary>
public sealed class IsoDateTests
{
    [Theory]
    [InlineData("nl-NL")]
    [InlineData("en-US")]
    [InlineData("")]   // invariant
    public void ParsesIsoDatesTheSameInEveryCulture(string culture)
    {
        WithCulture(culture, () =>
        {
            Assert.True(IsoDate.TryParse("2026-05-04", out var date));
            Assert.Equal(new DateOnly(2026, 5, 4), date);
        });
    }

    [Theory]
    [InlineData("nl-NL", "04-05-2026")]
    [InlineData("en-US", "05/04/2026")]
    [InlineData("en-US", "May 4, 2026")]
    [InlineData("", "2026-5-4")]
    [InlineData("", "2026-05-04T00:00:00")]
    [InlineData("", "")]
    [InlineData("", null)]
    public void RejectsAnythingButIso_EvenWhereTheCultureWouldAcceptIt(string culture, string? value)
    {
        WithCulture(culture, () => Assert.False(IsoDate.TryParse(value, out _)));
    }

    private static void WithCulture(string name, Action action)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(name);
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
