using GolfProgress.Api.OpenApi;
using Enums = GolfProgress.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace GolfProgress.Api.Contracts.Rounds
{

    /// <summary>
    /// Validated by [ApiController] before the action runs: simple ranges through the
    /// attributes, and bounds that depend on other fields in <see cref="Validate"/> (#11).
    /// </summary>
    public sealed class CreateRoundRequest : IValidatableObject
    {
        [Required] public string PlayedAt { get; init; } = "";   // "2026-04-20"
        public short Holes { get; init; } = 18;           // 9 or 18

        public string? CourseName { get; init; }
        public string? TeeName { get; init; }

        [Required, Range(1, short.MaxValue, ErrorMessage = "TotalStrokes must be > 0.")]
        public short TotalStrokes { get; init; }          // required

        // Optional stats (nullable)
        [Range(0, short.MaxValue)] public short? TotalPutts { get; init; }
        [Range(0, short.MaxValue)] public short? Penalties { get; init; }
        [Range(0, short.MaxValue)] public short? FairwaysHit { get; init; }
        [Range(0, short.MaxValue)] public short? Gir { get; init; }
        [Range(0, short.MaxValue)] public short? ThreePutts { get; init; }
        [Range(0, short.MaxValue)] public short? BlowUpHoles { get; init; }

        [StringEnum(typeof(Enums.RoundSource))]
        public string Source { get; init; } = "Manual";   // Manual|Import|Coach|Other
        public string? Notes { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Holes is not (9 or 18))
                yield return new ValidationResult("Holes must be 9 or 18.", new[] { nameof(Holes) });

            // Counted at most once per hole, so they cannot exceed the hole count:
            // 18 fairways hit on a 9-hole round is not a real round.
            foreach (var (name, value) in new[]
                     {
                         (nameof(FairwaysHit), FairwaysHit),
                         (nameof(Gir), Gir),
                         (nameof(ThreePutts), ThreePutts),
                         (nameof(BlowUpHoles), BlowUpHoles)
                     })
            {
                if (value > Holes)
                    yield return new ValidationResult(
                        $"{name} cannot exceed the number of holes ({Holes}).", new[] { name });
            }

            // Putts and penalty strokes are part of the total, so cannot exceed it.
            if (TotalPutts > TotalStrokes)
                yield return new ValidationResult(
                    "TotalPutts cannot exceed TotalStrokes.", new[] { nameof(TotalPutts) });

            if (Penalties > TotalStrokes)
                yield return new ValidationResult(
                    "Penalties cannot exceed TotalStrokes.", new[] { nameof(Penalties) });
        }
    }

}
