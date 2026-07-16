using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniGroup.CRM.Application.Common.Interfaces;

namespace UniGroup.CRM.Infrastructure.Services;

/// <summary>
/// Configuration options for the Chatwoot REST API integration.
/// </summary>
public class ChatwootOptions
{
    /// <summary>Gets or sets the Chatwoot base API URL (e.g. http://localhost:3000).</summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the Chatwoot account identifier.</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>Gets or sets the bot/agent API access token.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the shared secret used to verify inbound webhooks.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}

/// <summary>
/// HTTP client for the Chatwoot REST API. Sends outgoing conversation
/// messages and updates contact custom attributes. Fails soft (returns false)
/// when Chatwoot is not configured or unreachable.
/// </summary>
public class ChatwootClientService : IChatwootClientService
{
    private readonly HttpClient _httpClient;
    private readonly ChatwootOptions _options;
    private readonly ILogger<ChatwootClientService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatwootClientService"/> class.
    /// </summary>
    public ChatwootClientService(
        HttpClient httpClient,
        IOptions<ChatwootOptions> options,
        ILogger<ChatwootClientService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_options.ApiUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.ApiUrl);
            _httpClient.DefaultRequestHeaders.Add("api_access_token", _options.BotToken);
        }
    }

    /// <inheritdoc />
    public async Task<bool> SendMessageAsync(string conversationId, string message, bool isPrivate = false, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_options.ApiUrl))
        {
            _logger.LogWarning("Chatwoot API not configured; message to conversation {ConversationId} not sent.", conversationId);
            return false;
        }

        try
        {
            var url = $"/api/v1/accounts/{_options.AccountId}/conversations/{conversationId}/messages";
            var payload = new
            {
                content = message,
                message_type = isPrivate ? "private" : "outgoing"
            };

            var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Chatwoot message to conversation {ConversationId}.", conversationId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateContactCustomAttributesAsync(string contactId, Dictionary<string, string> attributes, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_options.ApiUrl))
        {
            return false;
        }

        try
        {
            var url = $"/api/v1/accounts/{_options.AccountId}/contacts/{contactId}";
            var payload = new { custom_attributes = attributes };

            var response = await _httpClient.PutAsJsonAsync(url, payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Chatwoot contact {ContactId}.", contactId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateContactDetailsAsync(string contactId, string? name, string? email, string? phoneNumber, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_options.ApiUrl))
        {
            _logger.LogWarning("Chatwoot API not configured; contact {ContactId} details not synced.", contactId);
            return false;
        }

        try
        {
            var url = $"/api/v1/accounts/{_options.AccountId}/contacts/{contactId}";

            // Only include fields that were provided so we never blank out
            // existing values on the Chatwoot side.
            var payload = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(name)) payload["name"] = name;
            if (!string.IsNullOrWhiteSpace(email)) payload["email"] = email;
            if (!string.IsNullOrWhiteSpace(phoneNumber)) payload["phone_number"] = phoneNumber;

            if (payload.Count == 0)
            {
                return true; // nothing to update
            }

            var response = await _httpClient.PutAsJsonAsync(url, payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Chatwoot contact sync for {ContactId} returned {StatusCode}.",
                    contactId, (int)response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync Chatwoot contact details for {ContactId}.", contactId);
            return false;
        }
    }
}
