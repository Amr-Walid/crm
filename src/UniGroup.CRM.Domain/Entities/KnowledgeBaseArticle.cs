using System;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Domain.Entities;

/// <summary>
/// A Knowledge Base guidance article shown to agents during live calls.
/// Each article is bound to a <see cref="TicketCategory"/> and carries interactive
/// call-flow guidance: questions to ask the customer, diagnosis steps to walk through,
/// ready-made suggested answers, and the conditions under which the call must be escalated.
/// Content fields (<see cref="QuestionsToAsk"/>, <see cref="DiagnosisSteps"/>,
/// <see cref="SuggestedAnswers"/>, <see cref="EscalationConditions"/>) are stored as
/// Markdown so the UI can render rich formatting (lists, bold, code, links).
/// A filtered unique index guarantees at most one <b>active</b> article per category.
/// </summary>
public class KnowledgeBaseArticle
{
    /// <summary>
    /// Gets or sets the unique identifier of the article.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the ticket category this guidance article applies to.
    /// Only one active article may exist per category (enforced by a filtered unique index).
    /// </summary>
    public TicketCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the human-readable title of the article (max 200 chars).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the intake questions the agent should ask the customer.
    /// Markdown-formatted for rich UI rendering.
    /// </summary>
    public string QuestionsToAsk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ordered diagnosis steps to walk through during the call.
    /// Markdown-formatted for rich UI rendering.
    /// </summary>
    public string DiagnosisSteps { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ready-made suggested answers the agent can read to the customer.
    /// Markdown-formatted for rich UI rendering.
    /// </summary>
    public string SuggestedAnswers { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the conditions under which the ticket/call must be escalated
    /// and the target department. Markdown-formatted for rich UI rendering.
    /// </summary>
    public string EscalationConditions { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional comma-separated search keywords/synonyms
    /// (e.g. "cracked, shattered, broken glass") boosting text search recall.
    /// </summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the article is active and served to agents.
    /// Inactive articles are retained as drafts/archives and excluded from call-flow guidance.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC timestamp when the article was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the UTC timestamp of the last modification, if any.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
