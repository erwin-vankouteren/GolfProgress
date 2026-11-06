using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GolfProgress.Api.Email;
using GolfProgress.Api.Localization;
using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using GolfProgress.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GolfProgress.Api.Services;

/// <summary>Coach invitation settings (#184); see docs/deployment.md → "Email".</summary>
public sealed class CoachInvitationOptions
{
    public const string SectionName = "CoachInvitations";

    /// <summary>
    /// The web app's page for an invitation; the token is added as ?token=. When unset, the
    /// page next to the password reset page, so an environment needs no extra setting.
    /// </summary>
    [Url]
    public string? LinkBaseUrl { get; set; }

    [Range(1, 30)]
    public int LifetimeDays { get; set; } = 7;

    /// <summary>Coaches a golfer can have, active and invited together.</summary>
    [Range(1, 20)]
    public int MaxCoaches { get; set; } = 3;

    /// <summary>Invitation emails a golfer can send a day, so the feature can't flood an inbox.</summary>
    [Range(1, 100)]
    public int MaxPerDay { get; set; } = 10;
}

/// <summary>Why an invitation couldn't be sent, accepted or declined.</summary>
public enum CoachInvitationProblem
{
    OwnAddress,
    AlreadyYourCoach,
    TooManyCoaches,
    DailyLimit,
    NotFound,
    WrongAccount,
}

/// <summary>
/// Golfers inviting coaches (#184, docs/coach-access.md → "Inviting a coach"). The link is
/// single use and expires; the account that accepts must have the invited address, so a
/// forwarded link can't be used by someone else; accepting creates the coach's link.
/// </summary>
public sealed class CoachInvitationService
{
    private readonly GolfProgressDbContext _db;
    private readonly IEmailOutbox _outbox;
    private readonly CoachInvitationOptions _options;
    private readonly string _linkBaseUrl;

    public CoachInvitationService(
        GolfProgressDbContext db,
        IEmailOutbox outbox,
        IOptions<CoachInvitationOptions> options,
        IOptions<PasswordResetOptions> passwordReset)
    {
        _db = db;
        _outbox = outbox;
        _options = options.Value;
        _linkBaseUrl = _options.LinkBaseUrl
            ?? new Uri(new Uri(passwordReset.Value.LinkBaseUrl), "/coach-invitation").ToString();
    }

    /// <summary>
    /// Invites [email] to coach the golfer, or, when an invitation to it is still open,
    /// sends that one again with a new link and the sharing chosen now.
    /// </summary>
    public async Task<(CoachInvitation? Invitation, bool Resent, CoachInvitationProblem? Problem)> InviteAsync(
        Guid golferId, string email, bool shareReflections, bool sharePractice, CancellationToken ct)
    {
        var address = Normalise(email);
        var golfer = await _db.Users.FirstAsync(u => u.UserId == golferId, ct);
        if (golfer.Email is not null && Normalise(golfer.Email) == address)
            return (null, false, CoachInvitationProblem.OwnAddress);

        var now = DateTimeOffset.UtcNow;
        var activeCoaches = await _db.CoachLinks.Where(l => l.GolferId == golferId && l.EndedAt == null)
            .Select(l => l.Coach.Email!.ToLower()).ToListAsync(ct);
        if (activeCoaches.Contains(address))
            return (null, false, CoachInvitationProblem.AlreadyYourCoach);

        var sentToday = await _db.Events.CountAsync(
            e => e.UserId == golferId && e.EventType == "CoachInvited" && e.CreatedAt > now.AddDays(-1), ct);
        if (sentToday >= _options.MaxPerDay)
            return (null, false, CoachInvitationProblem.DailyLimit);

        var open = await _db.CoachInvitations
            .Where(i => i.GolferId == golferId && i.AcceptedAt == null && i.DeclinedAt == null
                        && i.CancelledAt == null && i.ExpiresAt > now)
            .ToListAsync(ct);
        var invitation = open.FirstOrDefault(i => i.Email == address);
        var resent = invitation is not null;
        if (!resent && activeCoaches.Count + open.Count >= _options.MaxCoaches)
            return (null, false, CoachInvitationProblem.TooManyCoaches);

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        if (invitation is null)
        {
            invitation = new CoachInvitation { CoachInvitationId = Guid.NewGuid(), GolferId = golferId, Email = address };
            _db.CoachInvitations.Add(invitation);
        }

        // A resend replaces the link, so only the newest email works.
        invitation.TokenHash = Hash(token);
        invitation.ShareReflections = shareReflections;
        invitation.SharePractice = sharePractice;
        invitation.CreatedAt = now;
        invitation.ExpiresAt = now.AddDays(_options.LifetimeDays);
        _db.Events.Add(Event(golferId, "CoachInvited", invitation.CoachInvitationId, now));
        await _db.SaveChangesAsync(ct);

        // In the golfer's language: the coach may not have an account yet.
        var texts = golfer.Language is { } language ? ApiTexts.For(language) : ApiTexts.Current;
        var (subject, plainText, html) = texts.CoachInvitationEmail(
            DisplayNameOf(golfer), LinkFor(token), _options.LifetimeDays, shareReflections, sharePractice);
        _outbox.Enqueue(new EmailMessage(address, subject, plainText, html));
        return (invitation, resent, null);
    }

