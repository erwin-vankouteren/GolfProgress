using GolfProgress.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #107: what the Progress section reads. The monthly trend comes from the rounds
/// themselves, not only from months someone generated a review for; reviews are stored
/// under the first of their month; and timeline points say how many holes they were.
/// </summary>
public sealed class ProgressTimelineTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Guid _userId;

    // Relative to today, so "this month" is always the API's current month.
    private static readonly DateOnly ThisMonth =
        new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        _userId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "progress@example.test"));
            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MonthlyTimeline_HasEveryMonth_AveragedFromItsRounds_WithoutAReview()
    {
        var early = await LogAsync(ThisMonth.AddMonths(-2), 92);
        var a = await LogAsync(ThisMonth, 88);
        var b = await LogAsync(ThisMonth, 84);

        var timeline = await Client().GetFromJsonAsync<MonthlyPayload>("/me/timeline/monthly?months=3");

        Assert.Equal(
            new[] { ThisMonth.AddMonths(-2), ThisMonth.AddMonths(-1), ThisMonth }.Select(Iso),
            timeline!.Items.Select(i => i.MonthStart));

        var (first, empty, current) = (timeline.Items[0], timeline.Items[1], timeline.Items[2]);
        Assert.Equal(1, first.RoundsLogged);
        Assert.Equal((decimal)early.FitnessScore!, first.FitnessAvg);

        Assert.Equal(0, empty.RoundsLogged);
        Assert.Null(empty.FitnessAvg);
        Assert.Null(empty.FitnessChange);

        Assert.Equal(2, current.RoundsLogged);
        Assert.Equal(Math.Round((a.FitnessScore!.Value + b.FitnessScore!.Value) / 2m, 2), current.FitnessAvg);
        // Nothing to compare with: last month had no rounds.
        Assert.Null(current.FitnessChange);

        Assert.All(timeline.Items, i => Assert.False(i.HasReview));
    }

    [Fact]
    public async Task MonthlyTimeline_ComparesTheFirstMonth_WithTheMonthBeforeIt()
    {
        var last = await LogAsync(ThisMonth.AddMonths(-1), 90);
        var now = await LogAsync(ThisMonth, 80);

        var timeline = await Client().GetFromJsonAsync<MonthlyPayload>("/me/timeline/monthly?months=1");

        var only = Assert.Single(timeline!.Items);
        Assert.Equal((decimal)(now.FitnessScore!.Value - last.FitnessScore!.Value), only.FitnessChange);
    }

    [Fact]
    public async Task MonthlyTimeline_MarksReviewedMonths()
    {
        await LogAsync(ThisMonth, 88);
        (await Client().PostAsJsonAsync("/me/monthly-reviews/generate", new { monthStart = Iso(ThisMonth) }))
            .EnsureSuccessStatusCode();

        var timeline = await Client().GetFromJsonAsync<MonthlyPayload>("/me/timeline/monthly?months=2");

        Assert.False(timeline!.Items[0].HasReview);
        Assert.True(timeline.Items[1].HasReview);
        Assert.NotEmpty(timeline.Items[1].Highlights);
    }

    [Fact]
    public async Task MonthlyTimeline_IsOnlyTheCallersRounds()
    {
        await LogAsync(ThisMonth, 88);
        var otherId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(otherId, "other@example.test"));
            await db.SaveChangesAsync();
        });

        var timeline = await TestWorld.ClientFor(_factory, otherId)
            .GetFromJsonAsync<MonthlyPayload>("/me/timeline/monthly?months=1");

        Assert.Equal(0, Assert.Single(timeline!.Items).RoundsLogged);
    }

    [Theory]
    [InlineData("2026-04-15")]
    [InlineData("2026-04-30")]
    public async Task GenerateReview_ForADayOtherThanTheFirst_IsRefused(string monthStart)
    {
        var response = await Client().PostAsJsonAsync("/me/monthly-reviews/generate", new { monthStart });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("MonthStart", problem!.Errors.Keys);
        _factory.WithDb(db => Assert.Empty(db.MonthlyReviews.Where(r => r.UserId == _userId)));
    }

    [Fact]
    public async Task GenerateReview_TrimsTheFocus_AndKeepsItWhenLeftOut()
    {
        var month = Iso(ThisMonth);
        var first = await GenerateAsync(new { monthStart = month, focusNextMonth = "  Fewer three-putts  " });
        Assert.Equal("Fewer three-putts", first.FocusNextMonth);

        var again = await GenerateAsync(new { monthStart = month, focusNextMonth = "   " });
        Assert.Equal("Fewer three-putts", again.FocusNextMonth);
    }

    [Fact]
    public async Task GenerateReview_WithTooLongAFocus_IsRefused()
    {
        var response = await Client().PostAsJsonAsync("/me/monthly-reviews/generate",
            new { monthStart = Iso(ThisMonth), focusNextMonth = new string('x', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => string.Equals(k, "FocusNextMonth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateReview_CountsTheFocusInCharacters()
    {
        // 200 golfers are 400 UTF-16 units; the web app counts them as 200, and so must the API.
        var review = await GenerateAsync(new
        {
            monthStart = Iso(ThisMonth),
            focusNextMonth = string.Concat(Enumerable.Repeat("🏌", 200)) + "   "
        });

        Assert.Equal(400, review.FocusNextMonth!.Length);
    }

    [Fact]
    public async Task GenerateReview_ForAMonthWithoutRounds_HasNoAverage()
    {
        var review = await GenerateAsync(new { monthStart = Iso(ThisMonth.AddMonths(-3)) });

        Assert.Null(review.FitnessAvg);
        Assert.Null(review.FitnessChange);
    }

    [Fact]
    public async Task Timeline_PointsSayHowManyHolesTheyWere()
    {
        await LogAsync(ThisMonth, 45, holes: 9);
        await LogAsync(ThisMonth, 90);

        var timeline = await Client().GetFromJsonAsync<TimelinePayload>("/me/timeline");

        Assert.Equal(new short[] { 9, 18 }, timeline!.Items.Select(i => i.Holes));
    }

    [Fact]
    public async Task Baseline_SaysHowManyRoundsItAverages()
    {
        await LogAsync(ThisMonth, 90);
        await LogAsync(ThisMonth, 88);

        var baseline = await Client().GetFromJsonAsync<BaselinePayload>("/me/baseline");
        Assert.Equal(2, baseline!.SampleSize);
        Assert.Equal(5, baseline.WindowRounds);

        for (var i = 0; i < 5; i++) await LogAsync(ThisMonth, 86);

        baseline = await Client().GetFromJsonAsync<BaselinePayload>("/me/baseline");
        Assert.Equal(5, baseline!.SampleSize);
    }

    private async Task<RoundPayload> LogAsync(DateOnly playedAt, int strokes, int holes = 18)
    {
        var response = await Client().PostAsJsonAsync("/me/rounds",
            new { playedAt = Iso(playedAt), holes, totalStrokes = strokes });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoundPayload>())!;
    }

    private async Task<ReviewPayload> GenerateAsync(object body)
    {
        var response = await Client().PostAsJsonAsync("/me/monthly-reviews/generate", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReviewPayload>())!;
    }

    private static string Iso(DateOnly date) => date.ToString(IsoDate.Format);

    private HttpClient Client() => TestWorld.ClientFor(_factory, _userId);

    private sealed record RoundPayload(Guid RoundId, short? FitnessScore);

    private sealed record MonthPayload(
        string MonthStart, decimal? FitnessAvg, decimal? FitnessChange, int RoundsLogged,
        List<string> Highlights, bool HasReview);

    private sealed record MonthlyPayload(List<MonthPayload> Items);

    private sealed record ReviewPayload(decimal? FitnessAvg, decimal? FitnessChange, string? FocusNextMonth);

    private sealed record BaselinePayload(int SampleSize, short WindowRounds);

    private sealed record PointPayload(short Holes);

    private sealed record TimelinePayload(List<PointPayload> Items);
}
