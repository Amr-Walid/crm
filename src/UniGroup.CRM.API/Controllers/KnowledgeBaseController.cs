using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using UniGroup.CRM.Application.Features.KnowledgeBase.Commands.CreateArticle;
using UniGroup.CRM.Application.Features.KnowledgeBase.Commands.DeleteArticle;
using UniGroup.CRM.Application.Features.KnowledgeBase.Commands.UpdateArticle;
using UniGroup.CRM.Application.Features.KnowledgeBase.Common;
using UniGroup.CRM.Application.Features.KnowledgeBase.Queries.GetArticleByCategory;
using UniGroup.CRM.Application.Features.KnowledgeBase.Queries.GetArticleById;
using UniGroup.CRM.Application.Features.KnowledgeBase.Queries.GetArticles;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.API.Controllers;

/// <summary>
/// Endpoints for the Knowledge Base &amp; Call Flow Guidance module (Phase 7).
/// Read endpoints serve interactive guidance (questions, diagnosis steps, suggested
/// answers, escalation conditions — all Markdown-formatted) to agents during live calls.
/// Write endpoints are restricted to the Admin role.
/// </summary>
[ApiController]
[Route("api/knowledge-base")]
[Authorize]
public class KnowledgeBaseController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBaseController"/> class.
    /// </summary>
    public KnowledgeBaseController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Creates a new knowledge base guidance article. Admin only.
    /// Returns 400 with a descriptive message for empty/malformed content
    /// or when an active article already exists for the category.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateArticleCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _sender.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing knowledge base article. Admin only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateArticleCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest(new { message = "Mismatched article ID between route and body." });
        }

        try
        {
            var success = await _sender.Send(command, cancellationToken);
            if (!success)
            {
                return NotFound();
            }

            return Ok();
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Permanently deletes a knowledge base article. Admin only.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var success = await _sender.Send(new DeleteArticleCommand(id), cancellationToken);
        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Gets the ACTIVE guidance article for a ticket category — the call-flow hot path
    /// invoked on every inbound call once the agent selects the ticket category.
    /// Served by an EF Core 9 compiled query. All authenticated roles may read.
    /// </summary>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(KnowledgeBaseArticleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCategory(TicketCategory category, CancellationToken cancellationToken)
    {
        var article = await _sender.Send(new GetArticleByCategoryQuery(category), cancellationToken);
        if (article == null)
        {
            return NotFound(new { message = "No guidelines article found for this category." });
        }

        return Ok(article);
    }

    /// <summary>
    /// Gets a single article by its identifier (any active state — used by admin screens).
    /// All authenticated roles may read.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(KnowledgeBaseArticleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var article = await _sender.Send(new GetArticleByIdQuery(id), cancellationToken);
        if (article == null)
        {
            return NotFound();
        }

        return Ok(article);
    }

    /// <summary>
    /// Gets a paginated list of articles with optional tokenized case-insensitive text
    /// search (title, diagnosis steps, questions, keywords), category and active filters.
    /// All authenticated roles may read.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(GetArticlesResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] TicketCategory? category = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetArticlesQuery(page, pageSize, search, category, isActive), cancellationToken);
        return Ok(result);
    }
}
