using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #22: the baseline window comes from configuration
/// (Progress:BaselineWindowRounds), not a literal in the controller.
/// </summary>
public sealed class ProgressOptionsTests : IDisposable
{
    private readonly ApiFactory _root = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ProgressOptionsTests()
    {
        _factory = _root.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Progress:BaselineWindowRounds"] = "3"
            })));
    }

    public void Dispose()
    {
        _factory.Dispose();
        _root.Dispose();
    }

    [Fact]
    public async Task Compute_UsesTheConfiguredWindow()
    {
        var userId = Guid.NewGuid();
        var latestId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GolfProgressDbContext>();
            db.Users.Add(TestWorld.NewUser(userId, "window@example.test"));

            var strokes = new[] { 100, 98, 96, 94, 92, 90 };
            for (var i = 0; i < strokes.Length; i++)
            {
                var round = TestWorld.NewRound(i == strokes.Length - 1 ? latestId : Guid.NewGuid(), userId, strokes[i]);
                round.PlayedAt = new DateOnly(2026, 5, 1).AddDays(i);
                db.Rounds.Add(round);
            }

            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestWorld.TokenFor(userId));

        await client.PostAsync($"/rounds/{latestId}/compute", content: null);
        var baseline = await client.GetFromJsonAsync<BaselinePayload>("/me/baseline");

        // The three most recent rounds, 94/92/90, rather than the default five.
        Assert.Equal(3, baseline!.WindowRounds);
        Assert.Equal(92m, baseline.AvgScore);
    }

    private sealed record BaselinePayload(short WindowRounds, decimal? AvgScore);
}
