using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// "Your next focus" (#292): it points at a stat that got worse than the baseline, and never
/// at one that improved.
/// </summary>
public sealed class NextLeverTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Guid _userId;

    /// <summary>Five rounds of 2 penalties, 1 three-putt and 1 blow-up each: the baseline.</summary>
    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        _userId = Guid.NewGuid();

        await _factory.WithDbAsync(async db =>
        {
            db.Users.Add(TestWorld.NewUser(_userId, "nextlever@example.test"));
            for (var i = 0; i < 5; i++)
            {
                var round = TestWorld.NewRound(Guid.NewGuid(), _userId, 90);
                round.PlayedAt = new DateOnly(2026, 5, 1).AddDays(i);
                db.Rounds.Add(round);
            }

            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task WhenNothingGotWorse_TheFocusIsAStatThatDidNotImprove()
    {
        var lever = await LeverFor(penalties: 0, threePutts: 1, blowUps: 1);

        // Penalties improved; three-putts and blow-ups held level. This used to say "Reduce
        // penalty strokes", with a negative delta as its reason.
        Assert.Equal("three_putts", lever.LeverKey);
        Assert.Equal("Three‑putts are a quick scoring lever.", lever.Rationale);
    }

    [Fact]
    public async Task ARoundBetterOnEveryStat_IsToldToKeepItSteady()
    {
        var lever = await LeverFor(penalties: 0, threePutts: 0, blowUps: 0);

        Assert.Equal("hold_steady", lever.LeverKey);
        Assert.Equal("Keep it steady", lever.Title);
        Assert.Equal("Penalties, 3‑putts and blow‑ups were all at or better than your baseline.", lever.Rationale);
    }

    [Fact]
    public async Task AStatThatGotWorse_IsTheFocus_EvenWhenAnotherImproved()
    {
        var lever = await LeverFor(penalties: 0, threePutts: 3, blowUps: 1);

        Assert.Equal("three_putts", lever.LeverKey);
    }

    [Fact]
    public async Task AStatThatWentDown_IsStillTheFocus_WhenItCostTheMost()
    {
        var lever = await LeverFor(penalties: 1, threePutts: 0, blowUps: 0);

        // Penalties went down, but they're the only strokes this round lost (#299).
        Assert.Equal("penalties", lever.LeverKey);
        Assert.Equal("Penalties went down, but still cost you the most strokes — keep going.", lever.Rationale);
    }

    [Fact]
    public async Task TheGolfersGoal_OutranksAStatThatGotSlightlyWorse()
    {
        var lever = await LeverFor(penalties: 3, threePutts: 1, blowUps: 1, goals: ["Putting"]);

        Assert.Equal("three_putts", lever.LeverKey);
        Assert.Equal("You chose putting as one of your goals.", lever.Rationale);
    }

    [Fact]
    public async Task TheGolfersGoal_StaysTheFocus_WhenItImproved()
    {
        var lever = await LeverFor(penalties: 1, threePutts: 2, blowUps: 1, goals: ["Penalties"]);

        Assert.Equal("penalties", lever.LeverKey);
        Assert.Equal("Better than your average — keep going with your goal: penalties.", lever.Rationale);
    }

    [Fact]
    public async Task AStatFarAboveTheBaseline_OutranksTheGolfersGoal()
    {
        var lever = await LeverFor(penalties: 6, threePutts: 1, blowUps: 1, goals: ["Putting"]);

        Assert.Equal("penalties", lever.LeverKey);
        Assert.StartsWith("Penalties vs baseline", lever.Rationale);
    }

    [Fact]
    public async Task AGoalWithoutAStat_IsSkipped()
    {
        // Tee shots have no lever yet, and a goal whose stat was 0 has nothing to work on.
        var lever = await LeverFor(penalties: 0, threePutts: 3, blowUps: 1, goals: ["TeeShots", "Penalties"]);

        Assert.Equal("three_putts", lever.LeverKey);
    }

    private async Task<LeverPayload> LeverFor(short penalties, short threePutts, short blowUps, List<string>? goals = null)
    {
        var roundId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
            if (goals is not null)
                (await db.Users.FindAsync(_userId))!.GoalFocus = goals;
            var round = TestWorld.NewRound(roundId, _userId, 88);
            round.PlayedAt = new DateOnly(2026, 5, 6);
            round.Penalties = penalties;
            round.ThreePutts = threePutts;
            round.BlowUpHoles = blowUps;
            db.Rounds.Add(round);
            await db.SaveChangesAsync();
        });

        var client = TestWorld.ClientFor(_factory, _userId);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        await client.PostAsync($"/rounds/{roundId}/compute", content: null);
        var response = await client.PostAsync($"/rounds/{roundId}/next-lever", content: null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LeverPayload>())!;
    }

    private sealed record LeverPayload(string LeverKey, string Title, string Rationale);
}
