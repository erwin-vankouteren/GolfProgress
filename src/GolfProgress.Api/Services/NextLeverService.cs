using GolfProgress.Domain.Entities;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Localization;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Services;

public sealed class NextLeverService
{
    private readonly GolfProgressDbContext _db;

    public NextLeverService(GolfProgressDbContext db) => _db = db;

    public async Task<(string leverKey, string title, string rec, string rationale, ConfidenceLevel confidence)> RecommendAsync(Guid roundId, Guid userId, CancellationToken ct)
    {
        var round = await _db.Rounds.AsNoTracking().FirstOrDefaultAsync(r => r.RoundId == roundId && r.UserId == userId, ct)
            ?? throw new NotFoundException($"Round {roundId} not found.");

        var baseline = await _db.UserBaselines.AsNoTracking().FirstOrDefaultAsync(b => b.UserId == round.UserId, ct);
        var confidence = baseline?.ConfidenceLevel ?? ConfidenceLevel.Low;
        var goalNames = await _db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.GoalFocus).FirstOrDefaultAsync(ct);
        var goals = (goalNames ?? new List<string>()).Select(MicroWinTexts.ParseFocus).OfType<GoalFocusArea>();

        // In the request's language (Accept-Language).
        var texts = ApiTexts.Current;
        (string key, string title, string rec, string rationale, ConfidenceLevel confidence) Answer(
            Candidate c, string rationale)
        {
            var (title, rec) = texts.Lever(c.Lever);
            return (c.Key, title, rec, rationale, confidence);
        }

        // Counts are scaled to 18 holes before use, both to compare against the
        // baseline (which holds 18-hole equivalents) and so the weights below mean
        // the same thing for a 9-hole round (#35). Lower is better for all three.
        var candidates = new List<Candidate>();
        if (HoleScaling.Per18(round.Penalties, round) is decimal penalties)
            candidates.Add(Candidate.For("penalties", LeverText.Penalties, penalties, baseline?.AvgPenalties, 3m, 1m));
        if (HoleScaling.Per18(round.ThreePutts, round) is decimal threePutts)
            candidates.Add(Candidate.For("three_putts", LeverText.ThreePutts, threePutts, baseline?.AvgThreePutts, 4m, 2m));
        if (HoleScaling.Per18(round.BlowUpHoles, round) is decimal blowUps)
            candidates.Add(Candidate.For("damage_control", LeverText.DamageControl, blowUps, baseline?.AvgBlowUpHoles, 2m, 1.5m));

        // If no stats at all
        if (candidates.Count == 0)
        {
            var (title, rec) = texts.Lever(LeverText.DataQuality);
            return ("data_quality", title, rec, texts.LeverRationale(LeverText.DataQuality, null), ConfidenceLevel.Low);
        }

        // The order is in docs/architecture.md → "Choosing the next focus" (#299).
        // 1. A stat far above the baseline, even when the golfer's goal is another.
        if (candidates.Where(c => c.Delta >= MuchWorse).MaxBy(c => c.Score) is { } muchWorse)
            return Answer(muchWorse, texts.LeverRationale(muchWorse.Lever, muchWorse.Delta));

        // 2. The golfer's goal area, if its stat occurred: worse, level, or better (keep going).
        foreach (var goal in goals)
        {
            if (candidates.FirstOrDefault(c => c.Lever == LeverFor(goal) && c.Count > 0) is not { } mine)
                continue;
            return Answer(mine, mine.Delta > 0m
                ? texts.LeverRationale(mine.Lever, mine.Delta)
                : texts.LeverGoalRationale(goal, improved: mine.Delta < 0m));
        }

        // 3. A stat that got worse; without a baseline, the one that cost the most strokes.
        var worst = candidates.MaxBy(c => c.Score)!;
        if (worst.Score > 0m)
            return Answer(worst, texts.LeverRationale(worst.Lever, worst.Delta));

        // 4. Nothing got worse: the stat that still cost the most strokes, also when it went
        //    down (keep going). A first round lands here, as its baseline is that round itself.
        var costliest = candidates.MaxBy(c => c.Cost)!;
        if (costliest.Cost > 0m)
            return Answer(costliest, costliest.Delta < 0m
                ? texts.LeverKeepGoingRationale(costliest.Lever)
                : texts.LeverRationale(costliest.Lever, null));

        // 5. No penalties, three-putts or blow-ups at all: keep doing what worked.
        var steady = Candidate.For("hold_steady", LeverText.HoldSteady, 0m, null, 0m, 0m);
        return Answer(steady, texts.LeverRationale(LeverText.HoldSteady, baseline is null ? null : 0m));
    }

    /// <summary>"Much worse": this many above the baseline, per 18 holes, outranks the golfer's goal.</summary>
    private const decimal MuchWorse = 2m;

    /// <summary>The stat a goal focus area is worked on through; tee shots have none yet.</summary>
    private static LeverText? LeverFor(GoalFocusArea goal) => goal switch
    {
        GoalFocusArea.Putting => LeverText.ThreePutts,
        GoalFocusArea.Penalties => LeverText.Penalties,
        GoalFocusArea.Consistency => LeverText.DamageControl,
        _ => null,
    };

    /// <summary>
    /// One stat of the round. <see cref="Score"/> is how much it got worse than the baseline,
    /// weighted (or, without a baseline, its cost); <see cref="Cost"/> is roughly how many
    /// strokes it cost, whether or not it got worse.
    /// </summary>
    private sealed record Candidate(string Key, LeverText Lever, decimal Count, decimal? Delta, decimal Score, decimal Cost)
    {
        public static Candidate For(string key, LeverText lever, decimal count, decimal? baseVal, decimal weight, decimal costWeight)
        {
            decimal? delta = baseVal is decimal b ? count - b : null;
            var score = delta is decimal d ? Math.Max(d, 0m) * weight : count * costWeight;
            return new Candidate(key, lever, count, delta, score, count * costWeight);
        }
    }

    public async Task LogEventAsync(Guid userId, Guid roundId, string leverKey, CancellationToken ct)
    {
        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = userId,
            EventType = "NextLeverRecommended",
            RelatedId = roundId,
            PayloadJson = JsonDocument.Parse(
                JsonSerializer.Serialize(new { roundId, leverKey }),
                new JsonDocumentOptions()
            ),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}