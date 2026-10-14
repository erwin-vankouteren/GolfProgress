using GolfProgress.Api.Services;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;

namespace GolfProgress.Api.Tests;

/// <summary>
/// Pins the pure scoring functions in <see cref="ProgressComputationService"/>.
/// These encode business rules that are otherwise undocumented, so that changing
/// a threshold becomes a deliberate act with a failing test rather than silent drift.
/// </summary>
public sealed class ScoringTests
{
    private static Round RoundWith(
        int totalStrokes = 90, short? penalties = 0, short? blowUps = 0,
        short? putts = null, short? fairways = null, short? gir = null, short? threePutts = null) => new()
        {
            RoundId = Guid.NewGuid(),
            TotalStrokes = (short)totalStrokes,
            Penalties = penalties,
            BlowUpHoles = blowUps,
            TotalPutts = putts,
            FairwaysHit = fairways,
            Gir = gir,
            ThreePutts = threePutts
        };

    private static UserBaseline BaselineWith(decimal? avgScore = null, decimal? stdDev = null) => new()
        { AvgScore = avgScore, ScoreStdDev = stdDev };

    // ---------- Execution

    [Fact]
    public void Execution_WithNoBaseline_IsNeutral12()
    {
        var (execution, _, _, _) = ProgressComputationService.ComputePillars(
            RoundWith(totalStrokes: 90), BaselineWith(avgScore: null));

        Assert.Equal(12, execution);
    }

    [Theory]
    // strokes, baselineAvg, expected: 12 - round(strokes - avg), clamped 0..25
    [InlineData(90, 90, 12)]  // on baseline
    [InlineData(85, 90, 17)]  // 5 better
    [InlineData(95, 90, 7)]   // 5 worse
    [InlineData(70, 90, 25)]  // 20 better, clamps at 25
    [InlineData(110, 90, 0)]  // 20 worse, clamps at 0
    public void Execution_ScalesInverselyWithStrokesVsBaseline(int strokes, int baselineAvg, int expected)
    {
        var (execution, _, _, _) = ProgressComputationService.ComputePillars(
            RoundWith(totalStrokes: strokes), BaselineWith(avgScore: baselineAvg));

        Assert.Equal(expected, execution);
    }

    // ---------- Damage control

    [Theory]
    // 25 - (penalties * 4) - (blowUps * 2), clamped 0..25
    [InlineData(0, 0, 25)]
    [InlineData(1, 0, 21)]
    [InlineData(2, 0, 17)]
    [InlineData(0, 1, 23)]
    [InlineData(2, 3, 11)]
    [InlineData(7, 0, 0)]   // clamps rather than going negative
    [InlineData(9, 9, 0)]
    public void DamageControl_PenalisesPenaltiesFourfoldAndBlowUpsTwofold(
        short penalties, short blowUps, int expected)
    {
        var (_, damage, _, _) = ProgressComputationService.ComputePillars(
            RoundWith(penalties: penalties, blowUps: blowUps), BaselineWith());

        Assert.Equal(expected, damage);
    }

    /// <summary>
    /// Documents CURRENT behaviour, which is wrong. See issue #27.
    /// Missing penalty/blow-up data is coerced to zero, so a round that tracked
    /// nothing scores full marks while an honest round reporting 2 penalties
    /// scores 17. Update this test when #27 is fixed.
    /// </summary>
    [Fact]
    public void DamageControl_WithNoData_CurrentlyScoresFullMarks_Issue27()
    {
        var (_, withoutData, _, _) = ProgressComputationService.ComputePillars(
            RoundWith(penalties: null, blowUps: null), BaselineWith());

        var (_, honestRound, _, _) = ProgressComputationService.ComputePillars(
            RoundWith(penalties: 2, blowUps: 0), BaselineWith());

        Assert.Equal(25, withoutData);
        Assert.Equal(17, honestRound);
        Assert.True(withoutData > honestRound, "tracking nothing currently outscores honest tracking");
    }

    // ---------- Consistency

