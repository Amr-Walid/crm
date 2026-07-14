using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniGroup.CRM.Infrastructure.Channels;

namespace UniGroup.CRM.API.Controllers;

/// <summary>
/// Secure ingest endpoint for Chatwoot webhooks. Validates the HMAC-SHA256
/// signature with constant-time comparison, then enqueues the raw payload to a
/// bounded channel and returns 202 immediately (Inbox pattern, sub-10ms).
/// </summary>
[ApiController]
[Route("api/webhooks/chatwoot")]
public class ChatwootWebhookController : ControllerBase
{
    private readonly ChatwootWebhookChannel _webhookChannel;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatwootWebhookController> _logger;

    private const string SignatureHeader = "X-Chatwoot-Signature";
    private const string SecretEnvVar = "CHATWOOT_WEBHOOK_SECRET";

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatwootWebhookController"/> class.
    /// </summary>
    public ChatwootWebhookController(
        ChatwootWebhookChannel webhookChannel,
        IConfiguration configuration,
        ILogger<ChatwootWebhookController> logger)
    {
        _webhookChannel = webhookChannel;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Receives a Chatwoot webhook: verifies the HMAC-SHA256 signature and
    /// enqueues the payload for asynchronous background processing.
    /// </summary>
    /// <returns>202 Accepted when enqueued; 400/401/500 on validation failures.</returns>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        // Enable buffering so the raw body can be read for signature verification
        // and remain readable afterwards (guardrail 8.3).
        Request.EnableBuffering();

        var signature = Request.Headers[SignatureHeader].ToString();
        if (string.IsNullOrEmpty(signature))
        {
            return BadRequest("Missing signature header.");
        }

        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest("Empty payload.");
        }

        // Secret: configuration key takes precedence, environment variable as fallback.
        var secret = _configuration.GetValue<string>("Chatwoot:WebhookSecret")
                     ?? Environment.GetEnvironmentVariable(SecretEnvVar);
        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogError("Chatwoot webhook secret is not configured.");
            return StatusCode(StatusCodes.Status500InternalServerError, "Webhook secret not configured.");
        }

        // HMAC-SHA256 over the raw body, Base64-encoded, compared in constant time
        // to prevent timing attacks. The signature value itself is never logged.
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var computedSignature = Convert.ToBase64String(computedHash);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(computedSignature)))
        {
            _logger.LogWarning("Rejected Chatwoot webhook with invalid signature from {Ip}.",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized("Invalid signature.");
        }

        await _webhookChannel.Writer.WriteAsync(body, cancellationToken);
        return Accepted();
    }
}
