using GolfProgress.Api.Caching;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Security;

/// <summary>
/// Rejects an access token whose user no longer exists (#84).
///
/// Access tokens live for 15 minutes and can't be revoked, so after an account is
/// deleted its tokens still validate. Without this check a request in that window would
/// reach the endpoints as a user with no row: reads would return empty data, and a write
/// would fail on the foreign key. Answering 401 instead sends the client to refresh,
/// which fails because the refresh tokens were deleted with the account, so it signs out.
///
/// The answer is cached per user. Deleting the user is a save that touches their row,
/// so the cache interceptor (#14) evicts it at once.
/// </summary>
public static class ActiveUserCheck
{
    public static async Task OnTokenValidated(TokenValidatedContext context)
    {
        if (context.Principal is null) return;

        var userId = context.Principal.GetUserId();
        var services = context.HttpContext.RequestServices;
        var cache = services.GetRequiredService<UserScopedCache>();

        var exists = await cache.GetOrCreateAsync(userId, CacheKeys.UserExists(userId), () =>
            services.GetRequiredService<GolfProgressDbContext>().Users
                .AnyAsync(u => u.UserId == userId, context.HttpContext.RequestAborted));

        if (!exists) context.Fail("The user no longer exists.");
    }
}
