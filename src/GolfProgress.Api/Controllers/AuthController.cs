using GolfProgress.Api.Contracts.Auth;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Controllers;

/// <summary>
/// Sign-in endpoints. Every success returns a short-lived access token and a refresh
/// token, which reaches the client in one of two ways (#68):
///
/// - in the response body (the default), for the mobile apps, which keep it in the
///   Keychain / Android Keystore;
/// - in an HttpOnly cookie when the request sends <c>X-Refresh-Token-Mode: cookie</c>,
///   for the web app, so script can never read it. The body's refreshToken is then null.
///
/// /auth/refresh and /auth/logout read the cookie when the body has no token.
/// </summary>
[ApiController]
[EnableRateLimiting(RateLimitPolicies.Auth)]
[EnableCors(CorsPolicies.AuthWithCredentials)]
public sealed class AuthController : ControllerBase
{
    /// <summary>Request header a web client sends to receive the refresh token as a cookie.</summary>
    public const string RefreshTokenModeHeader = "X-Refresh-Token-Mode";

    public const string RefreshTokenCookie = "gp_refresh";

    /// <summary>
    /// The cookie is only sent to the auth endpoints, never to the rest of the API, so no
    /// other request carries the 14-day credential.
    /// </summary>
    public const string RefreshTokenCookiePath = "/auth";

    private readonly GolfProgressDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly JwtTokenService _jwt;
    private readonly RefreshTokenService _refresh;
    private readonly LoginThrottle _loginThrottle;

