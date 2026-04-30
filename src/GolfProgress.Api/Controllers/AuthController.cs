using GolfProgress.Api.Contracts.Auth;
using GolfProgress.Api.Contracts.Auth;
using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Controllers;

[ApiController]
public sealed class AuthController : ControllerBase
{
    private readonly GolfProgressDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly JwtTokenService _jwt;
    private readonly IConfiguration _config;
    private readonly RefreshTokenService _refresh;

    public AuthController(GolfProgressDbContext db, IPasswordHasher<User> hasher, JwtTokenService jwt, IConfiguration config, RefreshTokenService refresh)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _config = config;
        _refresh = refresh;
    }

    // POST /auth/register
    [HttpPost("/auth/register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest("Password is required.");
        if (string.IsNullOrWhiteSpace(request.DisplayName)) return BadRequest("DisplayName is required.");

        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await _db.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email, ct);
        if (exists) return Conflict("Email already registered.");

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
        var expiresMinutes = int.Parse(_config["Jwt:ExpiresMinutes"]!);

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
    [HttpPost("/auth/login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest("Password is required.");

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);
        if (user is null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(user.PasswordHash)) return Unauthorized();

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed) return Unauthorized();

        var token = _jwt.CreateToken(user);
        var expiresMinutes = int.Parse(_config["Jwt:ExpiresMinutes"]!);

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
    [HttpPost("/auth/refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest("RefreshToken is required.");

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
        var expiresMinutes = int.Parse(_config["Jwt:ExpiresMinutes"]!);

        return Ok(new AuthResponse
        {
            UserId = user.UserId,
            Token = accessToken,
            RefreshToken = newRaw,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiresMinutes)
        });
    }

    [AllowAnonymous]
    [HttpPost("/auth/logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest("RefreshToken is required.");

        var token = await _refresh.FindValidAsync(request.RefreshToken, ct);
        if (token is null) return NoContent(); // idempotent logout

        await _refresh.RevokeAsync(token, "Logout", ct);
        return NoContent();
    }
}