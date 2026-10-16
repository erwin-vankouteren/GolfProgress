using GolfProgress.Api.Configuration;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace GolfProgress.Api.Security;

/// <summary>
/// Limits password attempts on one account: per email address at login (#6), and per
/// user for actions a signed-in user confirms with their password (#84, #83), so a
/// stolen access token can't be used to guess the password either.
///
/// The per-IP limiter on /auth/* cannot see the request body, so it cannot stop a
/// distributed attempt on one account. This runs inside the action, once the account
/// is known. Every attempt counts, successful or not, so the limit cannot be
/// probed to learn whether a password was right.
/// </summary>
public sealed class LoginThrottle : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public LoginThrottle(IOptions<AuthRateLimitOptions> options)
    {
        var o = options.Value;

        _limiter = PartitionedRateLimiter.Create<string, string>(email =>
            RateLimitPartition.GetFixedWindowLimiter(email, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = o.PerEmailPermitLimit,
                Window = TimeSpan.FromSeconds(o.PerEmailWindowSeconds),
                QueueLimit = 0
            }));
    }

    /// <summary>
    /// Records an attempt against <paramref name="normalisedEmail"/>. Returns false,
    /// with how long to wait, once the limit for the current window is spent.
    /// </summary>
    public bool TryAcquire(string normalisedEmail, out TimeSpan? retryAfter) =>
        TryAcquireKey(normalisedEmail, out retryAfter);

    /// <summary>
    /// Records a password attempt by the signed-in user <paramref name="userId"/>, with
    /// the same limit as login. Kept apart from the login count, so neither can lock the
    /// other out. An email can't collide with the key: it contains an @.
    /// </summary>
    public bool TryAcquireForUser(Guid userId, out TimeSpan? retryAfter) =>
        TryAcquireKey($"user:{userId}", out retryAfter);

    private bool TryAcquireKey(string key, out TimeSpan? retryAfter)
    {
        using var lease = _limiter.AttemptAcquire(key);

        retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : null;
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
