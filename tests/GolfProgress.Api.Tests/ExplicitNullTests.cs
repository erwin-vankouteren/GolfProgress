using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace GolfProgress.Api.Tests;

/// <summary>
/// The Android app's generated client writes out every field, sending null for each one
/// left empty. An optional field that has a default must take null as that default:
/// "source": null once rejected every round logged on Android with a 400.
/// </summary>
public sealed class ExplicitNullTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Owner _alice = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        (_alice, _) = await TestWorld.SeedTwoUsersAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ARound_AsTheAndroidAppSendsIt_IsSavedAsManual()
    {
        // Captured from the app: every optional field present, empty ones null.
        var response = await PostAsync("/me/rounds", """
            {"playedAt":"2026-10-18","totalStrokes":88,"holes":18,"courseName":null,"teeName":null,
             "totalPutts":32,"penalties":null,"fairwaysHit":null,"gir":null,"threePutts":null,
             "blowUpHoles":null,"source":null,"notes":null}
            """);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Manual", (await response.Content.ReadFromJsonAsync<RoundPayload>())!.Source);
    }

    [Fact]
    public async Task EditingARound_AsTheAndroidAppSendsIt_KeepsWorking()
    {
        var response = await Client().PutAsync($"/rounds/{_alice.RoundId}", Json("""
            {"playedAt":"2026-05-01","totalStrokes":91,"holes":18,"courseName":null,"teeName":null,
             "totalPutts":null,"penalties":null,"fairwaysHit":null,"gir":null,"threePutts":null,
             "blowUpHoles":null,"source":null,"notes":null}
            """));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task APracticeSession_WithNullTypeAndTags_IsARangeSessionWithoutTags()
    {
        var response = await PostAsync("/me/practice-sessions", """
            {"practicedAt":"2026-10-18","durationMinutes":30,"sessionType":null,"venueName":null,
             "tags":null,"primaryGoal":null,"notes":null}
            """);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<PracticePayload>();
        Assert.Equal("Range", session!.SessionType);
        Assert.Empty(session.Tags);
    }

    [Fact]
    public async Task AnUnknownSessionType_IsStillRejected()
    {
        var response = await PostAsync("/me/practice-sessions", """{"practicedAt":"2026-10-18","sessionType":"Bowling"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient Client() => TestWorld.ClientFor(_factory, _alice.UserId);

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> PostAsync(string path, string json) => Client().PostAsync(path, Json(json));

    private sealed record RoundPayload(string Source);

    private sealed record PracticePayload(string SessionType, List<string> Tags);
}