    public AuthController(GolfProgressDbContext db, IPasswordHasher<User> hasher, JwtTokenService jwt, RefreshTokenService refresh, LoginThrottle loginThrottle)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _refresh = refresh;
        _loginThrottle = loginThrottle;
    }

    // POST /auth/register
    [HttpPost("/auth/register", Name = "register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        [FromHeader(Name = RefreshTokenModeHeader)] string? refreshTokenMode,
        CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (PasswordPolicy.Check(request.Password, email) is { } passwordProblem)
            return this.FieldError(nameof(request.Password), passwordProblem);

        var exists = await _db.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email, ct);
        if (exists) return Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered.");

        var now = DateTimeOffset.UtcNow;

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Units = Units.Metric,
            TrackingMode = TrackingMode.Minimal,
            GoalFocus = new List<string>(),
            CreatedAt = now,
            UpdatedAt = now
        };

        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Ok(await SignInAsync(user, UsesCookie(refreshTokenMode), replacing: null, ct));
    }

    // POST /auth/login
    [HttpPost("/auth/login", Name = "login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        [FromHeader(Name = RefreshTokenModeHeader)] string? refreshTokenMode,
        CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Checked before the account is looked up, so unknown and known emails are
        // throttled alike and the limit reveals nothing about which accounts exist.
        if (!_loginThrottle.TryAcquire(email, out var retryAfter))
        {
            if (retryAfter is { } wait)
                Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString();

            return StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);
        if (user is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(user.PasswordHash)) return Unauthorized();

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed) return Unauthorized();

        return Ok(await SignInAsync(user, UsesCookie(refreshTokenMode), replacing: null, ct));
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token, rotating the refresh token. The
    /// token comes from the body, or from the cookie when the body has none; a token that
    /// came from the cookie is renewed in the cookie.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthSession)]
    [HttpPost("/auth/refresh", Name = "refreshToken")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequest? request,
        [FromHeader(Name = RefreshTokenModeHeader)] string? refreshTokenMode,
        CancellationToken ct)
    {
        var (raw, fromCookie) = SubmittedRefreshToken(request);
        if (raw is null)
            return this.FieldError(nameof(RefreshRequest.RefreshToken), "RefreshToken is required, in the body or the refresh cookie.");

        var existing = await _refresh.FindValidAsync(raw, ct);
        if (existing is null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == existing.UserId, ct);
        if (user is null) return Unauthorized();

        return Ok(await SignInAsync(user, fromCookie || UsesCookie(refreshTokenMode), replacing: existing, ct));
    }

    /// <summary>
    /// Revokes the refresh token from the body or the cookie, and clears the cookie.
    /// Idempotent: signing out without a token, or with one that's no longer valid, still
    /// succeeds.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthSession)]
    [HttpPost("/auth/logout", Name = "logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequest? request,
        CancellationToken ct)
    {
        var (raw, _) = SubmittedRefreshToken(request);

        if (raw is not null && await _refresh.FindValidAsync(raw, ct) is { } token)
            await _refresh.RevokeAsync(token, "Logout", ct);

        Response.Cookies.Delete(RefreshTokenCookie, RefreshCookieOptions(expires: null));
        return NoContent();
    }

    /// <summary>
    /// Emails a password reset link (#86). Always 202, whether or not the address has an
    /// account, and just as fast either way: the email is sent in the background. An
    /// account gets at most a few per hour.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("/auth/password-reset", Name = "requestPasswordReset")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestPasswordReset(
        [FromBody] PasswordResetRequest request,
        [FromServices] PasswordResetService reset,
        CancellationToken ct)
    {
        await reset.RequestAsync(request.Email, ct);
        return Accepted();
    }

    /// <summary>
    /// Sets a new password from a reset link (#86). The link works once, for a limited
    /// time; afterwards every session on every device is signed out.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("/auth/password-reset/confirm", Name = "confirmPasswordReset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmPasswordReset(
        [FromBody] ConfirmPasswordResetRequest request,
        [FromServices] PasswordResetService reset,
        CancellationToken ct)
    {
        var user = await reset.FindUserAsync(request.Token, ct);
        if (user is null)
            return this.FieldError(nameof(request.Token), "This reset link is invalid or has expired. Ask for a new one.");

        if (PasswordPolicy.Check(request.NewPassword, user.Email) is { } problem)
            return this.FieldError(nameof(request.NewPassword), problem);

        await reset.CompleteAsync(user, _hasher.HashPassword(user, request.NewPassword), ct);
        return NoContent();
    }

    private static bool UsesCookie(string? refreshTokenMode) =>
        string.Equals(refreshTokenMode, "cookie", StringComparison.OrdinalIgnoreCase);

    private (string? Token, bool FromCookie) SubmittedRefreshToken(RefreshRequest? request)
    {
        if (!string.IsNullOrWhiteSpace(request?.RefreshToken)) return (request.RefreshToken, false);

        return Request.Cookies.TryGetValue(RefreshTokenCookie, out var cookie) && !string.IsNullOrWhiteSpace(cookie)
            ? (cookie, true)
            : (null, false);
    }

    /// <summary>
    /// Issues an access token and a new refresh token, replacing (rotating) an old one
    /// when refreshing. The refresh token goes in the cookie or the body.
    /// </summary>
    private async Task<AuthResponse> SignInAsync(User user, bool cookie, RefreshToken? replacing, CancellationToken ct)
    {
        var (raw, hash) = _refresh.GenerateToken();
        if (replacing is not null) await _refresh.RotateAsync(replacing, hash, ct);
        var stored = await _refresh.StoreAsync(user.UserId, hash, ct);

        if (cookie) Response.Cookies.Append(RefreshTokenCookie, raw, RefreshCookieOptions(stored.ExpiresAt));

        return new AuthResponse
        {
            UserId = user.UserId,
            Token = _jwt.CreateToken(user, sessionId: stored.RefreshTokenId),
            RefreshToken = cookie ? null : raw,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_jwt.ExpiresMinutes)
        };
    }

    /// <summary>
    /// HttpOnly so script can't read it; Secure so it only travels over HTTPS (browsers
    /// treat localhost as secure, so local development works); SameSite=Strict so other
    /// sites can't make the browser send it; and scoped to the auth endpoints.
    /// </summary>
    private static CookieOptions RefreshCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = RefreshTokenCookiePath,
        Expires = expires,
        IsEssential = true
    };
}
