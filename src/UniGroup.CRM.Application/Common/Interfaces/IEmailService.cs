using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Common.Interfaces;

/// <summary>
/// SMTP e-mail dispatch abstraction used by the notification engine.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an e-mail message.
    /// </summary>
    /// <param name="to">Recipient e-mail address.</param>
    /// <param name="subject">Message subject.</param>
    /// <param name="body">Message body (plain text).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the message was handed to the SMTP server successfully.</returns>
    Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
