using GolfProgress.Api.Configuration;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace GolfProgress.Api.Security;

/// <summary>
/// Limits login attempts per email address (#6).
///
/// The per-IP limiter on /auth/* cannot see the request body, so it cannot stop a
/// distributed attempt on one account. This runs inside the login action, once the
/// email is known. Every attempt counts, successful or not, so the limit cannot be
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
    public bool TryAcquire(string normalisedEmail, out TimeSpan? retryAfter)
    {
        using var lease = _limiter.AttemptAcquire(normalisedEmail);

        retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : null;
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
