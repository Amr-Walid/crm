using System;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.KnowledgeBase.Common;

/// <summary>
/// Read-model DTO for a knowledge base guidance article.
/// Content fields (QuestionsToAsk, DiagnosisSteps, SuggestedAnswers, EscalationConditions)
/// are Markdown strings; the UI is expected to render them with a Markdown component.
/// </summary>
public record KnowledgeBaseArticleDto(
    Guid Id,
    TicketCategory Category,
    string CategoryName,
    string Title,
    string QuestionsToAsk,
    string DiagnosisSteps,
    string SuggestedAnswers,
    string EscalationConditions,
    string Keywords,
    bool IsActive,
    string ContentFormat,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    /// <summary>
    /// Maps a <see cref="KnowledgeBaseArticle"/> entity to its DTO representation.
    /// </summary>
    public static KnowledgeBaseArticleDto FromEntity(KnowledgeBaseArticle article) => new(
        article.Id,
        article.Category,
        article.Category.ToString(),
        article.Title,
        article.QuestionsToAsk,
        article.DiagnosisSteps,
        article.SuggestedAnswers,
        article.EscalationConditions,
        article.Keywords,
        article.IsActive,
        ContentFormat: "markdown",
        article.CreatedAt,
        article.UpdatedAt);
}
