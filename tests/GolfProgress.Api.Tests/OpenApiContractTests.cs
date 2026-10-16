using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using System.Text.RegularExpressions;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Regression guard for issue #62: the OpenAPI document is complete enough to generate
/// typed clients from. These are rules over every operation, so an endpoint added later
/// fails here until it's documented, rather than producing an untyped client.
/// </summary>
public sealed class OpenApiContractTests : IClassFixture<OpenApiContractTests.Document>
{
    public sealed class Document : IDisposable
    {
        private readonly ApiFactory _factory = new();

        public Document() =>
            Spec = _factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        public OpenApiDocument Spec { get; }

        public void Dispose() => _factory.Dispose();
    }

    private readonly OpenApiDocument _spec;

    public OpenApiContractTests(Document document) => _spec = document.Spec;

    private IEnumerable<(string Name, OpenApiOperation Op)> Operations() =>
        _spec.Paths.SelectMany(path => path.Value.Operations.Select(op =>
            ($"{op.Key.ToString().ToUpperInvariant()} {path.Key}", op.Value)));

    [Fact]
    public void EveryOperation_HasAUniqueCamelCaseOperationId()
    {
        var ids = Operations().Select(o => o.Op.OperationId).ToList();

        Assert.All(Operations(), o => Assert.Matches(new Regex("^[a-z][a-zA-Z0-9]*$"), o.Op.OperationId ?? ""));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void EveryOperation_DocumentsItsSuccessResponseAndServerError()
    {
        Assert.All(Operations(), o =>
        {
            Assert.Contains(o.Op.Responses.Keys, code => code.StartsWith('2'));
            Assert.Contains("500", o.Op.Responses.Keys);
        });
    }

    /// <summary>
    /// The endpoints meant to be reached before signing in. Everything else needs the
    /// bearer token; a new anonymous endpoint must be added here deliberately.
    /// </summary>
    private static bool IsAnonymous(string name) =>
        name.Contains(" /auth/") || name.EndsWith(" /client-config");

    /// <summary>Only the endpoints in <see cref="IsAnonymous"/> skip the bearer token.</summary>
    [Fact]
    public void AuthenticatedOperations_DeclareTheBearerSchemeAnd401()
    {
        Assert.True(_spec.Components.SecuritySchemes.ContainsKey("Bearer"));

        foreach (var (name, op) in Operations())
        {
            var anonymous = IsAnonymous(name);

            if (anonymous)
            {
                Assert.Empty(op.Security);
            }
            else
            {
                Assert.True(op.Security.Count == 1, $"{name} declares no security requirement");
                Assert.True(op.Responses.ContainsKey("401"), $"{name} does not document 401");
            }
        }
    }

    [Fact]
    public void AnonymousOperations_AreRateLimited_AndDocument429WithRetryAfter()
    {
        foreach (var (name, op) in Operations().Where(o => IsAnonymous(o.Name)))
        {
            Assert.True(op.Responses.TryGetValue("429", out var tooMany), $"{name} does not document 429");
            Assert.Contains("Retry-After", tooMany!.Headers.Keys);
        }
    }

    [Fact]
    public void SuccessBodiesAreJson_AndErrorsAreProblemDetails()
    {
        foreach (var (name, op) in Operations())
        {
            foreach (var (status, response) in op.Responses)
            {
                var isError = int.Parse(status) >= 400;

                if (!isError)
                {
                    Assert.All(response.Content.Keys, type => Assert.Equal("application/json", type));
                    continue;
                }

                var media = Assert.Single(response.Content);
                Assert.Equal("application/problem+json", media.Key);
                Assert.True(
                    media.Value.Schema.Reference?.Id is "ProblemDetails" or "ValidationProblemDetails",
                    $"{name} {status} is not described as ProblemDetails");
            }
        }
    }

    /// <summary>
    /// A body-less success (204) has no content; every other success must have a
    /// schema, or the generated client returns an untyped value.
    /// </summary>
    [Fact]
    public void SuccessResponsesWithABody_AreTyped()
    {
        foreach (var (name, op) in Operations())
        {
            foreach (var (status, response) in op.Responses.Where(r => r.Key is "200" or "201"))
                Assert.True(response.Content.TryGetValue("application/json", out var json) && json.Schema is not null,
                    $"{name} {status} has no typed body");
        }
    }

    [Fact]
    public void Created_DocumentsLocation_AndPagedLists_DocumentTheTotal()
    {
        foreach (var (name, op) in Operations())
        {
            if (op.Responses.TryGetValue("201", out var created))
                Assert.True(created.Headers.ContainsKey("Location"), $"{name} 201 does not document Location");

            var paged = op.Parameters.Any(p => p.Name == "skip") && op.Parameters.Any(p => p.Name == "take");
            if (paged)
                Assert.True(op.Responses["200"].Headers.ContainsKey("X-Total-Count"),
                    $"{name} is paged but does not document X-Total-Count");
        }
    }

    /// <summary>
    /// Responses always carry every property (null where there's no value), so all are
    /// required; otherwise generators make them optional and clients need casts.
    /// </summary>
    [Fact]
    public void ResponseModels_MarkEveryPropertyRequired()
    {
        var responseModels = _spec.Components.Schemas
            .Where(s => !s.Key.EndsWith("Request") && s.Key is not ("ProblemDetails" or "ValidationProblemDetails"))
            .ToList();

        Assert.NotEmpty(responseModels);
        Assert.All(responseModels, s =>
            Assert.Equal(s.Value.Properties.Keys.OrderBy(k => k), s.Value.Required.OrderBy(k => k)));
    }

    [Fact]
    public void RequiredRequestFields_AreMarkedRequired()
    {
        Assert.Equal(new[] { "playedAt", "totalStrokes" }, Required("CreateRoundRequest"));
        Assert.Equal(new[] { "email", "password" }, Required("LoginRequest"));
        Assert.Equal(new[] { "practicedAt" }, Required("CreatePracticeSessionRequest"));

        // UserId was accepted but ignored (the caller comes from the token), so it is
        // no longer part of the contract.
        Assert.False(_spec.Components.Schemas["CreateRoundRequest"].Properties.ContainsKey("userId"));
    }

    [Theory]
    [InlineData("UserResponse", "units", new[] { "Metric", "Imperial" })]
    [InlineData("UserResponse", "trackingMode", new[] { "Minimal", "Balanced", "Detailed" })]
    [InlineData("CreatePracticeSessionRequest", "sessionType", new[] { "Range", "Putting", "ShortGame", "Simulator", "Lesson", "Other" })]
    [InlineData("MicroWinResponse", "direction", new[] { "Improved", "Stable", "Declined" })]
    [InlineData("FitnessScoreResponse", "confidenceLevel", new[] { "Low", "Medium", "High" })]
    [InlineData("CreateRoundRequest", "source", new[] { "Manual", "Import", "Coach", "Other" })]
    [InlineData("UpdateMeRequest", "units", new[] { "Metric", "Imperial" })]
    [InlineData("UpdateMeRequest", "trackingMode", new[] { "Minimal", "Balanced", "Detailed" })]
    public void EnumValuedStrings_ListTheirValues(string schema, string property, string[] expected)
    {
        var values = _spec.Components.Schemas[schema].Properties[property].Enum
            .Cast<OpenApiString>().Select(v => v.Value);

        Assert.Equal(expected, values);
    }

    /// <summary>
    /// The goal focus areas apps may send (#82). The response's goalFocus stays a plain
    /// list of strings, so an area added later can't break an installed app.
    /// </summary>
    [Fact]
    public void GoalFocus_ListsItsAreasOnTheRequest_AndStaysPlainStringsOnTheResponse()
    {
        var request = _spec.Components.Schemas["UpdateMeRequest"].Properties["goalFocus"];
        Assert.Equal("array", request.Type);
        Assert.Equal(
            new[] { "Putting", "Penalties", "TeeShots", "Consistency" },
            request.Items.Enum.Cast<OpenApiString>().Select(v => v.Value));

        var response = _spec.Components.Schemas["UserResponse"].Properties["goalFocus"];
        Assert.Empty(response.Items.Enum);
        Assert.Equal("string", response.Items.Type);
    }

    /// <summary>
    /// The response fields agreed in #134 as open: they may gain values at any time,
    /// because installed apps treat unknown values as unknown instead of failing.
    /// Everything else is closed. Changing this list is a contract decision.
    /// </summary>
    private static readonly string[] AgreedOpenEnums =
    {
        "MicroWinResponse.category",
        "PracticeSessionResponse.sessionType",
        "RoundResponse.source",
    };

    [Fact]
    public void OpenEnums_AreExactlyTheAgreedResponseFields_Issue134()
    {
        var open = _spec.Components.Schemas
            .SelectMany(s => s.Value.Properties
                .Where(p => p.Value.Extensions.ContainsKey("x-known-values"))
                .Select(p => $"{s.Key}.{p.Key}"))
            .OrderBy(n => n);

        Assert.Equal(AgreedOpenEnums, open);
    }

    [Theory]
    [InlineData("PracticeSessionResponse", "sessionType", new[] { "Range", "Putting", "ShortGame", "Simulator", "Lesson", "Other" })]
    [InlineData("MicroWinResponse", "category", new[] { "StatDelta", "Behavior", "Narrative", "Habit", "Recovery", "FocusArea" })]
    [InlineData("RoundResponse", "source", new[] { "Manual", "Import", "Coach", "Other" })]
    public void OpenEnums_ArePlainStrings_ListingTheirKnownValues_Issue134(string schema, string property, string[] known)
    {
        var field = _spec.Components.Schemas[schema].Properties[property];

        // No `enum`: a generated client keeps a string, so a new value can't break it.
        Assert.Empty(field.Enum);
        Assert.Equal("string", field.Type);

        var listed = ((OpenApiArray)field.Extensions["x-known-values"]).Cast<OpenApiString>().Select(v => v.Value);
        Assert.Equal(known, listed);
        Assert.Contains("treat any other value as unknown", field.Description);
    }

    /// <summary>Apps only send values they know, so request fields stay typed (#134).</summary>
    [Theory]
    [InlineData("CreatePracticeSessionRequest", "sessionType")]
    [InlineData("CreateRoundRequest", "source")]
    public void RequestEnums_StayClosed_Issue134(string schema, string property)
    {
        var field = _spec.Components.Schemas[schema].Properties[property];

        Assert.NotEmpty(field.Enum);
        Assert.False(field.Extensions.ContainsKey("x-known-values"));
    }

    [Fact]
    public void OptionalStats_AreNullable()
    {
        var round = _spec.Components.Schemas["RoundResponse"];

        Assert.True(round.Properties["totalPutts"].Nullable);
        Assert.False(round.Properties["totalStrokes"].Nullable);
    }

    private string[] Required(string schema) =>
        _spec.Components.Schemas[schema].Required.OrderBy(r => r).ToArray();
}
