namespace GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;

public class User
{
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }

    /// <summary>Longest display name a user can set, at registration or with PATCH /me.</summary>
    public const int DisplayNameMaxLength = 100;

    public string? DisplayName { get; set; }
    public string? Timezone { get; set; }
    public Units Units { get; set; } = Units.Metric;

    public decimal? HandicapIndex { get; set; }
    public int? Typical18Score { get; set; }

    // Stored as JSON in SQL Server (nvarchar(max))
    public List<string> GoalFocus { get; set; } = new();

    public TrackingMode TrackingMode { get; set; } = TrackingMode.Minimal;

    /// <summary>The language the user chose in their profile; null follows the device.</summary>
    public AppLanguage? Language { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ICollection<Round> Rounds { get; set; } = new List<Round>();
    public ICollection<PracticeSession> PracticeSessions { get; set; } = new List<PracticeSession>();
    public ICollection<Reflection> Reflections { get; set; } = new List<Reflection>();
    public ICollection<FitnessScore> FitnessScores { get; set; } = new List<FitnessScore>();
    public ICollection<MicroWin> MicroWins { get; set; } = new List<MicroWin>();
    public ICollection<MonthlyReview> MonthlyReviews { get; set; } = new List<MonthlyReview>();
    public ICollection<EventLog> Events { get; set; } = new List<EventLog>();

    public UserBaseline? Baseline { get; set; }

}
