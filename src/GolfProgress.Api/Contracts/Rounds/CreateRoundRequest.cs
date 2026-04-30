namespace GolfProgress.Api.Contracts.Rounds
{

    public sealed class CreateRoundRequest
    {
        public Guid UserId { get; init; }                 // required
        public string PlayedAt { get; init; } = "";       // "2026-04-20"
        public short Holes { get; init; } = 18;           // 9 or 18

        public string? CourseName { get; init; }
        public string? TeeName { get; init; }

        public short TotalStrokes { get; init; }          // required

        // Optional stats (nullable)
        public short? TotalPutts { get; init; }
        public short? Penalties { get; init; }
        public short? FairwaysHit { get; init; }
        public short? Gir { get; init; }
        public short? ThreePutts { get; init; }
        public short? BlowUpHoles { get; init; }

        public string Source { get; init; } = "Manual";   // Manual|Import|Coach|Other
        public string? Notes { get; init; }
    }

}
