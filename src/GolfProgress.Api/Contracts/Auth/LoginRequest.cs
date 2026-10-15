using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Auth;

public sealed class LoginRequest
{
    [Required] public string Email { get; init; } = "";
    [Required] public string Password { get; init; } = "";
}