    [Fact]
    public void Consistency_WithNoStdDev_IsNeutral12()
    {
        var (_, _, consistency, _) = ProgressComputationService.ComputePillars(
            RoundWith(totalStrokes: 90), BaselineWith(avgScore: 90, stdDev: null));

        Assert.Equal(12, consistency);
    }

    [Fact]
    public void Consistency_WithZeroStdDev_IsNeutral12()
    {
        // A zero std dev (identical prior rounds) is explicitly excluded by the
        // guard, so the within/outside branch never runs and neutral stands.
        var (_, _, consistency, _) = ProgressComputationService.ComputePillars(
            RoundWith(totalStrokes: 90), BaselineWith(avgScore: 90, stdDev: 0));

        Assert.Equal(12, consistency);
    }

    [Theory]
    [InlineData(90, 18)]   // dead on average, within 1 sd
    [InlineData(93, 18)]   // exactly 1 sd away, boundary is inclusive
    [InlineData(87, 18)]   // 1 sd below
    [InlineData(94, 8)]    // outside 1 sd
    [InlineData(80, 8)]    // well below average, still counted as outside
    public void Consistency_RewardsStayingWithinOneStandardDeviation(int strokes, int expected)
    {
        var (_, _, consistency, _) = ProgressComputationService.ComputePillars(
            RoundWith(totalStrokes: strokes), BaselineWith(avgScore: 90, stdDev: 3));

        // Note: the comparison is on absolute distance, so an unusually GOOD round
        // is penalised for inconsistency exactly as an unusually bad one is.
        // Arguably intended for a "consistency" pillar, but worth being explicit.
        Assert.Equal(expected, consistency);
    }

    // ---------- Habits and total

    [Fact]
    public void Habits_IsAlwaysTheSamePlaceholderValue()
    {
        var (_, _, _, greatRound) = ProgressComputationService.ComputePillars(
            RoundWith(totalStrokes: 70, penalties: 0, blowUps: 0), BaselineWith(avgScore: 90, stdDev: 3));

        var (_, _, _, poorRound) = ProgressComputationService.ComputePillars(
            RoundWith(totalStrokes: 120, penalties: 6, blowUps: 8), BaselineWith(avgScore: 90, stdDev: 3));

        Assert.Equal(10, greatRound);
        Assert.Equal(10, poorRound);
    }

    /// <summary>
    /// Documents the real ceiling on ScoreTotal. See issue #28.
    ///
    /// ScoreTotal is clamped to 0..100 and FitnessScore documents each pillar as
    /// 0..25, but the maximum attainable total is 78:
    ///   execution 25 + damage 25 + consistency 18 + habits 10
    /// because Consistency only ever returns 8, 12 or 18, and Habits is fixed at 10.
    ///
    /// Reaching even 78 requires a baseline std dev of at least 13, since Execution
    /// peaks when the round is far below the baseline average while Consistency
    /// requires it to stay within one std dev of it. Those two pull against each
    /// other, so a narrow baseline lowers the ceiling further.
    /// </summary>
    [Fact]
    public void ScoreTotal_CeilingIs78NotOneHundred_Issue28()
    {
        // Best case: 13 strokes under a baseline whose std dev is also 13, so
        // Execution maxes out while the round still counts as consistent.
        var (execution, damage, consistency, habits) = ProgressComputationService.ComputePillars(
            RoundWith(totalStrokes: 77, penalties: 0, blowUps: 0),
            BaselineWith(avgScore: 90, stdDev: 13));

        Assert.Equal(25, execution);
        Assert.Equal(25, damage);
        Assert.Equal(18, consistency);
        Assert.Equal(10, habits);
        Assert.Equal(78, execution + damage + consistency + habits);
    }

