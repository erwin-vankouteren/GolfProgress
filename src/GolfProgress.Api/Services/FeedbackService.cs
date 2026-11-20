using System.Text.Json;
using GolfProgress.Api.Contracts.Feedback;
using GolfProgress.Api.Security;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GolfProgress.Api.Services;

/// <summary>Why a message couldn't be written or changed.</summary>
public enum FeedbackProblem
{
    /// <summary>No such item, or the caller can't see it: answered 404.</summary>
    NotFound,
    EmptyText,
    /// <summary>The golfer named no coach, or one who can't see the item.</summary>
    NoSuchCoach,
    HourlyLimit,
}

/// <summary>
/// Feedback threads on a golfer's rounds, reflections and practice sessions (#212,
/// docs/coach-access.md → "Feedback"). One rule decides every request: the caller is the
/// golfer, or a coach with an active link whose sharing covers the item (<see cref="CoachAccess"/>).
/// Each coach has their own thread with the golfer on an item, so coaches never see each other.
/// </summary>
public sealed class FeedbackService
{
    private readonly GolfProgressDbContext _db;
    private readonly CoachAccess _access;

    public FeedbackService(GolfProgressDbContext db, CoachAccess access)
    {
        _db = db;
        _access = access;
    }

    /// <summary>What a coach must be shown to see an item of this type.</summary>
    public static CoachShare ShareFor(FeedbackTargetType type) => type switch
    {
        FeedbackTargetType.Reflection => CoachShare.Reflections,
        FeedbackTargetType.PracticeSession => CoachShare.Practice,
        _ => CoachShare.RoundsAndProgress,
    };

    public static bool TryParseTarget(string value, out FeedbackTargetType type) =>
        Enum.TryParse(value, ignoreCase: true, out type) && Enum.IsDefined(type);

    /// <summary>
    /// The thread on an item, oldest first: every coach's for the golfer, only their own for a
    /// coach. Null when the caller can't see the item.
    /// </summary>
    public async Task<List<FeedbackMessageResponse>?> ThreadAsync(
        Guid callerId, Guid golferId, FeedbackTargetType type, Guid targetId, CancellationToken ct)
    {
        if (!await CanSeeAsync(callerId, golferId, type, targetId, ct)) return null;

        var query = _db.FeedbackMessages.AsNoTracking()
            .Where(m => m.GolferId == golferId && m.TargetType == type && m.TargetId == targetId);
        if (callerId != golferId) query = query.Where(m => m.CoachId == callerId);

        var messages = await query.OrderBy(m => m.CreatedAt).ThenBy(m => m.FeedbackMessageId).ToListAsync(ct);
        return await ToResponsesAsync(callerId, messages, ct);
    }

