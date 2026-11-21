using GolfProgress.Api.Configuration;
using GolfProgress.Api.Email;
using GolfProgress.Api.Localization;
using GolfProgress.Api.Security;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GolfProgress.Api.Services;

/// <summary>
/// The digest emails (#240, #245; docs/coach-access.md → "Notifications (N4)"). The daily
/// feedback digest goes to golfers with unread feedback that arrived since the last one,
/// so a message is in one digest, not in one every day until it's read. The weekly coach
/// digest lists each student's new rounds, sessions and replies, limited to what they
/// share. Both are skipped for whoever switched them off in Preferences, and when there's
/// nothing to say.
///
/// Each send is claimed by inserting its <see cref="DigestRun"/>: the key is (kind, day),
/// so when two instances try at once, one insert fails and only the other sends.
/// </summary>
public sealed class DigestService
{
    private readonly GolfProgressDbContext _db;
    private readonly FeedbackService _feedback;
    private readonly IEmailSender _sender;
    private readonly DigestOptions _options;
    private readonly Uri _app;
    private readonly ILogger<DigestService> _log;
    private readonly PlanService _plans;

    public DigestService(
        GolfProgressDbContext db,
        FeedbackService feedback,
        IEmailSender sender,
        IOptions<DigestOptions> options,
        IOptions<PasswordResetOptions> passwordReset,
        ILogger<DigestService> log,
        PlanService plans)
    {
        _plans = plans;
        _db = db;
        _feedback = feedback;
        _sender = sender;
        _options = options.Value;
        _app = new Uri(new Uri(passwordReset.Value.LinkBaseUrl), "/");
        _log = log;
    }

    /// <summary>Sends each digest whose time has come and that no one has sent yet.</summary>
    public async Task RunDueAsync(DateTimeOffset now, CancellationToken ct)
    {
        var day = DailySlot(now);
        await RunAsync(DigestKind.Feedback, day, now, TimeSpan.FromDays(1), SendFeedbackDigestsAsync, ct);
        await RunAsync(DigestKind.Coach, WeeklySlot(day), now, TimeSpan.FromDays(7), SendCoachDigestsAsync, ct);
    }

    /// <summary>The latest day whose send time has passed.</summary>
    internal DateOnly DailySlot(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        return now.UtcDateTime.Hour >= _options.SendHourUtc ? today : today.AddDays(-1);
    }

    /// <summary>The latest coach-digest day whose send time has passed.</summary>
    internal DateOnly WeeklySlot(DateOnly dailySlot)
    {
        var day = dailySlot;
        while (day.DayOfWeek != _options.CoachDigestDay) day = day.AddDays(-1);
        return day;
    }

    private async Task RunAsync(
        DigestKind kind, DateOnly period, DateTimeOffset now, TimeSpan firstWindow,
        Func<DateTimeOffset, DateTimeOffset, CancellationToken, Task<int>> send, CancellationToken ct)
    {
        if (await _db.DigestRuns.AnyAsync(r => r.Kind == kind && r.Period == period, ct)) return;

        // It covers what happened since the previous one started; the first covers one period.
        var previous = await _db.DigestRuns.AsNoTracking()
            .Where(r => r.Kind == kind && r.Period < period)
            .OrderByDescending(r => r.Period)
            .Select(r => (DateTimeOffset?)r.StartedAt)
            .FirstOrDefaultAsync(ct);
        var from = previous ?? now - firstWindow;

        var run = new DigestRun { Kind = kind, Period = period, StartedAt = now };
        _db.DigestRuns.Add(run);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another instance claimed it first.
            _db.ChangeTracker.Clear();
            return;
        }

