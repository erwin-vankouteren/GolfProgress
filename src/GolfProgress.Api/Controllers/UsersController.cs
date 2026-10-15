using GolfProgress.Api.Contracts.Users;
using GolfProgress.Api.Security;
using GolfProgress.Domain.Entities;
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
