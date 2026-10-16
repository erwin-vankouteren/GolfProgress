using GolfProgress.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Issue #82: PATCH /me saves what the onboarding and Profile screens show. Fields
/// left out are unchanged, and anything invalid is rejected without storing a thing.
/// </summary>
public sealed class UpdateMeTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private Owner _alice = null!;
    private Owner _bob = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        (_alice, _bob) = await TestWorld.SeedTwoUsersAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Patch_ChangesOnlyTheFieldsSent()
    {
        var before = await Client().GetFromJsonAsync<UserPayload>("/me");

        var response = await PatchAsync("""{ "trackingMode": "Balanced" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.Equal("Balanced", updated!.TrackingMode);
        Assert.Equal(before!.Units, updated.Units);
        Assert.Equal(before.DisplayName, updated.DisplayName);
        Assert.Equal(before.GoalFocus, updated.GoalFocus);
        Assert.True(updated.UpdatedAt > before.UpdatedAt);

        // Stored, not just echoed.
        Assert.Equal("Balanced", (await Client().GetFromJsonAsync<UserPayload>("/me"))!.TrackingMode);
    }

    [Fact]
    public async Task Patch_SavesEveryOnboardingAndPreferencesSetting()
    {
        var response = await PatchAsync("""
            {
              "displayName": "  Erwin  ",
              "timezone": "Europe/Amsterdam",
              "units": "imperial",
              "trackingMode": "Detailed",
              "goalFocus": ["putting", "TeeShots"],
              "handicapIndex": 18.4,
              "typical18Score": 92
            }
            """);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await Client().GetFromJsonAsync<UserPayload>("/me");
        Assert.Equal("Erwin", me!.DisplayName);
        Assert.Equal("Europe/Amsterdam", me.Timezone);
        Assert.Equal("Imperial", me.Units);
        Assert.Equal("Detailed", me.TrackingMode);
        // Stored with the API's own spelling, whatever case was sent.
        Assert.Equal(new[] { "Putting", "TeeShots" }, me.GoalFocus);
        Assert.Equal(18.4m, me.HandicapIndex);
        Assert.Equal(92, me.Typical18Score);
        // Email isn't part of PATCH /me.
        Assert.Equal("alice@example.test", me.Email);
    }

    [Fact]
    public async Task Patch_NullClearsTheOptionalFields()
    {
        await PatchAsync("""{ "timezone": "Europe/Amsterdam", "handicapIndex": 18.4, "typical18Score": 92, "goalFocus": ["Putting"] }""");

        var response = await PatchAsync("""{ "timezone": null, "handicapIndex": null, "typical18Score": null, "goalFocus": [] }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await Client().GetFromJsonAsync<UserPayload>("/me");
        Assert.Null(me!.Timezone);
        Assert.Null(me.HandicapIndex);
        Assert.Null(me.Typical18Score);
        Assert.Empty(me.GoalFocus);
    }

    [Fact]
    public async Task Patch_WithNothingToChange_LeavesUpdatedAtAlone()
    {
        var before = await Client().GetFromJsonAsync<UserPayload>("/me");

        var empty = await PatchAsync("{}");
        var same = await PatchAsync("""{ "units": "Metric" }""");

        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        Assert.Equal(before!.UpdatedAt, (await Client().GetFromJsonAsync<UserPayload>("/me"))!.UpdatedAt);
    }

    [Fact]
    public async Task Patch_ChangesOnlyTheCallersProfile()
    {
        await PatchAsync("""{ "displayName": "Changed", "units": "Imperial" }""");

        var bob = await TestWorld.ClientFor(_factory, _bob.UserId).GetFromJsonAsync<UserPayload>("/me");
        Assert.Equal("bob", bob!.DisplayName);
        Assert.Equal("Metric", bob.Units);
    }

    [Theory]
    [InlineData("displayName", "\"\"")]
    [InlineData("displayName", "\"   \"")]
    [InlineData("displayName", "null")]
    [InlineData("displayName", LongName)]
    [InlineData("timezone", "\"Mars/Olympus_Mons\"")]
    [InlineData("timezone", "\"W. Europe Standard Time\"")]   // a Windows id, not IANA
    [InlineData("units", "\"Kelvin\"")]
    [InlineData("units", "\"1\"")]                           // enum by name only
    [InlineData("units", "null")]
    [InlineData("trackingMode", "\"Expert\"")]
    [InlineData("trackingMode", "null")]
    [InlineData("goalFocus", "[\"Putting\", \"Penalties\", \"TeeShots\", \"Consistency\"]")]  // more than 3
    [InlineData("goalFocus", "[\"Driving\"]")]
    [InlineData("goalFocus", "[\"Putting\", \"putting\"]")]
    [InlineData("goalFocus", "null")]
    [InlineData("handicapIndex", "-10.1")]
    [InlineData("handicapIndex", "54.1")]
    [InlineData("typical18Score", "17")]
    [InlineData("typical18Score", "201")]
    public async Task Patch_InvalidField_IsRejectedAndNothingIsStored(string field, string json)
    {
        // A valid change alongside, which must not be stored either.
        var valid = field == "units" ? "\"trackingMode\": \"Detailed\"" : "\"units\": \"Imperial\"";

        var response = await PatchAsync($$"""{ {{valid}}, "{{field}}": {{json}} }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => string.Equals(k, field, StringComparison.OrdinalIgnoreCase));

        var me = await Client().GetFromJsonAsync<UserPayload>("/me");
        Assert.Equal("Minimal", me!.TrackingMode);
        Assert.Equal("Metric", me.Units);
    }

    [Theory]
    [InlineData("-10")]
    [InlineData("54")]
    [InlineData("0")]
    public async Task Patch_HandicapIndexAtTheBounds_IsAccepted(string value)
    {
        var response = await PatchAsync($$"""{ "handicapIndex": {{value}} }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Patch_RequiresAuthentication()
    {
        var response = await _factory.CreateClient().PatchAsync("/me", Json("""{ "units": "Imperial" }"""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Patch_ForATokenWhoseUserNoLongerExists_Returns401()
    {
        var response = await TestWorld.ClientFor(_factory, Guid.NewGuid())
            .PatchAsync("/me", Json("""{ "units": "Imperial" }"""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>The goal focus saved here is what micro-wins tie their focus-area win to.</summary>
    [Fact]
    public async Task SavedGoalFocus_DrivesTheFocusAreaMicroWin()
    {
        await PatchAsync("""{ "goalFocus": ["TeeShots"] }""");

        var generated = await Client().PostAsync($"/rounds/{_alice.RoundId}/micro-wins/generate", content: null);
        var wins = await generated.Content.ReadFromJsonAsync<GeneratedPayload>();

        var focusWin = Assert.Single(wins!.Items, w => w.Category == nameof(MicroWinCategory.FocusArea));
        Assert.Equal("fairways_hit", focusWin.Metric);
        Assert.Equal("Focus-area check: tee shots", focusWin.Title);
    }

    /// <summary>101 characters: one over the limit.</summary>
    private const string LongName =
        "\"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\"";

    private Task<HttpResponseMessage> PatchAsync(string json) => Client().PatchAsync("/me", Json(json));

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private HttpClient Client() => TestWorld.ClientFor(_factory, _alice.UserId);

    private sealed record UserPayload(
        Guid UserId, string? Email, string? DisplayName, string? Timezone, string Units, string TrackingMode,
        decimal? HandicapIndex, int? Typical18Score, List<string> GoalFocus, DateTimeOffset UpdatedAt);

    private sealed record MicroWinPayload(string Category, string? Metric, string Title);

    private sealed record GeneratedPayload(List<MicroWinPayload> Items);
}
