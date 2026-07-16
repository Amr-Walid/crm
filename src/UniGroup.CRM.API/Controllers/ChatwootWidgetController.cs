using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.Customers.Commands.CreateCustomer;
using UniGroup.CRM.Application.Features.Customers.Queries.Common;
using UniGroup.CRM.Application.Features.Customers.Queries.GetCustomerDetails;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.API.Controllers;

/// <summary>
/// Backend for the Chatwoot Dashboard-App sidebar widget
/// (<c>https://lvh.me:5011/chatwoot-widget</c>). Provides customer lookup by
/// conversation link / phone / email, manual customer registration with
/// immediate sync back to Chatwoot, and manual ticket creation linked to a
/// Chatwoot conversation.
/// </summary>
/// <remarks>
/// The widget runs inside a Chatwoot iframe where the agent has no CRM JWT
/// session, so these endpoints are anonymous. They expose no destructive
/// operations; harden with a shared widget token or signed context in
/// production deployments.
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/chatwoot-widget")]
public class ChatwootWidgetController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IApplicationDbContext _db;
    private readonly ITicketNumberGenerator _ticketNumberGenerator;
    private readonly IChatwootClientService _chatwoot;
    private readonly ILogger<ChatwootWidgetController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatwootWidgetController"/> class.
    /// </summary>
    public ChatwootWidgetController(
        ISender sender,
        IApplicationDbContext db,
        ITicketNumberGenerator ticketNumberGenerator,
        IChatwootClientService chatwoot,
        ILogger<ChatwootWidgetController> logger)
    {
        _sender = sender;
        _db = db;
        _ticketNumberGenerator = ticketNumberGenerator;
        _chatwoot = chatwoot;
        _logger = logger;
    }

    /// <summary>
    /// Looks up the CRM customer for the current Chatwoot context. Resolution
    /// order: (1) ticket already linked to the conversation id, (2) phone
    /// number match (normalized, country-code tolerant), (3) email match.
    /// </summary>
    [HttpGet("details")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ChatwootWidgetDetailsResponse))]
    public async Task<IActionResult> GetDetails(
        [FromQuery] string? conversationId,
        [FromQuery] string? phone,
        [FromQuery] string? email,
        CancellationToken cancellationToken)
    {
        Guid? customerId = null;
        string? linkedTicketId = null;

        // 1) Previously linked ticket for this conversation.
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            var linked = await _db.Tickets
                .AsNoTracking()
                .Where(t => t.ChatwootConversationId == conversationId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { t.Id, t.CustomerId })
                .FirstOrDefaultAsync(cancellationToken);

            if (linked is not null)
            {
                customerId = linked.CustomerId;
                linkedTicketId = linked.Id;
            }
        }

        // 2) Phone lookup (exact candidates + suffix match for country codes).
        if (customerId is null && !string.IsNullOrWhiteSpace(phone))
        {
            var candidates = BuildPhoneCandidates(phone);
            var suffix = LastDigits(phone, 9);

            customerId = await _db.CustomerPhones
                .AsNoTracking()
                .Where(p => candidates.Contains(p.Phone) ||
                            (suffix != null && EF.Functions.Like(p.Phone, "%" + suffix)))
                .Select(p => (Guid?)p.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // 3) Email lookup.
        if (customerId is null && !string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim();
            customerId = await _db.Customers
                .AsNoTracking()
                .Where(c => c.Email != null && c.Email == normalizedEmail)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (customerId is null)
        {
            return Ok(new ChatwootWidgetDetailsResponse(false, null, null, new List<ChatwootTicketSummary>()));
        }

        var customer = await _sender.Send(new GetCustomerDetailsQuery(customerId.Value), cancellationToken);

        // Recent tickets give the agent immediate context in the sidebar.
        var recentTickets = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId.Value)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new ChatwootTicketSummary(
                t.Id,
                t.Title,
                t.Status.ToString(),
                t.Priority.ToString(),
                t.ChatwootConversationId,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new ChatwootWidgetDetailsResponse(true, customer, linkedTicketId, recentTickets));
    }

    /// <summary>
    /// Registers a new customer from the widget. If the phone number is
    /// already registered the existing customer is returned instead of
    /// violating the unique phone index. On success the contact identity
    /// (name / email / phone) is synchronized back to Chatwoot immediately.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ChatwootRegisterResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterCustomer(
        [FromBody] ChatwootRegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new { message = "Name and phone number are required." });
        }

        var normalizedPhone = NormalizePhone(request.Phone);

        // Guard against the unique index on CustomerPhones.Phone: if the number
        // already exists (any candidate form), reuse the existing customer.
        var candidates = BuildPhoneCandidates(request.Phone);
        var existingCustomerId = await _db.CustomerPhones
            .AsNoTracking()
            .Where(p => candidates.Contains(p.Phone))
            .Select(p => (Guid?)p.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);

        Guid customerId;
        bool alreadyExisted;
        if (existingCustomerId.HasValue)
        {
            customerId = existingCustomerId.Value;
            alreadyExisted = true;
        }
        else
        {
            customerId = await _sender.Send(new CreateCustomerCommand(
                request.Name.Trim(),
                string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                request.Province,
                request.City,
                request.AddressDetails,
                normalizedPhone), cancellationToken);
            alreadyExisted = false;
        }

        // Sync identity back to Chatwoot right away (fail-soft: registration
        // succeeds even when Chatwoot is unreachable / unconfigured).
        var chatwootSynced = false;
        if (!string.IsNullOrWhiteSpace(request.ChatwootContactId))
        {
            chatwootSynced = await _chatwoot.UpdateContactDetailsAsync(
                request.ChatwootContactId,
                request.Name.Trim(),
                string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                ToE164Egypt(normalizedPhone),
                cancellationToken);
        }

        var customer = await _sender.Send(new GetCustomerDetailsQuery(customerId), cancellationToken);
        return Ok(new ChatwootRegisterResponse(customerId, alreadyExisted, chatwootSynced, customer));
    }

    /// <summary>
    /// Creates a CRM ticket for a registered customer and links it to the
    /// Chatwoot conversation so future messages append to it automatically.
    /// </summary>
    [HttpPost("link-ticket")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ChatwootLinkTicketResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinkTicket(
        [FromBody] ChatwootLinkTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            return BadRequest(new { message = "ConversationId is required." });
        }

        var customerExists = await _db.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            return BadRequest(new { message = "Customer not found. Register the customer first." });
        }

        // Conversation-level idempotency: reuse an active linked ticket.
        var existing = await _db.Tickets
            .AsNoTracking()
            .Where(t => t.ChatwootConversationId == request.ConversationId &&
                        t.Status != TicketStatus.Resolved &&
                        t.Status != TicketStatus.Closed &&
                        t.Status != TicketStatus.Cancelled)
            .Select(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return Ok(new ChatwootLinkTicketResponse(existing, true));
        }

        // System actor for the history entry (widget runs without a CRM session).
        var systemUserId = await _db.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!systemUserId.HasValue)
        {
            return BadRequest(new { message = "No CRM users exist to attribute the ticket creation." });
        }

        var priority = request.Priority ?? TicketPriority.Medium;
        var now = DateTime.UtcNow;
        var slaHours = priority switch
        {
            TicketPriority.Critical => 4,
            TicketPriority.High => 24,
            TicketPriority.Medium => 72,
            _ => 120
        };

        var ticketId = await _ticketNumberGenerator.GenerateAsync(cancellationToken);
        _db.Tickets.Add(new Ticket
        {
            Id = ticketId,
            CustomerId = request.CustomerId,
            Title = string.IsNullOrWhiteSpace(request.Title)
                ? $"Chatwoot Conversation #{request.ConversationId}"
                : request.Title.Trim(),
            Description = request.Description ?? string.Empty,
            Category = request.Category ?? TicketCategory.GeneralInquiry,
            Priority = priority,
            Status = TicketStatus.New,
            ChatwootConversationId = request.ConversationId,
            SlaDeadline = now.AddHours(slaHours),
            CreatedAt = now,
            UpdatedAt = now
        });
        _db.TicketHistories.Add(new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            FromStatus = null,
            ToStatus = TicketStatus.New,
            ChangedById = systemUserId.Value,
            Note = $"Ticket created from Chatwoot widget and linked to conversation {request.ConversationId}.",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Widget linked new ticket {TicketId} to Chatwoot conversation {ConversationId}.",
            ticketId, request.ConversationId);

        return Ok(new ChatwootLinkTicketResponse(ticketId, false));
    }

    /* ================= Phone normalization helpers ================= */

    private static string NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        // Egyptian numbers arrive from Chatwoot as +20 10 1234 5678 → store local format 01012345678.
        if (digits.StartsWith("20") && digits.Length >= 12)
        {
            digits = "0" + digits[2..];
        }
        return digits;
    }

    private static List<string> BuildPhoneCandidates(string phone)
    {
        var raw = phone.Trim();
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        var candidates = new List<string> { raw, digits };

        if (digits.StartsWith("20") && digits.Length >= 12)
        {
            candidates.Add("0" + digits[2..]);   // +201012345678 → 01012345678
        }
        if (digits.StartsWith("0") && digits.Length >= 10)
        {
            candidates.Add("20" + digits[1..]);  // 01012345678 → 201012345678
            candidates.Add("+20" + digits[1..]); // → +201012345678
        }

        return candidates.Distinct().ToList();
    }

    private static string? LastDigits(string phone, int count)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= count ? digits[^count..] : null;
    }

    private static string ToE164Egypt(string localPhone)
    {
        var digits = new string(localPhone.Where(char.IsDigit).ToArray());
        return digits.StartsWith("0") && digits.Length >= 10
            ? "+20" + digits[1..]
            : (digits.StartsWith("20") ? "+" + digits : "+" + digits);
    }
}

