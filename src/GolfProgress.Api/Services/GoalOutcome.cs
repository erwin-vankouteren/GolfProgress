using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;

namespace GolfProgress.Api.Services;

/// <summary>The stat a goal's focus area is measured by (#234).</summary>
public enum GoalStat { PuttsPer18, PenaltiesPer18, FairwaysPer18, ScoreSpread }

/// <summary>What "Did it work?" says.</summary>
public enum GoalVerdict
{
    Improved,
    Worse,
    Same,
    /// <summary>Too few rounds with the stat, before or during: no verdict, as everywhere in the app.</summary>
    CantTellYet,
    /// <summary>The goal has no focus area to measure.</summary>
    NotMeasured,
}

/// <summary>A goal's focus stat before it started, and during it (plus two weeks).</summary>
public sealed record GoalOutcomeResult(
    GoalStat? Stat,
    bool HigherIsBetter,
    decimal? Before,
    decimal? During,
    decimal? Change,
    int RoundsBefore,
    int RoundsDuring,
    ConfidenceLevel Confidence,
    GoalVerdict Verdict);

/// <summary>
/// "Did it work?" (#234, docs/coach-access.md → "Practice goals"): the focus area's stat in
/// the rounds during the goal and the two weeks after, against the 5 rounds before it started.
/// Pure, so it's tested on its own. A round that didn't track the stat is left out rather
/// than counted as zero, as in the fitness score: fewer rounds lower the confidence instead.
/// </summary>
public static class GoalOutcome
{
    /// <summary>Rounds after the goal's end that still count: practice takes a while to show.</summary>
    public const int DaysAfter = 14;

    /// <summary>Rounds before the start it's compared with.</summary>
    public const int RoundsBeforeCount = 5;

    /// <summary>A change smaller than this, per 18 holes, reads as the same.</summary>
    public const decimal SameWithin = 0.5m;

    public static GoalStat? StatFor(GoalFocusArea? area) => area switch
    {
        GoalFocusArea.Putting => GoalStat.PuttsPer18,
        GoalFocusArea.Penalties => GoalStat.PenaltiesPer18,
        GoalFocusArea.TeeShots => GoalStat.FairwaysPer18,
        GoalFocusArea.Consistency => GoalStat.ScoreSpread,
        _ => null,
    };

    /// <summary>
    /// The outcome of a goal from [startsOn] to [endsOn], given the golfer's rounds (any order).
    /// </summary>
    public static GoalOutcomeResult Compute(
        GoalFocusArea? area, DateOnly startsOn, DateOnly endsOn, IEnumerable<Round> rounds)
    {
        if (StatFor(area) is not { } stat)
            return new GoalOutcomeResult(null, false, null, null, null, 0, 0, ConfidenceLevel.Low, GoalVerdict.NotMeasured);

        var all = rounds.ToList();
        var last = endsOn.AddDays(DaysAfter);
        var before = all.Where(r => r.PlayedAt < startsOn && Tracks(stat, r))
            .OrderByDescending(r => r.PlayedAt).ThenByDescending(r => r.CreatedAt)
            .Take(RoundsBeforeCount).ToList();
        var during = all.Where(r => r.PlayedAt >= startsOn && r.PlayedAt <= last && Tracks(stat, r)).ToList();

        var beforeValue = Value(stat, before);
        var duringValue = Value(stat, during);
        var change = beforeValue is { } b && duringValue is { } d ? Math.Round(d - b, 1) : (decimal?)null;
        var higherIsBetter = stat == GoalStat.FairwaysPer18;
        var confidence = ConfidenceOf(before.Count, during.Count);

        var verdict = confidence == ConfidenceLevel.Low || change is null
            ? GoalVerdict.CantTellYet
            : Math.Abs(change.Value) < SameWithin
                ? GoalVerdict.Same
                : (change.Value > 0) == higherIsBetter ? GoalVerdict.Improved : GoalVerdict.Worse;

        return new GoalOutcomeResult(
            stat, higherIsBetter,
            beforeValue is { } bv ? Math.Round(bv, 1) : null,
            duringValue is { } dv ? Math.Round(dv, 1) : null,
            change, before.Count, during.Count, confidence, verdict);
    }

    /// <summary>
    /// Low below 2 rounds on either side (a spread needs two); High with all 5 before and at
    /// least 4 during; Medium in between.
    /// </summary>
    internal static ConfidenceLevel ConfidenceOf(int before, int during) =>
        before < 2 || during < 2 ? ConfidenceLevel.Low
        : before >= RoundsBeforeCount && during >= 4 ? ConfidenceLevel.High
        : ConfidenceLevel.Medium;

    private static bool Tracks(GoalStat stat, Round r) => stat switch
    {
        GoalStat.PuttsPer18 => r.TotalPutts.HasValue,
        GoalStat.PenaltiesPer18 => r.Penalties.HasValue,
        GoalStat.FairwaysPer18 => r.FairwaysHit.HasValue,
        _ => true,
    };

    private static decimal? Value(GoalStat stat, List<Round> rounds)
    {
        if (rounds.Count == 0) return null;
        return stat switch
        {
            GoalStat.PuttsPer18 => rounds.Average(r => HoleScaling.Per18(r.TotalPutts, r)!.Value),
            GoalStat.PenaltiesPer18 => rounds.Average(r => HoleScaling.Per18(r.Penalties, r)!.Value),
            GoalStat.FairwaysPer18 => rounds.Average(r => HoleScaling.Per18(r.FairwaysHit, r)!.Value),
            _ => rounds.Count < 2 ? null : ProgressComputationService.StdDev(rounds.Select(ProgressComputationService.StrokesPer18)),
        };
    }
}
