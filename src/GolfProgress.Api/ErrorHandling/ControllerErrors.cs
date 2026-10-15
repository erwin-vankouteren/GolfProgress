using Microsoft.AspNetCore.Mvc;

namespace GolfProgress.Api.ErrorHandling;

/// <summary>
/// Error responses for checks a controller makes itself, in the same ProblemDetails
/// shape as [ApiController]'s automatic validation, so clients handle every 400 one
/// way: show each message next to its field (#62).
/// </summary>
public static class ControllerErrors
{
    /// <summary>A 400 ValidationProblemDetails with one error for <paramref name="field"/>.</summary>
    public static ActionResult FieldError(this ControllerBase controller, string field, string message)
    {
        controller.ModelState.AddModelError(field, message);
        return controller.ValidationProblem(controller.ModelState);
    }
}
