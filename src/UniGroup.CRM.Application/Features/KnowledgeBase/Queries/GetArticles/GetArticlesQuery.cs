using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Common.Interfaces;
using UniGroup.CRM.Application.Features.KnowledgeBase.Common;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Features.KnowledgeBase.Queries.GetArticles;

/// <summary>
/// Paginated admin/agent listing query with advanced text search.
/// The search is tokenized (split on whitespace) and every token must match
/// (AND semantics) against Title, DiagnosisSteps, QuestionsToAsk or Keywords —
/// compared case-insensitively so "screen" finds "Screen Damage" and "SCREEN".
/// </summary>
/// <param name="Page">1-based page number (clamped to >= 1).</param>
/// <param name="PageSize">Page size (clamped to 1..100).</param>
/// <param name="SearchTerm">Optional free-text search phrase.</param>
/// <param name="Category">Optional category filter.</param>
/// <param name="IsActive">Optional active-state filter.</param>
public record GetArticlesQuery(
    int Page = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    TicketCategory? Category = null,
    bool? IsActive = null) : IRequest<GetArticlesResult>;

/// <summary>
/// Paged result envelope for <see cref="GetArticlesQuery"/>.
/// </summary>
/// <param name="Articles">The current page of articles.</param>
/// <param name="TotalCount">Total number of articles matching the filters.</param>
/// <param name="Page">The effective (clamped) page number.</param>
/// <param name="PageSize">The effective (clamped) page size.</param>
/// <param name="TotalPages">Total number of pages available.</param>
public record GetArticlesResult(
    List<KnowledgeBaseArticleDto> Articles,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
/// Handler for <see cref="GetArticlesQuery"/>.
/// </summary>
public class GetArticlesQueryHandler : IRequestHandler<GetArticlesQuery, GetArticlesResult>
{
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetArticlesQueryHandler"/> class.
    /// </summary>
    public GetArticlesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<GetArticlesResult> Handle(GetArticlesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var query = _context.KnowledgeBaseArticles.AsNoTracking().AsQueryable();

        if (request.Category.HasValue)
        {
            query = query.Where(a => a.Category == request.Category.Value);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(a => a.IsActive == request.IsActive.Value);
        }

        // Advanced text search: tokenize the phrase; every token must match at least one
        // searchable field. ToLower() is translated to LOWER() by both the SQL Server and
        // SQLite providers, giving deterministic case-insensitive matching regardless of
        // the database collation.
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var tokens = request.SearchTerm
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .Distinct()
                .Take(8) // sanity cap: at most 8 tokens participate in the predicate
                .ToArray();

            foreach (var token in tokens)
            {
                var term = token; // avoid modified-closure capture
                query = query.Where(a =>
                    a.Title.ToLower().Contains(term) ||
                    a.DiagnosisSteps.ToLower().Contains(term) ||
                    a.QuestionsToAsk.ToLower().Contains(term) ||
                    a.Keywords.ToLower().Contains(term));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var articles = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new GetArticlesResult(
            articles.Select(KnowledgeBaseArticleDto.FromEntity).ToList(),
            totalCount,
            page,
            pageSize,
            totalPages);
    }
}
