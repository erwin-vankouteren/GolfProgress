using System.Security.Claims;

namespace GolfProgress.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Missing user id claim.");
        return Guid.Parse(value);
    }
}