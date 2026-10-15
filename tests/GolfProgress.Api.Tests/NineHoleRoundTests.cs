using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #35. Drives a 9-hole round through compute,
/// micro-wins and next-lever, which all compare its counts against a baseline
/// held in 18-hole equivalents.
/// </summary>
public sealed class NineHoleRoundTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Guid _userId;
    private Guid _nineHoleRoundId;

    /// <summary>
    /// Five 18-hole rounds, each 32 putts, 2 penalties, 1 three-putt and 1 blow-up,
    /// then a 9-hole round of 16 putts, 1 penalty, 1 three-putt and no blow-ups.
    ///
    /// Per 18 holes that 9-hole round is 32 putts, 2 penalties and 2 three-putts:
    /// level on putts and penalties, worse on three-putts. Unscaled, it read as 16
    /// fewer putts and one fewer penalty.
    /// </summary>
    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        _userId = Guid.NewGuid();
        _nineHoleRoundId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "ninehole@example.test"));

            for (var i = 0; i < 5; i++)
            {
                var round = TestWorld.NewRound(Guid.NewGuid(), _userId, 90);
                round.PlayedAt = new DateOnly(2026, 5, 1).AddDays(i);
                db.Rounds.Add(round);
            }

            var nine = TestWorld.NewRound(_nineHoleRoundId, _userId, 45);
            nine.PlayedAt = new DateOnly(2026, 5, 6);
            nine.Holes = 9;
            nine.TotalPutts = 16;
            nine.Penalties = 1;
            nine.ThreePutts = 1;
            nine.BlowUpHoles = 0;
            db.Rounds.Add(nine);

            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task StoredBaseline_HoldsCountsPerEighteenHoles()
    {
        var client = TestWorld.ClientFor(_factory, _userId);
        await client.PostAsync($"/rounds/{_nineHoleRoundId}/compute", content: null);

        var baseline = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");

        // Window is four 18-hole rounds plus the 9-hole one, all 32 putts and 2
        // penalties per 18. Unscaled these were 28.8 and 1.8.
        Assert.Equal(32m, baseline!.AvgPutts);
        Assert.Equal(2m, baseline.AvgPenalties);
    }

    [Fact]
    public async Task MicroWins_DoNotReportAnImprovementForPlayingFewerHoles()
    {
        var client = TestWorld.ClientFor(_factory, _userId);
        await client.PostAsync($"/rounds/{_nineHoleRoundId}/compute", content: null);

        var response = await client.PostAsync($"/rounds/{_nineHoleRoundId}/micro-wins/generate", content: null);
        var generated = await response.Content.ReadFromJsonAsync<GeneratedPayload>();

        var statWin = Assert.Single(generated!.Items, w => w.Category == "StatDelta");

        // Unscaled, 16 putts against an average of 28.8 was reported as "Putting
        // improved". Per 18 holes nothing improved by its threshold.
        Assert.Equal("Stable", statWin.Direction);
        Assert.Equal("penalties", statWin.Metric);
        Assert.Equal(0m, statWin.DeltaValue);
    }

    [Fact]
    public async Task NextLever_ComparesCountsPerEighteenHoles()
    {
        var client = TestWorld.ClientFor(_factory, _userId);
        await client.PostAsync($"/rounds/{_nineHoleRoundId}/compute", content: null);

        var response = await client.PostAsync($"/rounds/{_nineHoleRoundId}/next-lever", content: null);
        var lever = await response.Content.ReadFromJsonAsync<LeverPayload>();

        // Two three-putts per 18 against a baseline of 1.2 is the only count above
        // baseline. Unscaled, one three-putt sat level with the baseline of 1.0 and
        // every lever scored zero.
        Assert.Equal("three_putts", lever!.LeverKey);
    }

    private sealed record BaselinePayload(decimal? AvgPutts, decimal? AvgPenalties);

    private sealed record MicroWinPayload(string Category, string? Metric, string Direction, decimal? DeltaValue);

    private sealed record GeneratedPayload(List<MicroWinPayload> Items);

    private sealed record LeverPayload(string LeverKey);
}
