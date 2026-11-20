using GolfProgress.Api.Contracts.Coaching;
using GolfProgress.Api.OpenApi;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Controllers;

/// <summary>
/// The golfers a coach coaches (#199, docs/coach-access.md → "API"). Starts from the
/// caller's own active links, so nobody else's students can be reached.
/// </summary>
[Authorize]
[ApiController]
public sealed class CoachStudentsController : ControllerBase
{
    private readonly GolfProgressDbContext _db;
    private readonly CoachInvitationService _invitations;
    private readonly CoachAccess _access;
    private readonly StudentOverviewService _overview;
    private readonly FeedbackService _feedback;

    public CoachStudentsController(
        GolfProgressDbContext db,
        CoachInvitationService invitations,
        CoachAccess access,
        StudentOverviewService overview,
        FeedbackService feedback)
    {
        _db = db;
        _invitations = invitations;
        _access = access;
        _overview = overview;
        _feedback = feedback;
    }

    // GET /coach/students?skip=0&take=20
    //
    // Newest activity first; take is clamped to 1..100 and the total is in X-Total-Count.
    // A coach has few students, so they're sorted here rather than in one large query.
    [HttpGet("/coach/students", Name = "listStudents")]
    [ProducesResponseType<List<StudentResponse>>(StatusCodes.Status200OK)]
    [ResponseHeader(StatusCodes.Status200OK, "X-Total-Count", "integer", "Total number of the caller's students.")]
    public async Task<ActionResult<List<StudentResponse>>> List(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var coachId = User.GetUserId();
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 100);

        var links = await _db.CoachLinks.AsNoTracking().Include(l => l.Golfer)
            .Where(l => l.CoachId == coachId && l.EndedAt == null)
            .ToListAsync(ct);
        var golferIds = links.Select(l => l.GolferId).ToList();
        // Practice only counts where it's shared: a date is still something the coach sees.
        var sharingPractice = links.Where(l => l.SharePractice).Select(l => l.GolferId).ToList();

        var lastRounds = await _db.Rounds.AsNoTracking()
            .Where(r => golferIds.Contains(r.UserId))
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, Last = g.Max(r => r.PlayedAt) })
            .ToDictionaryAsync(x => x.UserId, x => x.Last, ct);
        var lastPractice = await _db.PracticeSessions.AsNoTracking()
            .Where(s => sharingPractice.Contains(s.UserId))
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, Last = g.Max(s => s.PracticedAt) })
            .ToDictionaryAsync(x => x.UserId, x => x.Last, ct);

        DateOnly? LastActivityOf(Guid golferId)
        {
            DateOnly? round = lastRounds.TryGetValue(golferId, out var r) ? r : null;
            DateOnly? practice = lastPractice.TryGetValue(golferId, out var p) ? p : null;
            return round > practice || practice is null ? round : practice;
        }

        var unread = await _feedback.UnreadRepliesAsync(coachId, links, ct);

        Response.Headers[RoundsController.TotalCountHeader] = links.Count.ToString();

        return Ok(links
            .Select(l => new StudentResponse
            {
                GolferId = l.GolferId,
                DisplayName = CoachInvitationService.DisplayNameOf(l.Golfer),
                LastActivityOn = LastActivityOf(l.GolferId),
                UnreadReplies = unread.GetValueOrDefault(l.GolferId),
                ShareReflections = l.ShareReflections,
                SharePractice = l.SharePractice,
            })
            .OrderByDescending(s => s.LastActivityOn)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.GolferId)
            .Skip(skip)
            .Take(take)
            .ToList());
    }

    // GET /coach/students/{golferId}/overview
    //
    // What a coach needs before a lesson (#202). 404 when the caller doesn't coach them.
    [HttpGet("/coach/students/{golferId:guid}/overview", Name = "getStudentOverview")]
    [ProducesResponseType<StudentOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentOverviewResponse>> Overview(Guid golferId, CancellationToken ct)
    {
        var link = await _access.LinkCoveringAsync(User.GetUserId(), golferId, CoachShare.RoundsAndProgress, ct);
        if (link is null) return NotFound();

        return Ok(await _overview.BuildAsync(link, ct));
    }

    // DELETE /coach/students/{golferId}
    //
    // The coach stops coaching a golfer (#189). 404, so it doesn't reveal whether a golfer
    // exists, when the caller doesn't coach them.
    [HttpDelete("/coach/students/{golferId:guid}", Name = "stopCoaching")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StopCoaching(Guid golferId, CancellationToken ct) =>
        await _invitations.StopCoachingAsync(User.GetUserId(), golferId, ct) ? NoContent() : NotFound();
}
