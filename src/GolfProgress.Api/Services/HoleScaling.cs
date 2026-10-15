using GolfProgress.Domain.Entities;

namespace GolfProgress.Api.Services;

/// <summary>
/// Puts per-round counts on an 18-hole footing, so 9- and 18-hole rounds can be
/// averaged together and compared against the same baseline (#5, #35).
///
/// Every per-round figure the baseline holds -- strokes, putts, penalties and so
/// on -- is an 18-hole equivalent. Anything compared against it must be scaled
/// the same way first.
///
/// Fairways and greens in regulation scale only approximately, since how many
/// fairways a round offers depends on the course's par-3 holes.
/// </summary>
internal static class HoleScaling
{
    public const short StandardHoles = 18;

    public static decimal Per18(decimal value, short holes) => value * StandardHoles / holes;

    public static decimal? Per18(short? value, short holes) =>
        value.HasValue ? Per18((decimal)value.Value, holes) : null;

    public static decimal? Per18(short? value, Round round) => Per18(value, round.Holes);
}
