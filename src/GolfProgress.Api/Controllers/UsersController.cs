using GolfProgress.Api.Contracts.Users;
using GolfProgress.Api.Security;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Controllers;

[Authorize]
[ApiController]
public sealed class UsersController : ControllerBase
{
    private readonly GolfProgressDbContext _db;

    public UsersController(GolfProgressDbContext db) => _db = db;

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
