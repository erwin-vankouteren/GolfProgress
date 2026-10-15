using GolfProgress.Api.Contracts.Auth;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController : ControllerBase
{
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
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

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

        var token = _jwt.CreateToken(user);
        var expiresMinutes = _jwt.ExpiresMinutes;

        var (rawRefresh, refreshHash) = _refresh.GenerateToken();
        await _refresh.StoreAsync(user.UserId, refreshHash, ct);

        return Ok(new AuthResponse
        {
            UserId = user.UserId,
            Token = token,
            RefreshToken = rawRefresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiresMinutes)
        });
    }

    // POST /auth/login
    [HttpPost("/auth/login", Name = "login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
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

        var token = _jwt.CreateToken(user);
        var expiresMinutes = _jwt.ExpiresMinutes;

        var (rawRefresh, refreshHash) = _refresh.GenerateToken();
        await _refresh.StoreAsync(user.UserId, refreshHash, ct);

        return Ok(new AuthResponse
        {
            UserId = user.UserId,
            Token = token,
            RefreshToken = rawRefresh,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiresMinutes)
        });
    }

    [AllowAnonymous]
    [HttpPost("/auth/refresh", Name = "refreshToken")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var existing = await _refresh.FindValidAsync(request.RefreshToken, ct);
        if (existing is null) return Unauthorized();

        // Load user
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == existing.UserId, ct);
        if (user is null) return Unauthorized();

        // Rotate refresh token
        var (newRaw, newHash) = _refresh.GenerateToken();
        await _refresh.RotateAsync(existing, newHash, ct);
        await _refresh.StoreAsync(user.UserId, newHash, ct);

        // Issue new access token
        var accessToken = _jwt.CreateToken(user);
        var expiresMinutes = _jwt.ExpiresMinutes;

        return Ok(new AuthResponse
        {
            UserId = user.UserId,
            Token = accessToken,
            RefreshToken = newRaw,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiresMinutes)
        });
    }

    [AllowAnonymous]
    [HttpPost("/auth/logout", Name = "logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var token = await _refresh.FindValidAsync(request.RefreshToken, ct);
        if (token is null) return NoContent(); // idempotent logout

        await _refresh.RevokeAsync(token, "Logout", ct);
        return NoContent();
    }
}