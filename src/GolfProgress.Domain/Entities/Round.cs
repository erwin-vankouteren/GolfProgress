namespace GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
public class Round
{
    public Guid RoundId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public DateOnly PlayedAt { get; set; }
    public short Holes { get; set; } = 18;
    public string? CourseName { get; set; }
    public string? TeeName { get; set; }

    public short TotalStrokes { get; set; }

    public short? TotalPutts { get; set; }
    public short? Penalties { get; set; }
    public short? FairwaysHit { get; set; }
    public short? Gir { get; set; }
    public short? ThreePutts { get; set; }
    public short? BlowUpHoles { get; set; }

    public RoundSource Source { get; set; } = RoundSource.Manual;
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<MicroWin> MicroWins { get; set; } = new List<MicroWin>();
    public ICollection<Reflection> Reflections { get; set; } = new List<Reflection>();
    public FitnessScore? FitnessScore { get; set; }
}
