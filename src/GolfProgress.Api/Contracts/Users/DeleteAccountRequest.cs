using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Users;

/// <summary>DELETE /me (#84): the user confirms with their password.</summary>
public sealed class DeleteAccountRequest
{
    [Required] public string Password { get; init; } = "";
}