/* ================= Widget contracts ================= */

/// <summary>Lookup result for the sidebar widget.</summary>
public record ChatwootWidgetDetailsResponse(
    bool Found,
    CustomerDetailsDto? Customer,
    string? LinkedTicketId,
    List<ChatwootTicketSummary> RecentTickets);

/// <summary>Compact ticket summary shown in the widget.</summary>
public record ChatwootTicketSummary(
    string Id,
    string Title,
    string Status,
    string Priority,
    string? ChatwootConversationId,
    DateTime CreatedAt);

/// <summary>Registration payload from the widget form.</summary>
public record ChatwootRegisterRequest(
    string Name,
    string? Email,
    string Phone,
    string? Province,
    string? City,
    string? AddressDetails,
    string? ChatwootContactId);

/// <summary>Registration result including immediate Chatwoot sync status.</summary>
public record ChatwootRegisterResponse(
    Guid CustomerId,
    bool AlreadyExisted,
    bool ChatwootSynced,
    CustomerDetailsDto Customer);

/// <summary>Manual ticket-link payload from the widget.</summary>
public record ChatwootLinkTicketRequest(
    Guid CustomerId,
    string ConversationId,
    string? Title,
    string? Description,
    TicketCategory? Category,
    TicketPriority? Priority);

/// <summary>Ticket-link result. <c>AlreadyLinked</c> signals idempotent reuse.</summary>
public record ChatwootLinkTicketResponse(string TicketId, bool AlreadyLinked);
