using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Coaching;

/// <summary>Accept or decline an invitation (#184), with the token from its email link.</summary>
public sealed class CoachInvitationTokenRequest
{
    [Required, MaxLength(200)] public string Token { get; init; } = "";
}
