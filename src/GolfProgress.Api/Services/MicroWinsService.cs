using GolfProgress.Domain.Entities;
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
            ?? throw new InvalidOperationException($"Round {roundId} not found.");

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == round.UserId, ct)
            ?? throw new InvalidOperationException($"User {round.UserId} not found.");

        var baseline = await _db.UserBaselines.AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == round.UserId, ct);

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

        await _db.SaveChangesAsync(ct);

        return wins;
    }

    private static MicroWin BuildStatWin(User user, Round round, UserBaseline? baseline, ConfidenceLevel confidence, DateTimeOffset now)
    {
        // Candidate metrics (lower is better)
        var candidates = new List<(string metric, decimal? roundValue, decimal? baseValue, decimal threshold, string titleImproved, string msgImproved, string titleStable, string msgStable)>();

        candidates.Add(("penalties",
            round.Penalties, baseline?.AvgPenalties, 1m,
            "Fewer penalties", "You had fewer penalty strokes than your recent average — that’s immediate damage control.",
            "Penalties held steady", "Penalties stayed under control today — that’s a strong scoring foundation."));

        candidates.Add(("putts",
            round.TotalPutts, baseline?.AvgPutts, 2m,
            "Putting improved", "Your total putts were lower than your recent average — nice progress on the greens.",
            "Putting held steady", "Putting stayed consistent — stability here usually shows up in scores soon."));

        candidates.Add(("blow_up_holes",
            round.BlowUpHoles, baseline?.AvgBlowUpHoles, 1m,
            "Fewer blow-up holes", "Your big-mistake holes were lower than usual — that’s real progress even if score didn’t show it.",
            "Big mistakes contained", "You kept big mistakes from spiraling — that’s a key improvement signal."));

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
                        delta, confidence, c.titleImproved, c.msgImproved, now,
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
                delta, confidence, fallback.titleStable, fallback.msgStable, now,
                new { metric = fallback.metric, baseline = fallback.baseValue, round = fallback.roundValue, delta });
        }

        return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.Narrative, null, MicroWinDirection.Stable,
            null, confidence, "Round logged", "You logged the round — that’s how progress becomes visible over time.", now,
            new { reason = "no_stats_available" });
    }

    private static MicroWin BuildBehaviorWin(Round round, Reflection? reflection, ConfidenceLevel confidence, DateTimeOffset now)
    {
        if (reflection != null)
        {
            return NewMicroWin(round.UserId, round.RoundId, MicroWinCategory.Behavior, "reflection", MicroWinDirection.Improved,
                null, confidence, "Learning captured", "You captured what worked and what to repeat — that speeds up improvement.", now,
                new { reflectionId = reflection.ReflectionId });
        }

        return NewMicroWin(round.UserId, round.RoundId, MicroWinCategory.Habit, "logging", MicroWinDirection.Improved,
            null, confidence, "Consistency win", "Logging rounds consistently makes trends show up faster — nice follow-through.", now,
            new { reason = "reflection_missing" });
    }

    private static MicroWin BuildFocusWin(User user, Round round, UserBaseline? baseline, ConfidenceLevel confidence, DateTimeOffset now)
    {
        // Map focus → metric
        // (Your goal_focus comes from User.GoalFocus list)
        var focus = user.GoalFocus?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(focus))
        {
            return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.Narrative, null, MicroWinDirection.Stable,
                null, confidence, "Progress signals matter", "Even when score is noisy, reducing big mistakes and staying consistent is progress.", now,
                new { reason = "no_focus_area" });
        }

        var metric = focus.ToLowerInvariant() switch
        {
            "putting" => "putts",
            "penalties" => "penalties",
            "tee_shots" => "fairways_hit",
            "consistency" => "blow_up_holes",
            _ => null
        };

        if (metric == "putts" && round.TotalPutts.HasValue)
            return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.FocusArea, metric, MicroWinDirection.Stable,
                baseline?.AvgPutts != null ? round.TotalPutts.Value - baseline.AvgPutts : null,
                confidence,
                "Focus-area check: putting",
                "You’re prioritizing putting — tracking it consistently is step one to improving it.",
                now, new { focus, metric });

        if (metric == "penalties" && round.Penalties.HasValue)
            return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.FocusArea, metric, MicroWinDirection.Stable,
                baseline?.AvgPenalties != null ? round.Penalties.Value - baseline.AvgPenalties : null,
                confidence,
                "Focus-area check: penalties",
                "You’re prioritizing penalties — keeping them in view is how they drop over time.",
                now, new { focus, metric });

        return NewMicroWin(user.UserId, round.RoundId, MicroWinCategory.FocusArea, metric, MicroWinDirection.Stable,
            null, confidence,
            $"Focus-area check: {focus}",
            "You set a focus area — we’ll tie more insights to it as you log more rounds.",
            now, new { focus, metric });
    }

    private static MicroWin NewMicroWin(Guid userId, Guid roundId,
        MicroWinCategory category, string? metric, MicroWinDirection direction,
        decimal? delta, ConfidenceLevel confidence,
        string title, string message, DateTimeOffset createdAt, object evidence)
    {
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
            Message = confidence == ConfidenceLevel.Low
                ? message + " (Lower confidence — add one more stat next time for sharper insights.)"
                : message,
            EvidenceJson = JsonDocument.Parse(JsonSerializer.Serialize(evidence), new JsonDocumentOptions()),
            CreatedAt = createdAt
        };
    }
}