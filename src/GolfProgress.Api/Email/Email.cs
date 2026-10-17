using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace GolfProgress.Api.Email;

/// <summary>An email to one recipient, as plain text and HTML.</summary>
public sealed record EmailMessage(string To, string Subject, string PlainText, string Html);

/// <summary>Sends an email now. Behind <see cref="IEmailOutbox"/>, never called by a request.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>
/// Hands emails to a background sender, so a request never waits on the email provider.
/// That keeps a password reset for an unknown address as fast as one for a known
/// address (#86): the response time reveals nothing.
/// </summary>
public interface IEmailOutbox
{
    void Enqueue(EmailMessage message);
}

/// <summary>Email settings (#86); see docs/deployment.md → "Email".</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// The Azure Communication Services connection string. Empty means emails are only
    /// logged, which is right for local development and wrong anywhere users depend on
    /// them: a warning says so at startup.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>The verified sender address, e.g. DoNotReply@golfprogress.app.</summary>
    [EmailAddress]
    public string? SenderAddress { get; set; }

    public bool Configured => !string.IsNullOrWhiteSpace(ConnectionString);
}

/// <summary>Sends through Azure Communication Services Email.</summary>
public sealed class AzureCommunicationEmailSender : IEmailSender
{
    private readonly EmailClient _client;
    private readonly string _sender;

    public AzureCommunicationEmailSender(IOptions<EmailOptions> options)
    {
        _client = new EmailClient(options.Value.ConnectionString);
        _sender = options.Value.SenderAddress
            ?? throw new InvalidOperationException("Email:SenderAddress is required when Email:ConnectionString is set.");
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var email = new Azure.Communication.Email.EmailMessage(
            _sender,
            message.To,
            new EmailContent(message.Subject) { PlainText = message.PlainText, Html = message.Html });

        // Started, not awaited to delivery: the provider retries on its side.
        await _client.SendAsync(WaitUntil.Started, email, ct);
    }
}

/// <summary>
/// Logs instead of sending: for local development, where the reset link is read from
/// the log. Logs the recipient's domain only, never the address.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _log;

    public LoggingEmailSender(ILogger<LoggingEmailSender> log) => _log = log;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var domain = message.To[(message.To.IndexOf('@') + 1)..];
        _log.LogInformation("Email not sent (no Email:ConnectionString), to a recipient at {Domain}: {Subject}\n{Body}",
            domain, message.Subject, message.PlainText);
        return Task.CompletedTask;
    }
}

/// <summary>An in-memory queue, drained by <see cref="EmailSendingWorker"/>.</summary>
public sealed class ChannelEmailOutbox : IEmailOutbox
{
    // Bounded, so a flood of reset requests can't grow memory without limit. When it's
    // full, new emails are dropped rather than queued: the user can ask again.
    internal readonly Channel<EmailMessage> Queue =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Enqueue(EmailMessage message) => Queue.Writer.TryWrite(message);
}

/// <summary>Sends what the outbox holds, one at a time; a failure is logged, not retried.</summary>
public sealed class EmailSendingWorker : BackgroundService
{
    private readonly ChannelEmailOutbox _outbox;
    private readonly IEmailSender _sender;
    private readonly ILogger<EmailSendingWorker> _log;

    public EmailSendingWorker(ChannelEmailOutbox outbox, IEmailSender sender, ILogger<EmailSendingWorker> log)
    {
        _outbox = outbox;
        _sender = sender;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _outbox.Queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _sender.SendAsync(message, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // The user can ask again; the address isn't logged.
                _log.LogError(e, "Sending an email failed: {Subject}", message.Subject);
            }
        }
    }
}
