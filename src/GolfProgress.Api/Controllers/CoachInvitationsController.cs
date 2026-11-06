using GolfProgress.Api.Contracts.Coaching;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GolfProgress.Api.Controllers;

/// <summary>
/// Coach invitations (#184, docs/coach-access.md → "Inviting a coach"): the golfer invites,
/// lists and cancels; the coach, signed in with the invited address, sees what's offered,
/// then accepts or declines.
/// </summary>
[Authorize]
[ApiController]
public sealed class CoachInvitationsController : ControllerBase
{
    private readonly CoachInvitationService _invitations;

    public CoachInvitationsController(CoachInvitationService invitations) => _invitations = invitations;

    // GET /me/coaches
    //
    // Active coaches, and invitations that can still be accepted.
    [HttpGet("/me/coaches", Name = "listCoaches")]
    [ProducesResponseType<CoachesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CoachesResponse>> ListCoaches(CancellationToken ct)
    {
        var (coaches, invitations) = await _invitations.ListAsync(User.GetUserId(), ct);
        return Ok(new CoachesResponse
        {
            Coaches = coaches.Select(ToResponse).ToList(),
            Invitations = invitations.Select(i => ToResponse(i, resent: false)).ToList(),
        });
    }

    // PATCH /me/coaches/{linkId}
    //
    // Changes what a coach sees, from their next request on (#189).
    [HttpPatch("/me/coaches/{linkId:guid}", Name = "updateCoach")]
    [ProducesResponseType<CoachResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoachResponse>> UpdateCoach(
        Guid linkId, [FromBody] UpdateCoachRequest request, CancellationToken ct)
    {
        var link = await _invitations.ChangeSharingAsync(
            User.GetUserId(), linkId, request.ShareReflections, request.SharePractice, ct);
        return link is null ? NotFound() : Ok(ToResponse(link));
    }

    // DELETE /me/coaches/{linkId}
    //
    // Removes a coach: their access ends with their next request (#189).
    [HttpDelete("/me/coaches/{linkId:guid}", Name = "removeCoach")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCoach(Guid linkId, CancellationToken ct) =>
        await _invitations.RemoveCoachAsync(User.GetUserId(), linkId, ct) ? NoContent() : NotFound();

    // POST /me/coach-invitations
    //
    // An open invitation to the same address is sent again, with a new link (resent: true).
    [HttpPost("/me/coach-invitations", Name = "inviteCoach")]
    [ProducesResponseType<CoachInvitationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CoachInvitationResponse>> Invite(
        [FromBody] CreateCoachInvitationRequest request, CancellationToken ct)
    {
        var (invitation, resent, problem) = await _invitations.InviteAsync(
            User.GetUserId(), request.Email, request.ShareReflections ?? true, request.SharePractice ?? true, ct);

        return problem switch
        {
            CoachInvitationProblem.OwnAddress =>
                this.FieldError(nameof(request.Email), "You can't invite yourself as your coach."),
            CoachInvitationProblem.AlreadyYourCoach =>
                this.FieldError(nameof(request.Email), "This person is already your coach."),
            CoachInvitationProblem.TooManyCoaches =>
                this.FieldError(nameof(request.Email), "You can have at most 3 coaches, including invitations not yet accepted."),
            CoachInvitationProblem.DailyLimit =>
                Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many invitations today. Try again tomorrow."),
            _ => Ok(ToResponse(invitation!, resent)),
        };
    }

    // DELETE /me/coach-invitations/{id}
    [HttpDelete("/me/coach-invitations/{id:guid}", Name = "cancelCoachInvitation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        await _invitations.CancelAsync(User.GetUserId(), id, ct) ? NoContent() : NotFound();

    // POST /coach-invitations/preview
    //
    // Who invites and what they share, under the same rules as accepting. A POST, so the
    // token stays out of URLs and request logs.
    [HttpPost("/coach-invitations/preview", Name = "previewCoachInvitation")]
    [ProducesResponseType<CoachInvitationPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoachInvitationPreviewResponse>> Preview(
        [FromBody] CoachInvitationTokenRequest request, CancellationToken ct)
    {
        var (invitation, golfer, problem) = await _invitations.PreviewAsync(User.GetUserId(), request.Token, ct);
        if (problem is { } p) return ProblemFor(p);

        return Ok(new CoachInvitationPreviewResponse
        {
            GolferDisplayName = CoachInvitationService.DisplayNameOf(golfer!),
            ShareReflections = invitation!.ShareReflections,
            SharePractice = invitation.SharePractice,
            ExpiresAt = invitation.ExpiresAt,
        });
    }

    // POST /coach-invitations/accept
    //
    // Unknown, used, cancelled and expired links all answer 404. A signed-in account
    // without the invited address answers 403, so the web app can say to sign in with it.
    [HttpPost("/coach-invitations/accept", Name = "acceptCoachInvitation")]
    [ProducesResponseType<AcceptedCoachInvitationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AcceptedCoachInvitationResponse>> Accept(
        [FromBody] CoachInvitationTokenRequest request, CancellationToken ct)
    {
        var (link, golfer, problem) = await _invitations.AcceptAsync(User.GetUserId(), request.Token, ct);
        if (problem is { } p) return ProblemFor(p);

        return Ok(new AcceptedCoachInvitationResponse
        {
            CoachLinkId = link!.CoachLinkId,
            GolferId = golfer!.UserId,
            GolferDisplayName = CoachInvitationService.DisplayNameOf(golfer),
            ShareReflections = link.ShareReflections,
            SharePractice = link.SharePractice,
        });
    }

    // POST /coach-invitations/decline
    [HttpPost("/coach-invitations/decline", Name = "declineCoachInvitation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Decline([FromBody] CoachInvitationTokenRequest request, CancellationToken ct)
    {
        var problem = await _invitations.DeclineAsync(User.GetUserId(), request.Token, ct);
        return problem is { } p ? ProblemFor(p) : NoContent();
    }

    private ActionResult ProblemFor(CoachInvitationProblem problem) => problem switch
    {
        CoachInvitationProblem.WrongAccount => Problem(statusCode: StatusCodes.Status403Forbidden,
            title: "This invitation is for another email address. Sign in with the address it was sent to."),
        CoachInvitationProblem.OwnAddress => Problem(statusCode: StatusCodes.Status403Forbidden,
            title: "You can't accept your own invitation."),
        CoachInvitationProblem.TooManyCoaches => Problem(statusCode: StatusCodes.Status409Conflict,
            title: "This golfer already has the most coaches they can have."),
        _ => Problem(statusCode: StatusCodes.Status404NotFound,
            title: "This invitation has expired, or was already used or cancelled."),
    };

    private static CoachResponse ToResponse(CoachLink link) => new()
    {
        CoachLinkId = link.CoachLinkId,
        DisplayName = CoachInvitationService.DisplayNameOf(link.Coach),
        ShareReflections = link.ShareReflections,
        SharePractice = link.SharePractice,
        CreatedAt = link.CreatedAt,
    };

    private static CoachInvitationResponse ToResponse(CoachInvitation invitation, bool resent) => new()
    {
        CoachInvitationId = invitation.CoachInvitationId,
        Email = invitation.Email,
        ShareReflections = invitation.ShareReflections,
        SharePractice = invitation.SharePractice,
        CreatedAt = invitation.CreatedAt,
        ExpiresAt = invitation.ExpiresAt,
        Resent = resent,
    };
}
