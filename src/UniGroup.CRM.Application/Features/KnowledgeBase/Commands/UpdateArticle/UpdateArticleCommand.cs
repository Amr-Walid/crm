using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.KnowledgeBase.Common;

namespace UniGroup.CRM.Application.Features.KnowledgeBase.Commands.UpdateArticle;

/// <summary>
/// Command to update an existing knowledge base guidance article (Admin only).
/// Content fields accept Markdown for rich UI rendering.
/// </summary>
/// <param name="Id">Identifier of the article to update.</param>
/// <param name="Title">New title (max 200 chars).</param>
/// <param name="QuestionsToAsk">Markdown intake questions.</param>
/// <param name="DiagnosisSteps">Markdown diagnosis steps.</param>
/// <param name="SuggestedAnswers">Markdown ready-made answers.</param>
/// <param name="EscalationConditions">Markdown escalation rules.</param>
/// <param name="Keywords">Optional comma-separated search keywords.</param>
/// <param name="IsActive">Whether the article should be served to agents.</param>
public record UpdateArticleCommand(
    Guid Id,
    string Title,
    string QuestionsToAsk,
    string DiagnosisSteps,
    string SuggestedAnswers,
    string EscalationConditions,
    string? Keywords,
    bool IsActive) : IRequest<bool>;

/// <summary>
/// Handler for <see cref="UpdateArticleCommand"/>. Validates content and, when
/// re-activating an article, enforces the single-active-article-per-category invariant.
/// Returns false when the article does not exist (mapped to 404 by the controller).
/// </summary>
public class UpdateArticleCommandHandler : IRequestHandler<UpdateArticleCommand, bool>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateArticleCommandHandler"/> class.
    /// </summary>
    public UpdateArticleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<bool> Handle(UpdateArticleCommand request, CancellationToken cancellationToken)
    {
        // Guard: reject empty/malformed content before touching the database.
        KnowledgeBaseArticleGuard.EnsureValidContent(
            request.Title,
            request.QuestionsToAsk,
            request.DiagnosisSteps,
            request.SuggestedAnswers,
            request.EscalationConditions,
            request.Keywords);

        var article = await _context.KnowledgeBaseArticles
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (article == null)
        {
            return false;
        }

        // Guard: activating this article must not collide with another active article
        // of the same category (the filtered unique index is the DB-level backstop).
        if (request.IsActive && !article.IsActive)
        {
            var otherActiveExists = await _context.KnowledgeBaseArticles
                .AnyAsync(a => a.Category == article.Category && a.IsActive && a.Id != article.Id, cancellationToken);

            if (otherActiveExists)
            {
                throw new ValidationException(
                    $"Another active knowledge base article already exists for category '{article.Category}'. " +
                    "Deactivate it first before activating this one.");
            }
        }

        article.Title = request.Title.Trim();
        article.QuestionsToAsk = request.QuestionsToAsk;
        article.DiagnosisSteps = request.DiagnosisSteps;
        article.SuggestedAnswers = request.SuggestedAnswers;
        article.EscalationConditions = request.EscalationConditions;
        article.Keywords = request.Keywords?.Trim() ?? string.Empty;
        article.IsActive = request.IsActive;
        article.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
