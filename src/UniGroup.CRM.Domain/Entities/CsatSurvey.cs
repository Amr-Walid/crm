using System;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// Represents a customer satisfaction (CSAT) survey sent automatically when a
/// ticket is closed. One survey per ticket; secured by an opaque unique token
/// that expires 7 days after dispatch.
/// </summary>
public class CsatSurvey
{
    /// <summary>
    /// Gets or sets the unique identifier of the survey.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the related ticket (unique — one survey per ticket).
    /// </summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ticket navigation property.
    /// </summary>
    public virtual Ticket Ticket { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the customer taking the survey.
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer navigation property.
    /// </summary>
    public virtual Customer Customer { get; set; } = null!;

    /// <summary>
    /// Gets or sets the star rating score (1 to 5). Zero until submitted.
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Gets or sets the optional text feedback (max 1000 characters).
    /// </summary>
    public string? Feedback { get; set; }

    /// <summary>
    /// Gets or sets the secure unique opaque token used in the survey link.
    /// </summary>
    public string SurveyToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the survey link was dispatched.
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the UTC timestamp when the survey expires (SentAt + 7 days).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the customer's submission (null = pending).
    /// </summary>
    public DateTime? SubmittedAt { get; set; }
}