        run.Sent = await send(from, now, ct);
        run.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Sent {Count} {Kind} digest emails for {Period}.", run.Sent, kind, period);
    }

    private async Task<int> SendFeedbackDigestsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        // Coaches' messages that arrived in this window: only their golfers can get a digest.
        var arrived = await _db.FeedbackMessages.AsNoTracking()
            .Where(m => m.CreatedAt >= from && m.CreatedAt < to && (m.AuthorId == null || m.AuthorId != m.GolferId))
            .Select(m => new { m.GolferId, m.TargetType, m.TargetId, m.CreatedAt })
            .ToListAsync(ct);
        var golferIds = arrived.Select(m => m.GolferId).Distinct().ToList();
        var golfers = await _db.Users.AsNoTracking()
            .Where(u => golferIds.Contains(u.UserId) && u.EmailFeedbackDigest && u.Email != null)
            .ToListAsync(ct);
        var reads = (await _db.FeedbackReads.AsNoTracking()
                .Where(r => golferIds.Contains(r.UserId) && r.UserId == r.GolferId)
                .ToListAsync(ct))
            .ToDictionary(r => (r.GolferId, r.TargetType, r.TargetId), r => r.ReadUpTo);

        var sent = 0;
        foreach (var golfer in golfers)
        {
            var stillUnread = arrived.Any(m => m.GolferId == golfer.UserId
                && (!reads.TryGetValue((m.GolferId, m.TargetType, m.TargetId), out var readUpTo) || m.CreatedAt > readUpTo));
            if (!stillUnread) continue;

            // The email lists every unread thread, not only this window's, like the app does.
            var unread = await _feedback.UnreadForGolferAsync(golfer.UserId, ct);
            var items = unread.Threads.Select(t => new FeedbackDigestItem(
                t.CoachName,
                Enum.Parse<FeedbackTargetType>(t.TargetType),
                t.OnDate is { } on ? DateOnly.Parse(on) : null,
                t.Unread,
                t.Preview)).ToList();

            var (subject, plain, html) = TextsFor(golfer).FeedbackDigestEmail(
                unread.Count, items, _app.ToString(), Link("profile"));
            if (await SendAsync(new EmailMessage(golfer.Email!, subject, plain, html), ct)) sent++;
        }
        return sent;
    }

    private async Task<int> SendCoachDigestsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var links = await _db.CoachLinks.AsNoTracking()
            .Include(l => l.Coach)
            .Include(l => l.Golfer)
            .Where(l => l.EndedAt == null && l.Coach.EmailCoachDigest && l.Coach.Email != null)
            .ToListAsync(ct);
        // A student without Coaching is paused (#312): nothing about them goes to the coach.
        var coaching = await _plans.CoachingOfAsync(links.Select(l => l.GolferId).Distinct().ToList(), ct);
        links = links.Where(l => coaching.GetValueOrDefault(l.GolferId)).ToList();
        var golferIds = links.Select(l => l.GolferId).Distinct().ToList();

        var rounds = await _db.Rounds.AsNoTracking()
            .Where(r => golferIds.Contains(r.UserId) && r.CreatedAt >= from && r.CreatedAt < to)
            .GroupBy(r => r.UserId)
            .Select(g => new { GolferId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.GolferId, g => g.Count, ct);
        var sessions = await _db.PracticeSessions.AsNoTracking()
            .Where(s => golferIds.Contains(s.UserId) && s.CreatedAt >= from && s.CreatedAt < to)
            .GroupBy(s => s.UserId)
            .Select(g => new { GolferId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.GolferId, g => g.Count, ct);
        var replies = await _db.FeedbackMessages.AsNoTracking()
            .Where(m => golferIds.Contains(m.GolferId) && m.AuthorId == m.GolferId && m.CoachId != null
                        && m.CreatedAt >= from && m.CreatedAt < to)
            .Select(m => new { m.GolferId, m.CoachId, m.TargetType })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var coachLinks in links.GroupBy(l => l.CoachId))
        {
            var students = coachLinks
                .Select(l => new StudentActivity(
                    CoachInvitationService.DisplayNameOf(l.Golfer),
                    rounds.GetValueOrDefault(l.GolferId),
                    l.SharePractice ? sessions.GetValueOrDefault(l.GolferId) : 0,
                    // Replies in this coach's threads, on what the golfer still shares with them.
                    replies.Count(m => m.GolferId == l.GolferId && m.CoachId == l.CoachId
                                       && CoachAccess.Covers(l, FeedbackService.ShareFor(m.TargetType)))))
                .Where(s => s.Rounds + s.Sessions + s.Replies > 0)
                .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (students.Count == 0) continue;

            var coach = coachLinks.First().Coach;
            var (subject, plain, html) = TextsFor(coach).CoachDigestEmail(students, Link("students"), Link("profile"));
            if (await SendAsync(new EmailMessage(coach.Email!, subject, plain, html), ct)) sent++;
        }
        return sent;
    }

    /// <summary>In the user's chosen language; English when they follow the device's, which the API can't know here.</summary>
    private static ApiTexts TextsFor(User user) => user.Language is { } language ? ApiTexts.For(language) : ApiTexts.English;

    private string Link(string path) => new Uri(_app, path).ToString();

    private async Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        try
        {
            await _sender.SendAsync(message, ct);
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // One failure doesn't stop the others; the address isn't logged.
            _log.LogError(e, "Sending a digest email failed: {Subject}", message.Subject);
            return false;
        }
    }
}

/// <summary>Checks every few minutes whether a digest is due (<see cref="DigestService"/>).</summary>
public sealed class DigestWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly DigestOptions _options;
    private readonly ILogger<DigestWorker> _log;

    public DigestWorker(IServiceScopeFactory scopes, IOptions<DigestOptions> options, ILogger<DigestWorker> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.CheckIntervalMinutes));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DigestService>().RunDueAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // A failed check must not stop the app; it's tried again next time.
                _log.LogError(e, "Sending digests failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
