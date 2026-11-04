using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Coaching;

/// <summary>
/// Invite a coach (#184). Rounds and progress are always shared; reflections and notes, and
/// practice, are shared unless switched off (docs/coach-access.md → "What a coach sees").
/// </summary>
public sealed class CreateCoachInvitationRequest
{
    [Required, EmailAddress, MaxLength(256)] public string Email { get; init; } = "";

    /// <summary>Round reflections and the notes on rounds and practice sessions. Default true.</summary>
    public bool? ShareReflections { get; init; }

    /// <summary>Practice sessions and practice goals. Default true.</summary>
    public bool? SharePractice { get; init; }
}
