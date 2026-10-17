using GolfProgress.Api.Contracts;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #161: Home's "This month" comes from this calendar month's rounds, without
/// anyone generating a monthly review, and follows the rounds as they change.
/// </summary>
public sealed class DashboardThisMonthTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Guid _userId;

    private static readonly DateOnly ThisMonth =
        new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        _userId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "home@example.test"));
            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ThisMonth_IsAveragedFromItsRounds_WithoutAReview()
    {
        var last = await LogAsync(ThisMonth.AddMonths(-1), 92);
        var a = await LogAsync(ThisMonth, 88);
        var b = await LogAsync(ThisMonth, 84);

        var dashboard = await DashboardAsync();

        var average = Math.Round((a.FitnessScore!.Value + b.FitnessScore!.Value) / 2m, 2);
        Assert.Equal(average, dashboard.MonthlyFitnessAvg);
        Assert.Equal(average - last.FitnessScore!.Value, dashboard.MonthlyFitnessChange);
    }

    [Fact]
    public async Task ThisMonth_IsEmpty_WhenOnlyEarlierMonthsHaveRounds_EvenWithAReview()
    {
        var month = ThisMonth.AddMonths(-2);
        await LogAsync(month, 90);
        (await Client().PostAsJsonAsync("/me/monthly-reviews/generate", new { monthStart = Iso(month) }))
            .EnsureSuccessStatusCode();

        var dashboard = await DashboardAsync();

        // An old review no longer stands in for this month.
        Assert.Null(dashboard.MonthlyFitnessAvg);
        Assert.Null(dashboard.MonthlyFitnessChange);
    }

    [Fact]
    public async Task ThisMonth_FollowsTheRounds_AsTheyAreLoggedAndDeleted()
    {
        Assert.Null((await DashboardAsync()).MonthlyFitnessAvg);

        var round = await LogAsync(ThisMonth, 88);
        Assert.Equal((decimal)round.FitnessScore!.Value, (await DashboardAsync()).MonthlyFitnessAvg);

        (await Client().DeleteAsync($"/rounds/{round.RoundId}")).EnsureSuccessStatusCode();
        Assert.Null((await DashboardAsync()).MonthlyFitnessAvg);
    }

    private async Task<RoundPayload> LogAsync(DateOnly playedAt, int strokes)
    {
        var response = await Client().PostAsJsonAsync("/me/rounds",
            new { playedAt = Iso(playedAt), holes = 18, totalStrokes = strokes });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoundPayload>())!;
    }

    private async Task<DashboardPayload> DashboardAsync() =>
        (await Client().GetFromJsonAsync<DashboardPayload>("/me/dashboard"))!;

    private static string Iso(DateOnly date) => date.ToString(IsoDate.Format);

    private HttpClient Client() => TestWorld.ClientFor(_factory, _userId);

    private sealed record RoundPayload(Guid RoundId, short? FitnessScore);

    private sealed record DashboardPayload(decimal? MonthlyFitnessAvg, decimal? MonthlyFitnessChange);
}
