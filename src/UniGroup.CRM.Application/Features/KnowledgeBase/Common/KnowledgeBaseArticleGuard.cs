using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace UniGroup.CRM.Application.Features.KnowledgeBase.Common;

/// <summary>
/// Centralized input-validation guards for knowledge base article write operations.
/// Guarantees admins can never persist empty or malformed guidance articles.
/// Throws <see cref="ValidationException"/> with an aggregated, human-readable message.
/// </summary>
public static class KnowledgeBaseArticleGuard
{
    /// <summary>Maximum allowed title length (mirrors the DB column limit).</summary>
    public const int TitleMaxLength = 200;

    /// <summary>Maximum allowed keywords length (mirrors the DB column limit).</summary>
    public const int KeywordsMaxLength = 500;

    /// <summary>Maximum allowed length for each Markdown content block (sanity cap).</summary>
    public const int ContentMaxLength = 20_000;

    /// <summary>
    /// Validates the core content fields of an article. All content fields are required
    /// (whitespace-only is rejected) and bounded to protect the database and the UI renderer.
    /// </summary>
    /// <exception cref="ValidationException">Thrown when one or more fields are invalid.</exception>
    public static void EnsureValidContent(
        string title,
        string questionsToAsk,
        string diagnosisSteps,
        string suggestedAnswers,
        string escalationConditions,
        string? keywords)
    {
        var errors = new List<string>();

        ValidateRequired(errors, "Title", title, TitleMaxLength);
        ValidateRequired(errors, "QuestionsToAsk", questionsToAsk, ContentMaxLength);
        ValidateRequired(errors, "DiagnosisSteps", diagnosisSteps, ContentMaxLength);
        ValidateRequired(errors, "SuggestedAnswers", suggestedAnswers, ContentMaxLength);
        ValidateRequired(errors, "EscalationConditions", escalationConditions, ContentMaxLength);

        if (keywords is not null && keywords.Length > KeywordsMaxLength)
        {
            errors.Add($"Keywords must not exceed {KeywordsMaxLength} characters.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(string.Join(" ", errors));
        }
    }

    private static void ValidateRequired(List<string> errors, string fieldName, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required and cannot be empty or whitespace.");
            return;
        }

        if (value.Length > maxLength)
        {
            errors.Add($"{fieldName} must not exceed {maxLength} characters.");
        }
    }
}
