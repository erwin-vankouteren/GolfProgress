using GolfProgress.Domain.Entities;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Localization;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GolfProgress.Api.Services;

public sealed class MicroWinsService
{
    private readonly GolfProgressDbContext _db;

    public MicroWinsService(GolfProgressDbContext db) => _db = db;

    public async Task<List<MicroWin>> GenerateForRoundAsync(Guid roundId, Guid userId, CancellationToken ct)
    {
        var round = await _db.Rounds.FirstOrDefaultAsync(r => r.RoundId == roundId && r.UserId == userId, ct)
            ?? throw new NotFoundException($"Round {roundId} not found.");

        var baseline = await _db.UserBaselines.AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == round.UserId, ct);

        var wins = await StageForRoundAsync(round, baseline, ct);

        await _db.SaveChangesAsync(ct);

        return wins;
    }

    /// <summary>
    /// Stages the round's micro-wins and their event, without saving, judged against
    /// <paramref name="baseline"/>. Logging a round passes the baseline it has just
    /// staged, so the round and its micro-wins are written in one save (#80).
    /// </summary>
    public async Task<List<MicroWin>> StageForRoundAsync(Round round, UserBaseline? baseline, CancellationToken ct)
    {
        var roundId = round.RoundId;

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == round.UserId, ct)
            ?? throw new NotFoundException($"User {round.UserId} not found.");

        // Latest reflection for this round (optional)
        var reflection = await _db.Reflections.AsNoTracking()
            .Where(r => r.RoundId == roundId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var confidence = baseline?.ConfidenceLevel ?? ConfidenceLevel.Low;
        var now = DateTimeOffset.UtcNow;

        // Clear existing micro-wins for this round (idempotent)
        var existing = await _db.MicroWins.Where(mw => mw.RoundId == roundId).ToListAsync(ct);
        if (existing.Count > 0)
            _db.MicroWins.RemoveRange(existing);

        var wins = new List<MicroWin>();

        // 1) Stat-based win (prefer real improvement vs baseline)
        wins.Add(BuildStatWin(user, round, baseline, confidence, now));

        // 2) Behavior/habit win (reflection or logging)
        wins.Add(BuildBehaviorWin(round, reflection, confidence, now));

        // 3) Focus-area narrative win (tie to user goal_focus if possible)
        wins.Add(BuildFocusWin(user, round, baseline, confidence, now));

        // Persist
        _db.MicroWins.AddRange(wins);

        // Event
        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = round.UserId,
            EventType = "MicroWinsGenerated",
            RelatedId = roundId,
            PayloadJson = JsonDocument.Parse(
                JsonSerializer.Serialize(new { roundId, count = wins.Count }),
                new JsonDocumentOptions()
            ),
            CreatedAt = now
        });

        return wins;
    }

    private static MicroWin BuildStatWin(User user, Round round, UserBaseline? baseline, ConfidenceLevel confidence, DateTimeOffset now)
    {
        // Candidate metrics (lower is better)
        //
        // The baseline holds 18-hole equivalents, so round values are scaled the
        // same way before comparing; otherwise a 9-hole round always looks like an
        // improvement (#35).
        var candidates = new List<(string metric, decimal? roundValue, decimal? baseValue, decimal threshold, MicroWinText improved, MicroWinText stable)>();

        candidates.Add(("penalties",
            HoleScaling.Per18(round.Penalties, round), baseline?.AvgPenalties, 1m,
            MicroWinText.FewerPenalties, MicroWinText.PenaltiesHeldSteady));

        candidates.Add(("putts",
            HoleScaling.Per18(round.TotalPutts, round), baseline?.AvgPutts, 2m,
            MicroWinText.PuttingImproved, MicroWinText.PuttingHeldSteady));

        candidates.Add(("blow_up_holes",
            HoleScaling.Per18(round.BlowUpHoles, round), baseline?.AvgBlowUpHoles, 1m,
            MicroWinText.FewerBlowUpHoles, MicroWinText.BigMistakesContained));

        // Pick best truthful metric:
        // Prefer: baseline exists AND round value exists AND improved by threshold
        foreach (var c in candidates)
        {
            if (c.roundValue.HasValue && c.baseValue.HasValue)
            {
                var delta = c.roundValue.Value - c.baseValue.Value; // negative = improved
                if (delta <= -c.threshold)
                {
                    return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.StatDelta, c.metric, MicroWinDirection.Improved,
                        delta, confidence, c.improved, now,
                        new { metric = c.metric, baseline = c.baseValue, round = c.roundValue, delta });
                }
            }
        }

        // Otherwise: choose a stable win based on available stats
        // Prefer penalties if present, else a generic “round logged” stable win
        var fallback = candidates.FirstOrDefault(c => c.roundValue.HasValue);
        if (!string.IsNullOrEmpty(fallback.metric) && fallback.roundValue.HasValue)
        {
            decimal? delta = (baseline != null && fallback.baseValue.HasValue) ? fallback.roundValue.Value - fallback.baseValue.Value : null;
            return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.StatDelta, fallback.metric, MicroWinDirection.Stable,
                delta, confidence, fallback.stable, now,
                new { metric = fallback.metric, baseline = fallback.baseValue, round = fallback.roundValue, delta });
        }

        return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.Narrative, null, MicroWinDirection.Stable,
            null, confidence, MicroWinText.RoundLogged, now,
            new { reason = "no_stats_available" });
    }

    private static MicroWin BuildBehaviorWin(Round round, Reflection? reflection, ConfidenceLevel confidence, DateTimeOffset now)
    {
        if (reflection != null)
        {
            return NewMicroWin(round.UserId, round.RoundId, MicroWinCategory.Behavior, "reflection", MicroWinDirection.Improved,
                null, confidence, MicroWinText.LearningCaptured, now,
                new { reflectionId = reflection.ReflectionId });
        }

        return NewMicroWin(round.UserId, round.RoundId, MicroWinCategory.Habit, "logging", MicroWinDirection.Improved,
            null, confidence, MicroWinText.ConsistencyWin, now,
            new { reason = "reflection_missing" });
    }

    private static MicroWin BuildFocusWin(User user, Round round, UserBaseline? baseline, ConfidenceLevel confidence, DateTimeOffset now)
    {
        // Map the first focus area to a metric
        var focus = user.GoalFocus?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(focus))
        {
            return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.Narrative, null, MicroWinDirection.Stable,
                null, confidence, MicroWinText.ProgressSignalsMatter, now,
                new { reason = "no_focus_area" });
        }

        // PATCH /me stores GoalFocusArea names (#82).
        var area = MicroWinTexts.ParseFocus(focus);

        var metric = area switch
        {
            GoalFocusArea.Putting => "putts",
            GoalFocusArea.Penalties => "penalties",
            GoalFocusArea.TeeShots => "fairways_hit",
            GoalFocusArea.Consistency => "blow_up_holes",
            _ => null
        };

        var label = area is GoalFocusArea known ? ApiTexts.English.FocusLabel(known) : focus;

        if (metric == "putts" && round.TotalPutts.HasValue)
            return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.FocusArea, metric, MicroWinDirection.Stable,
                baseline?.AvgPutts != null ? HoleScaling.Per18(round.TotalPutts, round) - baseline.AvgPutts : null,
                confidence, MicroWinText.FocusPutting, now, new { focus, metric });

        if (metric == "penalties" && round.Penalties.HasValue)
            return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.FocusArea, metric, MicroWinDirection.Stable,
                baseline?.AvgPenalties != null ? HoleScaling.Per18(round.Penalties, round) - baseline.AvgPenalties : null,
                confidence, MicroWinText.FocusPenalties, now, new { focus, metric });

        return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.FocusArea, metric, MicroWinDirection.Stable,
            null, confidence, MicroWinText.FocusSet, now, new { focus, metric }, label);
    }

    private static MicroWin NewMicroWin(Guid userId, Guid roundId,
        MicroWinCategory category, string? metric, MicroWinDirection direction,
        decimal? delta, ConfidenceLevel confidence,
        MicroWinText text, DateTimeOffset createdAt, object evidence, string? focusLabel = null)
    {
        // Stored in English; MicroWinTexts shows it in the reader's language.
        var (title, message) = MicroWinTexts.Stored(text, confidence, focusLabel);
        return new MicroWin
        {
            MicroWinId = Guid.NewGuid(),
            UserId = userId,
            RoundId = roundId,
            Category = category,
            Metric = metric,
            Direction = direction,
            DeltaValue = delta,
            ConfidenceLevel = confidence,
            Title = title,
            Message = message,
            EvidenceJson = JsonDocument.Parse(JsonSerializer.Serialize(evidence), new JsonDocumentOptions()),
            CreatedAt = createdAt
        };
    }
}