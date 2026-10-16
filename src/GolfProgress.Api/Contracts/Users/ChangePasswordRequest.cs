using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Users;

/// <summary>POST /me/password (#83).</summary>
public sealed class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; init; } = "";

    /// <summary>Must meet the password policy: 8 to 128 characters, not the email address.</summary>
    [Required] public string NewPassword { get; init; } = "";
}
