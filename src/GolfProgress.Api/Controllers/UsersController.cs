using GolfProgress.Api.Contracts.Users;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace GolfProgress.Api.Controllers
{
    
    [ApiController]
    [Route("users")]
    public sealed class UsersController : ControllerBase
    {
        private readonly GolfProgressDbContext _db;

        public UsersController(GolfProgressDbContext db) => _db = db;

        [HttpPost]
        public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName))
                return BadRequest("DisplayName is required.");

            var units = Enum.TryParse<Units>(request.Units, ignoreCase: true, out var u) ? u : Units.Metric;
            var tracking = Enum.TryParse<TrackingMode>(request.TrackingMode, ignoreCase: true, out var t) ? t : TrackingMode.Minimal;
            var now = DateTimeOffset.UtcNow;
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                DisplayName = request.DisplayName.Trim(),
                Timezone = request.Timezone,
                Units = units,
                TrackingMode = tracking,
                HandicapIndex = request.HandicapIndex,
                Typical18Score = request.Typical18Score,
                GoalFocus = request.GoalFocus ?? new List<string>(),
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
            var response = ToResponse(user);
            return CreatedAtAction(nameof(GetById), new {id = user.UserId}, response);
        }

        [Authorize(Roles ="admin")]
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
        {

            var user = await _db.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.UserId == id, ct);

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
}
