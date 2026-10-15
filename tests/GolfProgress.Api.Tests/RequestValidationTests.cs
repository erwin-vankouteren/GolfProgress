using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #11: out-of-range and inconsistent values are rejected
/// with 400 and a validation error naming the field, and nothing is stored.
/// </summary>
public sealed class RequestValidationTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Owner _owner = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        (_owner, _) = await TestWorld.SeedTwoUsersAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static Dictionary<string, object?> ValidRound(short holes = 18) => new()
    {
        ["playedAt"] = "2026-05-01",
        ["holes"] = holes,
        ["totalStrokes"] = holes == 9 ? 45 : 90,
        ["totalPutts"] = holes == 9 ? 16 : 32,
        ["penalties"] = 1,
        ["fairwaysHit"] = holes == 9 ? 4 : 7,
        ["gir"] = holes == 9 ? 3 : 6,
        ["threePutts"] = 1,
        ["blowUpHoles"] = 1
    };

    [Theory]
    [InlineData(18)]
    [InlineData(9)]
    public async Task CreateRound_ValidRound_IsAccepted(short holes)
    {
        var response = await Client().PostAsJsonAsync("/me/rounds", ValidRound(holes));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    // field, value, holes
    [InlineData("totalStrokes", 0, 18)]
    [InlineData("holes", 12, 18)]
    [InlineData("totalPutts", -1, 18)]
    [InlineData("penalties", -1, 18)]
    [InlineData("threePutts", -1, 18)]
    [InlineData("blowUpHoles", -1, 18)]
    [InlineData("fairwaysHit", -1, 18)]
    [InlineData("gir", -1, 18)]
    [InlineData("fairwaysHit", 18, 9)]   // the example from the issue
    [InlineData("gir", 10, 9)]
    [InlineData("threePutts", 10, 9)]
    [InlineData("blowUpHoles", 19, 18)]
    [InlineData("totalPutts", 91, 18)]   // more putts than strokes
    [InlineData("penalties", 91, 18)]
    public async Task CreateRound_InvalidField_IsRejectedAndNotStored(string field, int value, short holes)
    {
        var body = ValidRound(holes);
        body[field] = value;

        var response = await Client().PostAsJsonAsync("/me/rounds", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => string.Equals(k, field, StringComparison.OrdinalIgnoreCase));

        // Only the round seeded for this user exists.
        _factory.WithDb(db => Assert.Single(db.Rounds.Where(r => r.UserId == _owner.UserId)));
    }

    [Theory]
    [InlineData("focusRating", 0)]
    [InlineData("focusRating", 6)]
    [InlineData("moodRating", 0)]
    [InlineData("moodRating", 6)]
    public async Task CreateReflection_RatingOutsideOneToFive_IsRejected(string field, int value)
    {
        var response = await Client().PostAsJsonAsync(
            $"/rounds/{_owner.RoundId}/reflections",
            new Dictionary<string, object> { [field] = value });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _factory.WithDb(db => Assert.Empty(db.Reflections.Where(r => r.RoundId == _owner.RoundId)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task CreateReflection_RatingAtTheBounds_IsAccepted(int rating)
    {
        var response = await Client().PostAsJsonAsync(
            $"/rounds/{_owner.RoundId}/reflections",
            new { focusRating = rating, moodRating = rating });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient Client() => TestWorld.ClientFor(_factory, _owner.UserId);
}
