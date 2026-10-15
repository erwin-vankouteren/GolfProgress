using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Auth;

public sealed class RefreshRequest
{
    [Required] public string RefreshToken { get; init; } = "";
}
