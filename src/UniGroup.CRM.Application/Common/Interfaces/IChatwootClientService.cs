using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UniGroup.CRM.Application.Common.Interfaces;

/// <summary>
/// HTTP client abstraction for the Chatwoot REST API — used to send outgoing
/// messages to customer conversations and update contact attributes.
/// </summary>
public interface IChatwootClientService
{
    /// <summary>
    /// Sends a message into an existing Chatwoot conversation.
    /// </summary>
    /// <param name="conversationId">The Chatwoot conversation identifier.</param>
    /// <param name="message">The message text to send.</param>
    /// <param name="isPrivate">True to send as a private (internal) note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the API call succeeded.</returns>
    Task<bool> SendMessageAsync(string conversationId, string message, bool isPrivate = false, CancellationToken ct = default);

    /// <summary>
    /// Updates custom attributes on a Chatwoot contact.
    /// </summary>
    /// <param name="contactId">The Chatwoot contact identifier.</param>
    /// <param name="attributes">Attribute key/value pairs to set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the API call succeeded.</returns>
    Task<bool> UpdateContactCustomAttributesAsync(string contactId, Dictionary<string, string> attributes, CancellationToken ct = default);

    /// <summary>
    /// Updates the core identity details (name, email, phone number) of a
    /// Chatwoot contact — used to synchronize CRM customer registrations back
    /// to Chatwoot immediately.
    /// </summary>
    /// <param name="contactId">The Chatwoot contact identifier.</param>
    /// <param name="name">The contact full name (null to leave unchanged).</param>
    /// <param name="email">The contact email (null to leave unchanged).</param>
    /// <param name="phoneNumber">The contact phone number in E.164-ish format (null to leave unchanged).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the API call succeeded.</returns>
    Task<bool> UpdateContactDetailsAsync(string contactId, string? name, string? email, string? phoneNumber, CancellationToken ct = default);
}