    /// <summary>Cancels one of the golfer's open invitations; false when there's no such one.</summary>
    public async Task<bool> CancelAsync(Guid golferId, Guid invitationId, CancellationToken ct)
    {
        var invitation = await _db.CoachInvitations.FirstOrDefaultAsync(
            i => i.CoachInvitationId == invitationId && i.GolferId == golferId, ct);
        if (invitation is null || !invitation.IsOpen) return false;

        var now = DateTimeOffset.UtcNow;
        invitation.CancelledAt = now;
        _db.Events.Add(Event(golferId, "CoachInvitationCancelled", invitation.CoachInvitationId, now));
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>The golfer's active coaches, and their invitations that can still be accepted.</summary>
    public async Task<(IReadOnlyList<CoachLink> Coaches, IReadOnlyList<CoachInvitation> Invitations)> ListAsync(
        Guid golferId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var links = await _db.CoachLinks.Include(l => l.Coach)
            .Where(l => l.GolferId == golferId && l.EndedAt == null).ToListAsync(ct);
        var invitations = await _db.CoachInvitations
            .Where(i => i.GolferId == golferId && i.AcceptedAt == null && i.DeclinedAt == null
                        && i.CancelledAt == null && i.ExpiresAt > now)
            .ToListAsync(ct);

        return (links.OrderBy(l => l.CreatedAt).ToList(), invitations.OrderByDescending(i => i.CreatedAt).ToList());
    }

    /// <summary>
    /// The golfer changes what a coach sees, from the coach's next request on (#189). Null
    /// when it isn't an active link of theirs.
    /// </summary>
    public async Task<CoachLink?> ChangeSharingAsync(
        Guid golferId, Guid linkId, bool? shareReflections, bool? sharePractice, CancellationToken ct)
    {
        var link = await _db.CoachLinks.Include(l => l.Coach).FirstOrDefaultAsync(
            l => l.CoachLinkId == linkId && l.GolferId == golferId && l.EndedAt == null, ct);
        if (link is null) return null;

        var reflections = shareReflections ?? link.ShareReflections;
        var practice = sharePractice ?? link.SharePractice;
        if (reflections == link.ShareReflections && practice == link.SharePractice) return link;

        link.ShareReflections = reflections;
        link.SharePractice = practice;
        _db.Events.Add(Event(golferId, "CoachSharingChanged", link.CoachLinkId, DateTimeOffset.UtcNow,
            new { coachLinkId = link.CoachLinkId, shareReflections = reflections, sharePractice = practice }));
        await _db.SaveChangesAsync(ct);
        return link;
    }

    /// <summary>The golfer removes a coach; false when it isn't an active link of theirs.</summary>
    public async Task<bool> RemoveCoachAsync(Guid golferId, Guid linkId, CancellationToken ct) =>
        await EndAsync(await _db.CoachLinks.FirstOrDefaultAsync(
            l => l.CoachLinkId == linkId && l.GolferId == golferId && l.EndedAt == null, ct), CoachLinkEndedBy.Golfer, ct);

    /// <summary>The coach stops coaching a golfer; false when they don't coach them.</summary>
    public async Task<bool> StopCoachingAsync(Guid coachId, Guid golferId, CancellationToken ct) =>
        await EndAsync(await _db.CoachLinks.FirstOrDefaultAsync(
            l => l.CoachId == coachId && l.GolferId == golferId && l.EndedAt == null, ct), CoachLinkEndedBy.Coach, ct);

    /// <summary>
    /// Ends a link. It stays as history, and isn't reused: inviting the same coach again
    /// creates a new one. In the golfer's log, as it's their data that was shared.
    /// </summary>
    private async Task<bool> EndAsync(CoachLink? link, CoachLinkEndedBy endedBy, CancellationToken ct)
    {
        if (link is null) return false;

        var now = DateTimeOffset.UtcNow;
        link.EndedAt = now;
        link.EndedBy = endedBy;
        _db.Events.Add(Event(link.GolferId, "CoachLinkEnded", link.CoachLinkId, now,
            new { coachLinkId = link.CoachLinkId, endedBy = endedBy.ToString() }));
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// What the signed-in coach is invited to, under the same rules as accepting, so a
    /// forwarded link doesn't show the golfer's name to someone else.
    /// </summary>
    public async Task<(CoachInvitation? Invitation, User? Golfer, CoachInvitationProblem? Problem)> PreviewAsync(
        Guid coachId, string token, CancellationToken ct)
    {
        var (invitation, _, problem) = await OpenInvitationForAsync(coachId, token, ct);
        if (problem is not null) return (null, null, problem);

        var golfer = await _db.Users.FirstAsync(u => u.UserId == invitation!.GolferId, ct);
        return (invitation, golfer, null);
    }

    /// <summary>
    /// The signed-in coach accepts: the invitation is used up and the link created, in one
    /// save. Accepting again while already linked returns the existing link.
    /// </summary>
    public async Task<(CoachLink? Link, User? Golfer, CoachInvitationProblem? Problem)> AcceptAsync(
        Guid coachId, string token, CancellationToken ct)
    {
        var (invitation, coach, problem) = await OpenInvitationForAsync(coachId, token, ct);
        if (problem is not null) return (null, null, problem);

        var now = DateTimeOffset.UtcNow;
        var golfer = await _db.Users.FirstAsync(u => u.UserId == invitation!.GolferId, ct);
        var link = await _db.CoachLinks.FirstOrDefaultAsync(
            l => l.GolferId == golfer.UserId && l.CoachId == coach!.UserId && l.EndedAt == null, ct);
        if (link is null)
        {
            var active = await _db.CoachLinks.CountAsync(l => l.GolferId == golfer.UserId && l.EndedAt == null, ct);
            if (active >= _options.MaxCoaches)
                return (null, null, CoachInvitationProblem.TooManyCoaches);

            link = new CoachLink
            {
                CoachLinkId = Guid.NewGuid(),
                GolferId = golfer.UserId,
                CoachId = coach!.UserId,
                ShareReflections = invitation!.ShareReflections,
                SharePractice = invitation.SharePractice,
                CreatedAt = now,
            };
            _db.CoachLinks.Add(link);
        }

        invitation!.AcceptedAt = now;
        // In the golfer's log, as it's their data being shared.
        _db.Events.Add(Event(golfer.UserId, "CoachInvitationAccepted", invitation.CoachInvitationId, now,
            new { coachInvitationId = invitation.CoachInvitationId, coachLinkId = link.CoachLinkId }));
        await _db.SaveChangesAsync(ct);
        return (link, golfer, null);
    }

    /// <summary>The signed-in coach declines; the link stops working.</summary>
    public async Task<CoachInvitationProblem?> DeclineAsync(Guid coachId, string token, CancellationToken ct)
    {
        var (invitation, _, problem) = await OpenInvitationForAsync(coachId, token, ct);
        if (problem is not null) return problem;

        var now = DateTimeOffset.UtcNow;
        invitation!.DeclinedAt = now;
        _db.Events.Add(Event(invitation.GolferId, "CoachInvitationDeclined", invitation.CoachInvitationId, now));
        await _db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>The open, unexpired invitation behind [token], if it was sent to the caller's address.</summary>
    private async Task<(CoachInvitation? Invitation, User? Coach, CoachInvitationProblem? Problem)> OpenInvitationForAsync(
        Guid coachId, string token, CancellationToken ct)
    {
        var hash = Hash(token);
        var now = DateTimeOffset.UtcNow;
        var invitation = await _db.CoachInvitations.FirstOrDefaultAsync(
            i => i.TokenHash == hash && i.AcceptedAt == null && i.DeclinedAt == null
                 && i.CancelledAt == null && i.ExpiresAt > now, ct);
        if (invitation is null) return (null, null, CoachInvitationProblem.NotFound);

        var coach = await _db.Users.FirstAsync(u => u.UserId == coachId, ct);
        if (coach.UserId == invitation.GolferId) return (null, null, CoachInvitationProblem.OwnAddress);
        if (coach.Email is null || Normalise(coach.Email) != invitation.Email)
            return (null, null, CoachInvitationProblem.WrongAccount);
        return (invitation, coach, null);
    }

    internal string LinkFor(string token) =>
        $"{_linkBaseUrl}{(_linkBaseUrl.Contains('?') ? '&' : '?')}token={Uri.EscapeDataString(token)}";

    /// <summary>The name the coach sees: never the golfer's email address.</summary>
    public static string DisplayNameOf(User user)
    {
        // Control characters would end up in the email's subject line.
        var name = new string((user.DisplayName ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return name.Length > 0 ? name : "A GolfProgress golfer";
    }

    private static string Normalise(string email) => email.Trim().ToLowerInvariant();

    private static EventLog Event(Guid userId, string type, Guid relatedId, DateTimeOffset now, object? payload = null) => new()
    {
        EventLogId = Guid.NewGuid(),
        UserId = userId,
        EventType = type,
        RelatedId = relatedId,
        PayloadJson = JsonDocument.Parse(JsonSerializer.Serialize(payload ?? new { coachInvitationId = relatedId })),
        CreatedAt = now,
    };

    internal static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
