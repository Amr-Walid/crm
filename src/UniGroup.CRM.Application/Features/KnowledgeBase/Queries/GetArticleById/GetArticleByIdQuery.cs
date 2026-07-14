using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.KnowledgeBase.Common;

namespace UniGroup.CRM.Application.Features.KnowledgeBase.Queries.GetArticleById;

/// <summary>
/// Query that fetches a single knowledge base article by its identifier
/// (regardless of active state — used by the admin management screens).
/// </summary>
/// <param name="Id">The article identifier.</param>
public record GetArticleByIdQuery(Guid Id) : IRequest<KnowledgeBaseArticleDto?>;

/// <summary>
/// Handler for <see cref="GetArticleByIdQuery"/>.
/// </summary>
public class GetArticleByIdQueryHandler : IRequestHandler<GetArticleByIdQuery, KnowledgeBaseArticleDto?>
{
    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticleByIdQueryHandler"/> class.
    /// </summary>
    public GetArticleByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<KnowledgeBaseArticleDto?> Handle(GetArticleByIdQuery request, CancellationToken cancellationToken)
    {
        var article = await _context.KnowledgeBaseArticles
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        return article == null ? null : KnowledgeBaseArticleDto.FromEntity(article);
    }
}
