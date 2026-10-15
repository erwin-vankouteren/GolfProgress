using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required] public string Email { get; init; } = "";
    [Required] public string DisplayName { get; init; } = "";
    [Required] public string Password { get; init; } = "";
}
