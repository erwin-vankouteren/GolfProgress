using GolfProgress.Api.Contracts.Feedback;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Security;
using GolfProgress.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GolfProgress.Api.Controllers;

/// <summary>
/// Feedback threads, for the golfer and their coaches (#212, docs/coach-access.md →
/// "Feedback"). Without access to the item it's a 404, so a caller can't tell whether a
/// golfer, item or message exists.
/// </summary>
[Authorize]
[ApiController]
public sealed class FeedbackController : ControllerBase
{
    private readonly FeedbackService _feedback;

    public FeedbackController(FeedbackService feedback) => _feedback = feedback;

    // GET /feedback?golferId=…&targetType=Round&targetId=…
    [HttpGet("/feedback", Name = "getFeedbackThread")]
    [ProducesResponseType<List<FeedbackMessageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<FeedbackMessageResponse>>> Thread([FromQuery] FeedbackThreadQuery query, CancellationToken ct)
    {
        if (!FeedbackService.TryParseTarget(query.TargetType, out var type))
            return this.FieldError(nameof(query.TargetType), "targetType must be Round, Reflection or PracticeSession.");

        var thread = await _feedback.ThreadAsync(User.GetUserId(), query.GolferId!.Value, type, query.TargetId!.Value, ct);
        return thread is null ? NotFound() : Ok(thread);
    }

    // POST /feedback
    [HttpPost("/feedback", Name = "writeFeedback")]
    [ProducesResponseType<FeedbackMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<FeedbackMessageResponse>> Write([FromBody] CreateFeedbackRequest request, CancellationToken ct)
    {
        if (!FeedbackService.TryParseTarget(request.TargetType, out var type))
            return this.FieldError(nameof(request.TargetType), "targetType must be Round, Reflection or PracticeSession.");

        var (message, problem) = await _feedback.WriteAsync(
            User.GetUserId(), request.GolferId!.Value, type, request.TargetId!.Value, request.CoachId, request.Text, ct);
        return problem switch
        {
            FeedbackProblem.NotFound => NotFound(),
            FeedbackProblem.EmptyText => this.FieldError(nameof(request.Text), "Write something first."),
            FeedbackProblem.NoSuchCoach => this.FieldError(nameof(request.CoachId), "Choose a coach who can see this."),
            FeedbackProblem.HourlyLimit => Problem(
                statusCode: StatusCodes.Status429TooManyRequests, title: "Too many messages this hour. Try again later."),
            _ => Ok(message),
        };
    }

    // PATCH /feedback/{id}
    [HttpPatch("/feedback/{id:guid}", Name = "editFeedback")]
    [ProducesResponseType<FeedbackMessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackMessageResponse>> Edit(Guid id, [FromBody] UpdateFeedbackRequest request, CancellationToken ct)
    {
        var (message, problem) = await _feedback.EditAsync(User.GetUserId(), id, request.Text, ct);
        return problem switch
        {
            FeedbackProblem.NotFound => NotFound(),
            FeedbackProblem.EmptyText => this.FieldError(nameof(request.Text), "Write something first."),
            _ => Ok(message),
        };
    }

    // DELETE /feedback/{id}
    [HttpDelete("/feedback/{id:guid}", Name = "deleteFeedback")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await _feedback.DeleteAsync(User.GetUserId(), id, ct) ? NoContent() : NotFound();

    // POST /feedback/read
    [HttpPost("/feedback/read", Name = "markFeedbackRead")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead([FromBody] MarkFeedbackReadRequest request, CancellationToken ct)
    {
        if (!FeedbackService.TryParseTarget(request.TargetType, out var type))
            return this.FieldError(nameof(request.TargetType), "targetType must be Round, Reflection or PracticeSession.");

        return await _feedback.MarkReadAsync(User.GetUserId(), request.GolferId!.Value, type, request.TargetId!.Value, request.UpTo, ct)
            ? NoContent()
            : NotFound();
    }

    // GET /me/feedback/unread
    //
    // "New from your coach" on the dashboard (#220).
    [HttpGet("/me/feedback/unread", Name = "getUnreadFeedback")]
    [ProducesResponseType<UnreadFeedbackResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadFeedbackResponse>> Unread(CancellationToken ct) =>
        Ok(await _feedback.UnreadForGolferAsync(User.GetUserId(), ct));
}
