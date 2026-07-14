using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Domain.Entities;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Application.Common.Interfaces;

/// <summary>
/// High-performance read path for knowledge base call-flow guidance (Phase 7).
/// Backed by an EF Core 9 compiled query in the Infrastructure layer, because the
/// category-based article lookup happens on every single inbound call and must skip
/// per-invocation LINQ expression-tree compilation and query-cache lookups.
/// </summary>
public interface IKnowledgeBaseReadService
{
    /// <summary>
    /// Gets the single ACTIVE guidance article for a ticket category, or null when none exists.
    /// Executes as a pre-compiled, no-tracking query.
    /// </summary>
    Task<KnowledgeBaseArticle?> GetActiveArticleByCategoryAsync(TicketCategory category, CancellationToken cancellationToken);
}