    [Fact]
    public void Consistency_NeverExceeds18_SoThePillarCannotReachItsDocumented25()
    {
        // Sweep a wide range of rounds against a fixed baseline; the pillar only
        // ever takes the three discrete values 8, 12 and 18.
        var observed = Enumerable.Range(60, 60)
            .Select(strokes => ProgressComputationService.ComputePillars(
                RoundWith(totalStrokes: strokes), BaselineWith(avgScore: 90, stdDev: 3)).consistency)
            .Distinct()
            .OrderBy(v => v)
            .ToArray();

        Assert.Equal(new short[] { 8, 18 }, observed);
        Assert.True(observed.Max() < 25);
    }

    // ---------- Completeness

    [Fact]
    public void Completeness_WithNoRounds_IsZero()
        => Assert.Equal(0m, ProgressComputationService.ComputeCompleteness(new List<Round>()));

    [Fact]
    public void Completeness_CountsSixTrackedMetricsPerRound()
    {
        var allSix = new List<Round>
        {
            RoundWith(penalties: 1, blowUps: 1, putts: 30, fairways: 7, gir: 6, threePutts: 1)
        };
        Assert.Equal(1m, ProgressComputationService.ComputeCompleteness(allSix));

        var threeOfSix = new List<Round>
        {
            RoundWith(penalties: 1, blowUps: 1, putts: 30, fairways: null, gir: null, threePutts: null)
        };
        Assert.Equal(0.5m, ProgressComputationService.ComputeCompleteness(threeOfSix));

        var nothingTracked = new List<Round>
        {
            RoundWith(penalties: null, blowUps: null)
        };
        Assert.Equal(0m, ProgressComputationService.ComputeCompleteness(nothingTracked));
    }

    [Fact]
    public void Completeness_AveragesAcrossRounds()
    {
        // One fully tracked round and one untracked round averages to 0.5.
        var mixed = new List<Round>
        {
            RoundWith(penalties: 1, blowUps: 1, putts: 30, fairways: 7, gir: 6, threePutts: 1),
            RoundWith(penalties: null, blowUps: null)
        };

        Assert.Equal(0.5m, ProgressComputationService.ComputeCompleteness(mixed));
    }

    // ---------- Confidence

    [Theory]
    [InlineData(0, 0.0, ConfidenceLevel.Low)]
    [InlineData(3, 1.0, ConfidenceLevel.Low)]      // too few samples whatever the completeness
    [InlineData(4, 0.0, ConfidenceLevel.Medium)]   // sample count alone promotes to Medium
    [InlineData(7, 1.0, ConfidenceLevel.Medium)]
    [InlineData(8, 0.49, ConfidenceLevel.Medium)]  // enough samples, too sparse for High
    [InlineData(8, 0.5, ConfidenceLevel.High)]     // both thresholds met, boundary inclusive
    [InlineData(20, 1.0, ConfidenceLevel.High)]
    public void Confidence_RequiresBothSampleCountAndCompleteness(
        int sampleCount, double completeness, ConfidenceLevel expected)
        => Assert.Equal(expected,
            ProgressComputationService.ConfidenceFrom(sampleCount, (decimal)completeness));

    // ---------- Statistics helpers

    [Fact]
    public void StdDev_ReturnsZeroBelowTwoValues()
    {
        Assert.Equal(0m, ProgressComputationService.StdDev(Array.Empty<decimal>()));
        Assert.Equal(0m, ProgressComputationService.StdDev(new decimal[] { 90 }));
    }

    [Fact]
    public void StdDev_UsesTheSampleFormula()
    {
        // 86,88,90,92,94: mean 90, sum of squares 40, n-1 = 4, sqrt(10) = 3.1623
        var sd = ProgressComputationService.StdDev(new decimal[] { 86, 88, 90, 92, 94 });

        Assert.InRange(sd, 3.16m, 3.17m);
    }

    [Fact]
    public void AvgNullable_IgnoresNullsAndReturnsNullWhenAllMissing()
    {
        Assert.Null(ProgressComputationService.AvgNullable(new short?[] { null, null }));
        Assert.Equal(3m, ProgressComputationService.AvgNullable(new short?[] { 2, null, 4 }));
        Assert.Equal(2m, ProgressComputationService.AvgNullable(new short?[] { 2 }));
    }
}
