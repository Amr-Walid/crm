using MediatR;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.KnowledgeBase.Common;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.KnowledgeBase.Queries.GetArticleByCategory;

/// <summary>
/// Query that fetches the single ACTIVE guidance article for a ticket category.
/// This is the call-flow hot path: it runs on EVERY inbound call once the agent
/// selects a category, so it is served by <see cref="IKnowledgeBaseReadService"/>
/// which executes an EF Core 9 pre-compiled, no-tracking query.
/// </summary>
/// <param name="Category">The ticket category to fetch guidance for.</param>
public record GetArticleByCategoryQuery(TicketCategory Category) : IRequest<KnowledgeBaseArticleDto?>;

/// <summary>
/// Handler for <see cref="GetArticleByCategoryQuery"/>.
/// </summary>
public class GetArticleByCategoryQueryHandler : IRequestHandler<GetArticleByCategoryQuery, KnowledgeBaseArticleDto?>
{
    private readonly IKnowledgeBaseReadService _readService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticleByCategoryQueryHandler"/> class.
    /// </summary>
    public GetArticleByCategoryQueryHandler(IKnowledgeBaseReadService readService)
    {
        _readService = readService;
    }

    /// <inheritdoc />
    public async Task<KnowledgeBaseArticleDto?> Handle(GetArticleByCategoryQuery request, CancellationToken cancellationToken)
    {
        var article = await _readService.GetActiveArticleByCategoryAsync(request.Category, cancellationToken);
        return article == null ? null : KnowledgeBaseArticleDto.FromEntity(article);
    }
}
