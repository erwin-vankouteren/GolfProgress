using GolfProgress.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace GolfProgress.Api.Security;

/// <summary>
/// On an endpoint that logs or changes the golfer's own golf (#309, docs/payments.md →
/// "API"): in read-only, after the trial without a subscription, it answers 402 with
/// problem type <see cref="ProblemType"/> instead of running. Reading, the profile, export
/// and deleting the account never carry it, so nothing entered is held back.
/// It also adds the 402 to the endpoint's OpenAPI description.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresSubscriptionAttribute : Attribute, IFilterFactory, IApiResponseMetadataProvider
{
    /// <summary>The problem's type, for clients that want more than the status.</summary>
    public const string ProblemType = "https://golfprogress.app/problems/subscription-required";

    /// <summary>The type when it's Coaching that's missing (#312).</summary>
    public const string CoachingProblemType = "https://golfprogress.app/problems/coaching-required";

    /// <summary>
    /// Needs Coaching, in the trial or as an add-on (#312), rather than only being able to log:
    /// for the golfer's side of coaching, such as inviting a coach.
    /// </summary>
    public bool Coaching { get; init; }

    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) => new Filter(Coaching);

    public Type Type => typeof(ProblemDetails);

    public int StatusCode => StatusCodes.Status402PaymentRequired;

    public void SetContentTypes(MediaTypeCollection contentTypes)
    {
    }

    private sealed class Filter(bool coaching) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            var plan = await http.RequestServices.GetRequiredService<PlanService>()
                .ForUserAsync(http.User.GetUserId(), http.RequestAborted);

            // No such user: the endpoint answers that itself.
            if (plan is not null && (coaching ? !plan.Coaching : !plan.CanLog))
            {
                var problem = http.RequestServices.GetRequiredService<ProblemDetailsFactory>().CreateProblemDetails(
                    http,
                    StatusCodes.Status402PaymentRequired,
                    title: coaching
                        ? "Add Coaching to your subscription to work with a coach."
                        : "Your free trial has ended. Subscribe to log and change your golf.",
                    type: coaching ? CoachingProblemType : ProblemType);
                context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status402PaymentRequired };
                return;
            }

            await next();
        }
    }
}
