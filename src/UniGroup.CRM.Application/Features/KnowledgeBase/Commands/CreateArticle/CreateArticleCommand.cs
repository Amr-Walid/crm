using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.KnowledgeBase.Common;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.KnowledgeBase.Commands.CreateArticle;

/// <summary>
/// Command to create a new knowledge base guidance article (Admin only).
/// Content fields accept Markdown for rich UI rendering.
/// </summary>
/// <param name="Category">The ticket category this article guides.</param>
/// <param name="Title">Article title (max 200 chars).</param>
/// <param name="QuestionsToAsk">Markdown list of intake questions.</param>
/// <param name="DiagnosisSteps">Markdown ordered diagnosis steps.</param>
/// <param name="SuggestedAnswers">Markdown ready-made customer answers.</param>
/// <param name="EscalationConditions">Markdown escalation rules and target department.</param>
/// <param name="Keywords">Optional comma-separated search keywords/synonyms.</param>
/// <param name="IsActive">Whether the article is immediately served to agents (default true).</param>
public record CreateArticleCommand(
    TicketCategory Category,
    string Title,
    string QuestionsToAsk,
    string DiagnosisSteps,
    string SuggestedAnswers,
    string EscalationConditions,
    string? Keywords = null,
    bool IsActive = true) : IRequest<Guid>;

/// <summary>
/// Handler for <see cref="CreateArticleCommand"/>. Validates content via
/// <see cref="KnowledgeBaseArticleGuard"/> and pre-checks the single-active-article-per-category
/// invariant before persisting (the filtered unique index is the final DB-level backstop).
/// </summary>
public class CreateArticleCommandHandler : IRequestHandler<CreateArticleCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateArticleCommandHandler"/> class.
    /// </summary>
    public CreateArticleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Guid> Handle(CreateArticleCommand request, CancellationToken cancellationToken)
    {
        // Guard: reject empty/malformed content before touching the database.
        KnowledgeBaseArticleGuard.EnsureValidContent(
            request.Title,
            request.QuestionsToAsk,
            request.DiagnosisSteps,
            request.SuggestedAnswers,
            request.EscalationConditions,
            request.Keywords);

        // Guard: only one ACTIVE article per category. Friendly pre-check so admins
        // get a clear message instead of a raw unique-index violation.
        if (request.IsActive)
        {
            var activeExists = await _context.KnowledgeBaseArticles
                .AnyAsync(a => a.Category == request.Category && a.IsActive, cancellationToken);

            if (activeExists)
            {
                throw new ValidationException(
                    $"An active knowledge base article already exists for category '{request.Category}'. " +
                    "Deactivate or update the existing article instead.");
            }
        }

        var article = new KnowledgeBaseArticle
        {
            Id = Guid.NewGuid(),
            Category = request.Category,
            Title = request.Title.Trim(),
            QuestionsToAsk = request.QuestionsToAsk,
            DiagnosisSteps = request.DiagnosisSteps,
            SuggestedAnswers = request.SuggestedAnswers,
            EscalationConditions = request.EscalationConditions,
            Keywords = request.Keywords?.Trim() ?? string.Empty,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.KnowledgeBaseArticles.Add(article);
        await _context.SaveChangesAsync(cancellationToken);
        return article.Id;
    }
}
