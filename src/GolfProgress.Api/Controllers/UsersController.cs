using GolfProgress.Api.Contracts.Users;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.OpenApi;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class UsersController : ControllerBase
{
    private readonly GolfProgressDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly LoginThrottle _passwordThrottle;
    private readonly AccountDeletionService _accountDeletion;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        GolfProgressDbContext db,
        IPasswordHasher<User> hasher,
        LoginThrottle passwordThrottle,
        AccountDeletionService accountDeletion,
        ILogger<UsersController> logger)
    {
        _db = db;
        _hasher = hasher;
        _passwordThrottle = passwordThrottle;
        _accountDeletion = accountDeletion;
        _logger = logger;
    }

    // GET /me
    //
    // Replaces GET /users/{id}, which was gated on [Authorize(Roles = "admin")]
    // while no role claim is ever issued, so it answered 403 to every caller
    // including the owner of the record. Callers only ever need their own profile,
    // and scoping the lookup to the token removes the need for a role at all.
    [HttpGet("/me", Name = "getMe")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetCurrent(CancellationToken ct)
    {
        var userId = User.GetUserId();

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, ct);

        if (user is null) return NotFound();

        return Ok(ToResponse(user));
    }

    // PATCH /me
    //
    // Updates the caller's profile and preferences (#82): the onboarding after
    // registration and the Profile screens. Only the fields in the body change; see
    // UpdateMeRequest. Email isn't editable here, since changing it needs verification.
    [HttpPatch("/me", Name = "updateMe")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> UpdateCurrent([FromBody] UpdateMeRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null) return NotFound();

        // Values were checked by UpdateMeRequest.Validate before this runs.
        var changed = false;

        if (request.Sent(nameof(request.DisplayName)))
            changed |= Update(user.DisplayName, request.DisplayName!.Trim(), v => user.DisplayName = v);

        if (request.Sent(nameof(request.Timezone)))
            changed |= Update(user.Timezone, request.Timezone, v => user.Timezone = v);

        if (request.Sent(nameof(request.Units)))
        {
            UpdateMeRequest.TryParseName<Units>(request.Units, out var units);
            changed |= Update(user.Units, units, v => user.Units = v);
        }

        if (request.Sent(nameof(request.TrackingMode)))
        {
            UpdateMeRequest.TryParseName<TrackingMode>(request.TrackingMode, out var mode);
            changed |= Update(user.TrackingMode, mode, v => user.TrackingMode = v);
        }

        if (request.Sent(nameof(request.GoalFocus)))
        {
            // Stored by the enum's own spelling, whatever case was sent.
            var areas = request.GoalFocus!
                .Select(f => { UpdateMeRequest.TryParseName<GoalFocusArea>(f, out var a); return a.ToString(); })
                .ToList();

            if (!areas.SequenceEqual(user.GoalFocus))
            {
                user.GoalFocus = areas;
                changed = true;
            }
        }

        if (request.Sent(nameof(request.HandicapIndex)))
            changed |= Update(user.HandicapIndex, request.HandicapIndex, v => user.HandicapIndex = v);

        if (request.Sent(nameof(request.Typical18Score)))
            changed |= Update(user.Typical18Score, request.Typical18Score, v => user.Typical18Score = v);

        if (changed)
        {
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(ToResponse(user));
    }

    // DELETE /me
    //
    // Deletes the account and everything in it (#84), confirmed with the password. The
    // App Store and Google Play require it. Every device is signed out: the refresh
    // tokens are deleted, and ActiveUserCheck rejects the access tokens still in use.
    // A wrong password is a 400 rather than a 401, so the app doesn't sign the user out.
    // The web app then calls POST /auth/logout, which clears its refresh cookie.
    [HttpDelete("/me", Name = "deleteMe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ResponseHeader(StatusCodes.Status429TooManyRequests, "Retry-After", "integer", "Seconds to wait before retrying.")]
    public async Task<IActionResult> DeleteCurrent([FromBody] DeleteAccountRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();

        if (!_passwordThrottle.TryAcquireForUser(userId, out var retryAfter))
        {
            if (retryAfter is { } wait)
                Response.Headers.RetryAfter = ((int)Math.Ceiling(wait.TotalSeconds)).ToString();

            return StatusCode(StatusCodes.Status429TooManyRequests);
        }

        // ActiveUserCheck has already confirmed the user exists.
        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.UserId == userId, ct);

        if (string.IsNullOrWhiteSpace(user.PasswordHash) ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return this.FieldError(nameof(request.Password), "The password is incorrect.");
        }

        await _accountDeletion.DeleteAsync(userId, ct);

        // The only record left that the account existed. The id is a random GUID,
        // with nothing personal left to link it to.
        _logger.LogInformation("Account {UserId} deleted at the user's request.", userId);

        return NoContent();
    }

    private static bool Update<T>(T current, T value, Action<T> set)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return false;
        set(value);
        return true;
    }

    private static UserResponse ToResponse(User user) => new()
    {
        UserId = user.UserId,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Timezone = user.Timezone,
        Units = user.Units.ToString(),
        TrackingMode = user.TrackingMode.ToString(),
        HandicapIndex = user.HandicapIndex,
        Typical18Score = user.Typical18Score,
        GoalFocus = user.GoalFocus ?? new List<string>(),
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };
}
