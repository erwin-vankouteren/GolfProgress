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

    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) => new Filter();

    public Type Type => typeof(ProblemDetails);

    public int StatusCode => StatusCodes.Status402PaymentRequired;

    public void SetContentTypes(MediaTypeCollection contentTypes)
    {
    }

    private sealed class Filter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            var plan = await http.RequestServices.GetRequiredService<PlanService>()
                .ForUserAsync(http.User.GetUserId(), http.RequestAborted);

            // No such user: the endpoint answers that itself.
            if (plan is { CanLog: false })
            {
                var problem = http.RequestServices.GetRequiredService<ProblemDetailsFactory>().CreateProblemDetails(
                    http,
                    StatusCodes.Status402PaymentRequired,
                    title: "Your free trial has ended. Subscribe to log and change your golf.",
                    type: ProblemType);
                context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status402PaymentRequired };
                return;
            }

            await next();
        }
    }
}
