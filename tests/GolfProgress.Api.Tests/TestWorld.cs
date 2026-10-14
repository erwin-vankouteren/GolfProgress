using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace GolfProgress.Api.Tests;

/// <summary>A seeded user plus the round they own.</summary>
public sealed record Owner(Guid UserId, Guid RoundId);

/// <summary>
/// Seeds two independent users, each with one round, and mints real JWTs for them.
/// Tokens are signed with the same key the test host validates against, so the
/// genuine authentication middleware runs -- nothing is stubbed.
/// </summary>
public static class TestWorld
{
    public static async Task<(Owner alice, Owner bob)> SeedTwoUsersAsync(ApiFactory factory)
    {
        var alice = new Owner(Guid.NewGuid(), Guid.NewGuid());
        var bob = new Owner(Guid.NewGuid(), Guid.NewGuid());

        await factory.WithDbAsync(async db =>
        {
            db.Users.AddRange(
                NewUser(alice.UserId, "alice@example.test"),
                NewUser(bob.UserId, "bob@example.test"));

            db.Rounds.AddRange(
                NewRound(alice.RoundId, alice.UserId),
                NewRound(bob.RoundId, bob.UserId));

            await db.SaveChangesAsync();
        });

        return (alice, bob);
    }

    public static User NewUser(Guid userId, string email) => new()
    {
        UserId = userId,
        Email = email,
        DisplayName = email.Split('@')[0],
        Units = Units.Metric,
        TrackingMode = TrackingMode.Minimal,
        GoalFocus = new List<string>(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    public static Round NewRound(Guid roundId, Guid userId, int totalStrokes = 90) => new()
    {
        RoundId = roundId,
        UserId = userId,
        PlayedAt = new DateOnly(2026, 5, 1),
        Holes = 18,
        TotalStrokes = (short)totalStrokes,
        TotalPutts = 32,
        Penalties = 2,
        ThreePutts = 1,
        BlowUpHoles = 1,
        Source = RoundSource.Manual,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>An HttpClient carrying a valid bearer token for the given user.</summary>
    public static HttpClient ClientFor(ApiFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenFor(userId));
        return client;
    }

    public static string TokenFor(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtKey));
        var token = new JwtSecurityToken(
            issuer: ApiFactory.JwtIssuer,
            audience: ApiFactory.JwtAudience,
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
