using GolfProgress.Api.Contracts.Dashboard;
using GolfProgress.Api.ErrorHandling;
using GolfProgress.Api.Contracts.MicroWins;
using GolfProgress.Api.Contracts.Recommendations;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Services;

public sealed class DashboardService
{
    private readonly GolfProgressDbContext _db;
    private readonly NextLeverService _nextLeverSvc;

    public DashboardService(GolfProgressDbContext db, NextLeverService nextLeverSvc)
    {
        _db = db;
        _nextLeverSvc = nextLeverSvc;
    }

    public async Task<DashboardResponse> BuildAsync(Guid userId, CancellationToken ct)
    {
        var exists = await _db.Users.AnyAsync(u => u.UserId == userId, ct);
        if (!exists) throw new NotFoundException("User not found.");

        // Last round
        var lastRound = await _db.Rounds.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.PlayedAt)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        DashboardRoundSummary? roundSummary = null;
        List<MicroWinResponse> microWins = new();
        NextLeverResponse? nextLever = null;

        if (lastRound != null)
        {
            var fs = await _db.FitnessScores.AsNoTracking()
                .FirstOrDefaultAsync(x => x.RoundId == lastRound.RoundId, ct);

            roundSummary = new DashboardRoundSummary
            {
                RoundId = lastRound.RoundId,
                PlayedAt = lastRound.PlayedAt.ToString("yyyy-MM-dd"),
                TotalStrokes = lastRound.TotalStrokes,
                FitnessScore = fs?.ScoreTotal,
                FitnessConfidence = fs?.ConfidenceLevel.ToString()
            };

            microWins = await _db.MicroWins.AsNoTracking()
                .Where(mw => mw.RoundId == lastRound.RoundId)
                .OrderBy(mw => mw.CreatedAt)
                .Take(3)
                .Select(mw => new MicroWinResponse
                {
                    MicroWinId = mw.MicroWinId,
                    UserId = mw.UserId,
                    RoundId = mw.RoundId,
                    Category = mw.Category.ToString(),
                    Metric = mw.Metric,
                    Direction = mw.Direction.ToString(),
                    DeltaValue = mw.DeltaValue,
                    ConfidenceLevel = mw.ConfidenceLevel.ToString(),
                    Title = mw.Title,
                    Message = mw.Message,
                    CreatedAt = mw.CreatedAt
                })
                .ToListAsync(ct);

            var (leverKey, title, rec, rationale, conf) =
                await _nextLeverSvc.RecommendAsync(lastRound.RoundId, userId, ct);

            nextLever = new NextLeverResponse
            {
                RoundId = lastRound.RoundId,
                UserId = userId,
                LeverKey = leverKey,
                Title = title,
                Recommendation = rec,
                Rationale = rationale,
                ConfidenceLevel = conf.ToString(),
                CreatedAt = DateTimeOffset.UtcNow
            };
        }

        // Baseline
        var baseline = await _db.UserBaselines.AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId, ct);

        // Monthly review (latest)
        var monthly = await _db.MonthlyReviews.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.MonthStart)
            .FirstOrDefaultAsync(ct);

        return new DashboardResponse
        {
            UserId = userId,
            LastRound = roundSummary,
            MicroWins = microWins,
            NextLever = nextLever,
            BaselineAvgScore = baseline?.AvgScore,
            BaselineConfidence = baseline?.ConfidenceLevel.ToString(),
            MonthlyFitnessAvg = monthly?.FitnessAvg,
            MonthlyFitnessChange = monthly?.FitnessChange,
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }
}