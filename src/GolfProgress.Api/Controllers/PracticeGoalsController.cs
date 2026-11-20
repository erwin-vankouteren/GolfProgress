using GolfProgress.Api.Contracts.PracticeGoals;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GolfProgress.Api.Controllers;

/// <summary>
/// Practice goals (#226, docs/coach-access.md → "Practice goals"): the golfer's own routes,
/// and the coach's for a student. A coach needs an active link that shares practice, and only
/// reaches the goals they set; otherwise it's a 404, so they can't tell what exists.
/// </summary>
[Authorize]
[ApiController]
public sealed class PracticeGoalsController : ControllerBase
{
    private readonly PracticeGoalService _goals;
    private readonly CoachAccess _access;

    public PracticeGoalsController(PracticeGoalService goals, CoachAccess access)
    {
        _goals = goals;
        _access = access;
    }

    // ---------------------------------------------------------------- the golfer

    // GET /me/practice-goals
    //
    // Every coach's goals: current ones first, then the rest, newest first.
    [HttpGet("/me/practice-goals", Name = "listMyPracticeGoals")]
    [ProducesResponseType<List<PracticeGoalResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PracticeGoalResponse>>> Mine(CancellationToken ct) =>
        Ok(await _goals.ForGolferAsync(User.GetUserId(), ct));

    // GET /me/practice-goals/{id}
    [HttpGet("/me/practice-goals/{id:guid}", Name = "getMyPracticeGoal")]
    [ProducesResponseType<PracticeGoalDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PracticeGoalDetailResponse>> MineById(Guid id, CancellationToken ct) =>
        await _goals.DetailAsync(User.GetUserId(), id, coachId: null, includeNotes: true, ct) is { } detail
            ? Ok(detail)
            : NotFound();

    // PATCH /me/practice-goals/{id}
    //
    // Mark it done, or dismiss it.
    [HttpPatch("/me/practice-goals/{id:guid}", Name = "updateMyPracticeGoal")]
    [ProducesResponseType<PracticeGoalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PracticeGoalResponse>> SetStatus(
        Guid id, [FromBody] UpdatePracticeGoalStatusRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<PracticeGoalStatusChange>(request.Status, ignoreCase: true, out var change))
            return ErrorHandling.ControllerErrors.FieldError(this, nameof(request.Status), "Status must be Done or Dismissed.");

        var status = change == PracticeGoalStatusChange.Done ? PracticeGoalStatus.Done : PracticeGoalStatus.Dismissed;
        return await _goals.SetStatusAsync(User.GetUserId(), id, status, ct) is { } goal ? Ok(goal) : NotFound();
    }

    // ---------------------------------------------------------------- the coach

    // GET /coach/students/{golferId}/practice-goals
    [HttpGet("/coach/students/{golferId:guid}/practice-goals", Name = "listStudentPracticeGoals")]
    [ProducesResponseType<List<PracticeGoalResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<PracticeGoalResponse>>> Student(Guid golferId, CancellationToken ct)
    {
        if (await PracticeAccessAsync(golferId, ct) is null) return NotFound();
        return Ok(await _goals.ForCoachAsync(User.GetUserId(), golferId, ct));
    }

    // GET /coach/students/{golferId}/practice-goals/{id}
    [HttpGet("/coach/students/{golferId:guid}/practice-goals/{id:guid}", Name = "getStudentPracticeGoal")]
    [ProducesResponseType<PracticeGoalDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PracticeGoalDetailResponse>> StudentById(Guid golferId, Guid id, CancellationToken ct)
    {
        if (await PracticeAccessAsync(golferId, ct) is not { } link) return NotFound();
        return await _goals.DetailAsync(golferId, id, User.GetUserId(), CoachAccess.Covers(link, CoachShare.Reflections), ct)
            is { } detail
            ? Ok(detail)
            : NotFound();
    }

    // POST /coach/students/{golferId}/practice-goals
    [HttpPost("/coach/students/{golferId:guid}/practice-goals", Name = "setPracticeGoal")]
    [ProducesResponseType<PracticeGoalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PracticeGoalResponse>> Create(
        Guid golferId, [FromBody] PracticeGoalRequest request, CancellationToken ct)
    {
        if (await PracticeAccessAsync(golferId, ct) is null) return NotFound();
        return Ok(await _goals.CreateAsync(User.GetUserId(), golferId, request, ct));
    }

    // PUT /coach/students/{golferId}/practice-goals/{id}
    //
    // Replaces the goal's details, validated as when setting one.
    [HttpPut("/coach/students/{golferId:guid}/practice-goals/{id:guid}", Name = "changePracticeGoal")]
    [ProducesResponseType<PracticeGoalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PracticeGoalResponse>> Update(
        Guid golferId, Guid id, [FromBody] PracticeGoalRequest request, CancellationToken ct)
    {
        if (await PracticeAccessAsync(golferId, ct) is null) return NotFound();
        return await _goals.UpdateAsync(User.GetUserId(), golferId, id, request, ct) is { } goal ? Ok(goal) : NotFound();
    }

    // DELETE /coach/students/{golferId}/practice-goals/{id}
    //
    // Only while no session is linked to it: then it's the record of that practice, and can
    // be ended (an earlier end date) instead.
    [HttpDelete("/coach/students/{golferId:guid}/practice-goals/{id:guid}", Name = "removePracticeGoal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid golferId, Guid id, CancellationToken ct)
    {
        if (await PracticeAccessAsync(golferId, ct) is null) return NotFound();
        return await _goals.DeleteAsync(User.GetUserId(), golferId, id, ct) switch
        {
            PracticeGoalProblem.NotFound => NotFound(),
            PracticeGoalProblem.HasSessions => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Sessions are linked to this goal. End it sooner instead."),
            _ => NoContent(),
        };
    }

    private Task<Domain.Entities.CoachLink?> PracticeAccessAsync(Guid golferId, CancellationToken ct) =>
        _access.LinkCoveringAsync(User.GetUserId(), golferId, CoachShare.Practice, ct);
}
