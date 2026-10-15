using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
namespace GolfProgress.Api.Contracts.Rounds
{
    public sealed class RoundResponse
    {
        public Guid RoundId { get; init; }
        public Guid UserId { get; init; }

        public string PlayedAt { get; init; } = "";
        public short Holes { get; init; }

        public string? CourseName { get; init; }
        public string? TeeName { get; init; }

        public short TotalStrokes { get; init; }
        public short? TotalPutts { get; init; }
        public short? Penalties { get; init; }
        public short? FairwaysHit { get; init; }
        public short? Gir { get; init; }
        public short? ThreePutts { get; init; }
        public short? BlowUpHoles { get; init; }

        [StringEnum(typeof(Enums.RoundSource))]
        public string Source { get; init; } = "Manual";
        public string? Notes { get; init; }

        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }

}
