using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Auth;

/// <summary>POST /auth/password-reset (#86): where to send the reset link.</summary>
public sealed class PasswordResetRequest
{
    [Required, EmailAddress, MaxLength(320)] public string Email { get; init; } = "";
}

/// <summary>POST /auth/password-reset/confirm (#86): the link's token and the new password.</summary>
public sealed class ConfirmPasswordResetRequest
{
    [Required] public string Token { get; init; } = "";

    /// <summary>Must meet the password policy: 8 to 128 characters, not the email address.</summary>
    [Required] public string NewPassword { get; init; } = "";
}