    /// <summary>Writes a message in the caller's thread (a coach) or in the named coach's (the golfer).</summary>
    public async Task<(FeedbackMessageResponse? Message, FeedbackProblem? Problem)> WriteAsync(
        Guid callerId, Guid golferId, FeedbackTargetType type, Guid targetId, Guid? coachId, string text,
        CancellationToken ct)
    {
        if (!await CanSeeAsync(callerId, golferId, type, targetId, ct)) return (null, FeedbackProblem.NotFound);

        var trimmed = text.Trim();
        if (trimmed.Length == 0) return (null, FeedbackProblem.EmptyText);

        Guid threadCoach;
        if (callerId == golferId)
        {
            // Only to a coach who can see it: otherwise nobody would read it.
            if (coachId is not { } named || named == golferId
                || await _access.LinkCoveringAsync(named, golferId, ShareFor(type), ct) is null)
                return (null, FeedbackProblem.NoSuchCoach);
            threadCoach = named;
        }
        else
        {
            threadCoach = callerId;
        }

        var now = DateTimeOffset.UtcNow;
        var written = await _db.Events.CountAsync(
            e => e.UserId == callerId && e.EventType == "FeedbackWritten" && e.CreatedAt > now.AddHours(-1), ct);
        if (written >= FeedbackLimits.MaxPerHour) return (null, FeedbackProblem.HourlyLimit);

        var message = new FeedbackMessage
        {
            FeedbackMessageId = Guid.NewGuid(),
            GolferId = golferId,
            CoachId = threadCoach,
            AuthorId = callerId,
            TargetType = type,
            TargetId = targetId,
            Text = trimmed,
            CreatedAt = now,
        };
        _db.FeedbackMessages.Add(message);
        // In the author's log, where the hourly limit counts it.
        _db.Events.Add(new EventLog
        {
            EventLogId = Guid.NewGuid(),
            UserId = callerId,
            EventType = "FeedbackWritten",
            RelatedId = message.FeedbackMessageId,
            PayloadJson = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                feedbackMessageId = message.FeedbackMessageId,
                golferId,
                targetType = type.ToString(),
                targetId,
            })),
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);

        return ((await ToResponsesAsync(callerId, new List<FeedbackMessage> { message }, ct)).Single(), null);
    }

    /// <summary>Changes the caller's own message, while they can still see its item.</summary>
    public async Task<(FeedbackMessageResponse? Message, FeedbackProblem? Problem)> EditAsync(
        Guid callerId, Guid messageId, string text, CancellationToken ct)
    {
        var message = await OwnMessageAsync(callerId, messageId, ct);
        if (message is null) return (null, FeedbackProblem.NotFound);

        var trimmed = text.Trim();
        if (trimmed.Length == 0) return (null, FeedbackProblem.EmptyText);

        if (trimmed != message.Text)
        {
            message.Text = trimmed;
            message.EditedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return ((await ToResponsesAsync(callerId, new List<FeedbackMessage> { message }, ct)).Single(), null);
    }

    /// <summary>Deletes the caller's own message; false when there's no such one they can reach.</summary>
    public async Task<bool> DeleteAsync(Guid callerId, Guid messageId, CancellationToken ct)
    {
        var message = await OwnMessageAsync(callerId, messageId, ct);
        if (message is null) return false;

        _db.FeedbackMessages.Remove(message);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Marks the feedback on an item read up to [upTo] for the caller, who must be able to see
    /// it. Never moves backwards, nor past now. False without access.
    /// </summary>
    public async Task<bool> MarkReadAsync(
        Guid callerId, Guid golferId, FeedbackTargetType type, Guid targetId, DateTimeOffset? upTo, CancellationToken ct)
    {
        if (!await CanSeeAsync(callerId, golferId, type, targetId, ct)) return false;

        var now = DateTimeOffset.UtcNow;
        var until = upTo is { } at && at < now ? at : now;
        var read = await _db.FeedbackReads.FirstOrDefaultAsync(
            r => r.UserId == callerId && r.GolferId == golferId && r.TargetType == type && r.TargetId == targetId, ct);
        if (read is null)
            _db.FeedbackReads.Add(new FeedbackRead
            {
                UserId = callerId, GolferId = golferId, TargetType = type, TargetId = targetId, ReadUpTo = until,
            });
        else if (until > read.ReadUpTo)
            read.ReadUpTo = until;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// What the golfer hasn't read from their coaches (#220): the count, and the newest threads
    /// with what each is about.
    /// </summary>
    public async Task<UnreadFeedbackResponse> UnreadForGolferAsync(Guid golferId, CancellationToken ct)
    {
        var reads = await ReadsAsync(golferId, golferId, ct);
        var fromCoaches = await _db.FeedbackMessages.AsNoTracking()
            .Where(m => m.GolferId == golferId && (m.AuthorId == null || m.AuthorId != golferId))
            .ToListAsync(ct);
        var unread = fromCoaches.Where(m => IsUnread(m, reads)).ToList();

        var threads = unread
            .GroupBy(m => (m.TargetType, m.TargetId, m.CoachId))
            .Select(g => g.OrderByDescending(m => m.CreatedAt).ToList())
            .OrderByDescending(g => g[0].CreatedAt)
            .Take(5)
            .ToList();

        // What each is about, to link to it: a round, a reflection's round, or a session.
        var roundIds = threads.Where(t => t[0].TargetType == FeedbackTargetType.Round).Select(t => t[0].TargetId).ToList();
        var reflectionIds = threads.Where(t => t[0].TargetType == FeedbackTargetType.Reflection).Select(t => t[0].TargetId).ToList();
        var sessionIds = threads.Where(t => t[0].TargetType == FeedbackTargetType.PracticeSession).Select(t => t[0].TargetId).ToList();
        var reflectionRounds = await _db.Reflections.AsNoTracking()
            .Where(r => reflectionIds.Contains(r.ReflectionId))
            .ToDictionaryAsync(r => r.ReflectionId, r => r.RoundId, ct);
        var allRoundIds = roundIds.Concat(reflectionRounds.Values.OfType<Guid>()).ToList();
        var roundDates = await _db.Rounds.AsNoTracking()
            .Where(r => allRoundIds.Contains(r.RoundId))
            .ToDictionaryAsync(r => r.RoundId, r => r.PlayedAt, ct);
        var sessionDates = await _db.PracticeSessions.AsNoTracking()
            .Where(s => sessionIds.Contains(s.PracticeSessionId))
            .ToDictionaryAsync(s => s.PracticeSessionId, s => s.PracticedAt, ct);
        var coachIds = threads.Select(t => t[0].CoachId).OfType<Guid>().Distinct().ToList();
        var coaches = await _db.Users.AsNoTracking().Where(u => coachIds.Contains(u.UserId)).ToListAsync(ct);
        var names = coaches.ToDictionary(u => u.UserId, CoachInvitationService.DisplayNameOf);

        return new UnreadFeedbackResponse
        {
            Count = unread.Count,
            Threads = threads.Select(t =>
            {
                var newest = t[0];
                Guid? roundId = newest.TargetType switch
                {
                    FeedbackTargetType.Round => newest.TargetId,
                    FeedbackTargetType.Reflection => reflectionRounds.GetValueOrDefault(newest.TargetId),
                    _ => null,
                };
                DateOnly? on = newest.TargetType == FeedbackTargetType.PracticeSession
                    ? sessionDates.TryGetValue(newest.TargetId, out var practiced) ? practiced : null
                    : roundId is { } id && roundDates.TryGetValue(id, out var played) ? played : null;
                return new UnreadFeedbackThreadResponse
                {
                    TargetType = newest.TargetType.ToString(),
                    TargetId = newest.TargetId,
                    RoundId = roundId,
                    OnDate = on?.ToString("yyyy-MM-dd"),
                    CoachId = newest.CoachId,
                    CoachName = newest.CoachId is { } c && names.TryGetValue(c, out var name) ? name : null,
                    Unread = t.Count,
                    NewestAt = newest.CreatedAt,
                    Preview = newest.Text.Length <= 140 ? newest.Text : newest.Text[..139].TrimEnd() + "…",
                };
            }).ToList(),
        };
    }

    private async Task<Dictionary<(FeedbackTargetType, Guid), DateTimeOffset>> ReadsAsync(
        Guid userId, Guid golferId, CancellationToken ct) =>
        await _db.FeedbackReads.AsNoTracking()
            .Where(r => r.UserId == userId && r.GolferId == golferId)
            .ToDictionaryAsync(r => (r.TargetType, r.TargetId), r => r.ReadUpTo, ct);

    private static bool IsUnread(FeedbackMessage m, Dictionary<(FeedbackTargetType, Guid), DateTimeOffset> reads) =>
        !reads.TryGetValue((m.TargetType, m.TargetId), out var readUpTo) || m.CreatedAt > readUpTo;

    /// <summary>
    /// Replies from each of the coach's students the coach hasn't read, on what the coach can
    /// still see (#199's unread replies).
    /// </summary>
    public async Task<Dictionary<Guid, int>> UnreadRepliesAsync(Guid coachId, IReadOnlyList<CoachLink> links, CancellationToken ct)
    {
        var golferIds = links.Select(l => l.GolferId).ToList();
        var reads = await _db.FeedbackReads.AsNoTracking()
            .Where(r => r.UserId == coachId && golferIds.Contains(r.GolferId))
            .ToDictionaryAsync(r => (r.GolferId, r.TargetType, r.TargetId), r => r.ReadUpTo, ct);
        var replies = await _db.FeedbackMessages.AsNoTracking()
            .Where(m => m.CoachId == coachId && golferIds.Contains(m.GolferId) && m.AuthorId == m.GolferId)
            .Select(m => new { m.GolferId, m.TargetType, m.TargetId, m.CreatedAt })
            .ToListAsync(ct);

        return links.ToDictionary(
            l => l.GolferId,
            l => replies.Count(m => m.GolferId == l.GolferId
                                    && CoachAccess.Covers(l, ShareFor(m.TargetType))
                                    && (!reads.TryGetValue((m.GolferId, m.TargetType, m.TargetId), out var readUpTo)
                                        || m.CreatedAt > readUpTo)));
    }

    /// <summary>
    /// The caller's own message, while they can still see it: a coach only in their own thread,
    /// with an active link that covers the item.
    /// </summary>
    private async Task<FeedbackMessage?> OwnMessageAsync(Guid callerId, Guid messageId, CancellationToken ct)
    {
        var message = await _db.FeedbackMessages.FirstOrDefaultAsync(
            m => m.FeedbackMessageId == messageId && m.AuthorId == callerId, ct);
        if (message is null) return null;
        if (callerId != message.GolferId && message.CoachId != callerId) return null;
        return await CanSeeAsync(callerId, message.GolferId, message.TargetType, message.TargetId, ct) ? message : null;
    }

    /// <summary>The access rule: the golfer, or a coach whose link covers it; and the item is the golfer's.</summary>
    private async Task<bool> CanSeeAsync(Guid callerId, Guid golferId, FeedbackTargetType type, Guid targetId, CancellationToken ct)
    {
        if (callerId != golferId && await _access.LinkCoveringAsync(callerId, golferId, ShareFor(type), ct) is null)
            return false;

        return type switch
        {
            FeedbackTargetType.Round => await _db.Rounds.AnyAsync(r => r.RoundId == targetId && r.UserId == golferId, ct),
            FeedbackTargetType.Reflection => await _db.Reflections.AnyAsync(r => r.ReflectionId == targetId && r.UserId == golferId, ct),
            FeedbackTargetType.PracticeSession => await _db.PracticeSessions.AnyAsync(
                s => s.PracticeSessionId == targetId && s.UserId == golferId, ct),
            _ => false,
        };
    }

    private async Task<List<FeedbackMessageResponse>> ToResponsesAsync(
        Guid callerId, List<FeedbackMessage> messages, CancellationToken ct)
    {
        var coachIds = messages.Where(m => m.CoachId != null).Select(m => m.CoachId!.Value).Distinct().ToList();
        var coaches = await _db.Users.AsNoTracking().Where(u => coachIds.Contains(u.UserId)).ToListAsync(ct);
        var names = coaches.ToDictionary(u => u.UserId, CoachInvitationService.DisplayNameOf);

        return messages.Select(m => new FeedbackMessageResponse
        {
            FeedbackMessageId = m.FeedbackMessageId,
            GolferId = m.GolferId,
            CoachId = m.CoachId,
            CoachName = m.CoachId is { } id && names.TryGetValue(id, out var name) ? name : null,
            // A deleted author can only be the coach: the golfer's deletion takes the thread.
            FromCoach = m.AuthorId != m.GolferId,
            Mine = m.AuthorId == callerId,
            TargetType = m.TargetType.ToString(),
            TargetId = m.TargetId,
            Text = m.Text,
            CreatedAt = m.CreatedAt,
            EditedAt = m.EditedAt,
        }).ToList();
    }
}
