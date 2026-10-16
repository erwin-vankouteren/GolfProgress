using GolfProgress.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required] public string Email { get; init; } = "";

    // Bounded like PATCH /me, so a name set at registration can always be saved again.
    [Required, MaxLength(User.DisplayNameMaxLength)] public string DisplayName { get; init; } = "";

    [Required] public string Password { get; init; } = "";
}
