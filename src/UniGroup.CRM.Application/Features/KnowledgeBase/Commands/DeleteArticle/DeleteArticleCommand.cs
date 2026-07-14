using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;

namespace UniGroup.CRM.Application.Features.KnowledgeBase.Commands.DeleteArticle;

/// <summary>
/// Command to permanently delete a knowledge base guidance article (Admin only).
/// </summary>
/// <param name="Id">Identifier of the article to delete.</param>
public record DeleteArticleCommand(Guid Id) : IRequest<bool>;

/// <summary>
/// Handler for <see cref="DeleteArticleCommand"/>.
/// Returns false when the article does not exist (mapped to 404 by the controller).
/// </summary>
public class DeleteArticleCommandHandler : IRequestHandler<DeleteArticleCommand, bool>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteArticleCommandHandler"/> class.
    /// </summary>
    public DeleteArticleCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<bool> Handle(DeleteArticleCommand request, CancellationToken cancellationToken)
    {
        var article = await _context.KnowledgeBaseArticles
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (article == null)
        {
            return false;
        }

        _context.KnowledgeBaseArticles.Remove(article);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
