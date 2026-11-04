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

    private async Task<LeverPayload> LeverFor(short penalties, short threePutts, short blowUps)
    {
        var roundId = Guid.NewGuid();
        await _factory.WithDbAsync(async db =>
        {
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
