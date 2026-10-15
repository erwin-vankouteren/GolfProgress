using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
namespace GolfProgress.Api.Contracts.PracticeSessions;

public sealed class PracticeSessionResponse
{
    public Guid PracticeSessionId { get; init; }
    public Guid UserId { get; init; }

    public string PracticedAt { get; init; } = "";   // "2026-04-20"
    public short? DurationMinutes { get; init; }
    [StringEnum(typeof(Enums.SessionType), Open = true)]
    public string SessionType { get; init; } = "";
    public string? VenueName { get; init; }
    public List<string> Tags { get; init; } = new();
    public string? PrimaryGoal { get; init; }
    public string? Notes { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
