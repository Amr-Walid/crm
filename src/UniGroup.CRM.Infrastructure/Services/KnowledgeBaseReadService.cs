using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;
using UniGroup.CRM.Infrastructure.Data;

namespace UniGroup.CRM.Infrastructure.Services;

/// <summary>
/// EF Core 9 compiled-query implementation of <see cref="IKnowledgeBaseReadService"/>.
/// The category-based guidance lookup runs during EVERY inbound call, so the LINQ
/// expression tree is compiled exactly once per process via
/// <see cref="EF.CompileAsyncQuery{TContext,TParam1,TResult}(System.Linq.Expressions.Expression{Func{TContext,TParam1,TResult}})"/>
/// and reused for all subsequent executions, bypassing query-cache hashing and
/// expression-tree translation on the hot path.
/// </summary>
public class KnowledgeBaseReadService : IKnowledgeBaseReadService
{
    /// <summary>
    /// Process-wide compiled query: fetch the single ACTIVE article for a category.
    /// AsNoTracking is used because guidance articles are read-only on this path.
    /// The filtered unique index IX_KnowledgeBaseArticles_Category_Active guarantees
    /// at most one row matches, so FirstOrDefault is deterministic without ordering.
    /// </summary>
    private static readonly Func<ApplicationDbContext, TicketCategory, CancellationToken, Task<KnowledgeBaseArticle?>> GetActiveByCategoryCompiled =
        EF.CompileAsyncQuery((ApplicationDbContext context, TicketCategory category, CancellationToken ct) =>
            context.KnowledgeBaseArticles
                .AsNoTracking()
                .FirstOrDefault(a => a.Category == category && a.IsActive));

    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBaseReadService"/> class.
    /// </summary>
    public KnowledgeBaseReadService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<KnowledgeBaseArticle?> GetActiveArticleByCategoryAsync(TicketCategory category, CancellationToken cancellationToken)
        => GetActiveByCategoryCompiled(_context, category, cancellationToken);
}
