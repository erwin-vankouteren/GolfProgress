using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
using GolfProgress.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.PracticeSessions;

public sealed class CreatePracticeSessionRequest : IValidatableObject
{
    [Required] public string PracticedAt { get; init; } = "";   // "2026-04-20"

    [Range(1, 1440)]
    public short? DurationMinutes { get; init; }

    // SessionType and Tags are nullable so that null means the same as leaving them out
    // (Range, no tags): clients that write out every field send null for an empty one.
    [StringEnum(typeof(Enums.SessionType))]
    public string? SessionType { get; init; } = nameof(Domain.Enums.SessionType.Range); // Range|Putting|ShortGame|Simulator|Lesson|Other

    [MaxLength(200)]
    public string? VenueName { get; init; }

    [MaxLength(20)]
    public List<string>? Tags { get; init; } = new();

    /// <summary>The session type to store; Validate has checked it parses.</summary>
    internal Domain.Enums.SessionType ParsedSessionType =>
        SessionType is null ? Domain.Enums.SessionType.Range : Enum.Parse<Domain.Enums.SessionType>(SessionType, ignoreCase: true);

    internal IEnumerable<string> TagsOrNone => Tags ?? Enumerable.Empty<string>();

    [MaxLength(200)]
    public string? PrimaryGoal { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsoDate.TryParse(PracticedAt, out _))
            yield return new ValidationResult(
                "PracticedAt must be ISO date format: yyyy-MM-dd", new[] { nameof(PracticedAt) });

        if (SessionType is not null && !Enum.TryParse<SessionType>(SessionType, ignoreCase: true, out _))
            yield return new ValidationResult(
                $"SessionType must be one of: {string.Join(", ", Enum.GetNames<SessionType>())}.",
                new[] { nameof(SessionType) });

        if (TagsOrNone.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 50))
            yield return new ValidationResult(
                "Tags must be non-empty and at most 50 characters each.", new[] { nameof(Tags) });
    }
}
