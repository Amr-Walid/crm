using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniGroup.CRM.Application.Common.Interfaces;

namespace UniGroup.CRM.Infrastructure.Services;

/// <summary>
/// SMTP configuration options for outbound e-mail dispatch.
/// </summary>
public class SmtpOptions
{
    /// <summary>Gets or sets the SMTP server host.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP server port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Gets or sets the SMTP username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender address.</summary>
    public string FromAddress { get; set; } = "noreply@unigroup.com";

    /// <summary>Gets or sets a value indicating whether SSL is enabled.</summary>
    public bool EnableSsl { get; set; } = true;
}

/// <summary>
/// SMTP e-mail dispatcher. Fails soft (returns false) when SMTP is not
/// configured — the notification engine records the failure in NotificationLogs.
/// </summary>
public class EmailService : IEmailService
{
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailService"/> class.
    /// </summary>
    public EmailService(IOptions<SmtpOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_options.Host))
        {
            _logger.LogWarning("SMTP not configured; e-mail to {Recipient} not sent.", to);
            return false;
        }

        try
        {
            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                Credentials = new NetworkCredential(_options.Username, _options.Password)
            };

            using var message = new MailMessage(_options.FromAddress, to, subject, body);
            await client.SendMailAsync(message, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send e-mail to {Recipient}.", to);
            return false;
        }
    }
}
