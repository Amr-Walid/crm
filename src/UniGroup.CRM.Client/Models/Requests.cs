using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Client.Models;

/* ============================================================
   Client-side request contracts. These mirror the request records
   declared inside the API controllers (UniGroup.CRM.API.Controllers),
   which are not part of the shared Application project.
   All response/read DTOs come directly from UniGroup.CRM.Application.
   ============================================================ */

/// <summary>Login request body — mirrors <c>AuthController.LoginRequest</c>.</summary>
public record LoginRequest(string Email, string Password);

/// <summary>Registration request body — mirrors <c>AuthController.RegisterRequest</c>.</summary>
public record RegisterRequest(string Email, string Password, string FirstName, string LastName);

/// <summary>Create-customer body — mirrors <c>CustomersController.CreateCustomerRequest</c>.</summary>
public record CreateCustomerRequest(
    string Name,
    string? Email,
    string? Province,
    string? City,
    string? AddressDetails,
    string Phone);

/// <summary>Log-call body — mirrors <c>CallsController.LogCallRequest</c> (AgentId comes from JWT).</summary>
public record LogCallRequest(
    Guid? CustomerId,
    string Direction,
    string PhoneNumber,
    int DurationSeconds,
    string? Summary,
    string? RecordingUrl);

/// <summary>Create-brand body — mirrors <c>DevicesController.CreateBrandRequest</c>.</summary>
public record CreateBrandRequest(string Name);

/// <summary>Create-model body — mirrors <c>DevicesController.CreateModelRequest</c>.</summary>
public record CreateModelRequest(Guid BrandId, string Name);

/// <summary>Assign-device body — mirrors <c>DevicesController.AssignDeviceRequest</c>.</summary>
public record AssignDeviceRequest(
    Guid CustomerId,
    Guid ModelId,
    string? IMEI,
    string? SerialNumber,
    DateTime PurchaseDate,
    string? InvoiceNumber,
    DateTime? WarrantyExpiry);

/// <summary>Create-department body — mirrors <c>DepartmentsController.CreateDepartmentRequest</c>.</summary>
public record CreateDepartmentRequest(string Name, string? Description, bool IsActive);

/// <summary>Create-ticket body — mirrors <c>TicketsController.CreateTicketRequest</c>.</summary>
public record CreateTicketRequest(
    Guid CustomerId,
    Guid? CustomerDeviceId,
    string Title,
    string Description,
    TicketCategory Category,
    TicketPriority Priority);

/// <summary>Status-transition body — mirrors <c>TicketsController.TransitionStatusRequest</c>.</summary>
public record TransitionStatusRequest(TicketStatus NewStatus, string? Note);

/// <summary>Assign-ticket body — mirrors <c>TicketsController.AssignTicketRequest</c>.</summary>
public record AssignTicketRequest(Guid? AssignedToId, Guid? DepartmentId, string? Note);

/// <summary>Add-note body — mirrors <c>TicketsController.AddInternalNoteRequest</c>.</summary>
public record AddInternalNoteRequest(string Content);

/// <summary>CSAT submission body — mirrors <c>CsatController.SubmitSurveyRequest</c>.</summary>
public record SubmitSurveyRequest(string Token, int Rating, string? Feedback);

/// <summary>Create KB article body — mirrors <c>CreateArticleCommand</c> shape (without MediatR plumbing).</summary>
public record CreateArticleRequest(
    TicketCategory Category,
    string Title,
    string QuestionsToAsk,
    string DiagnosisSteps,
    string SuggestedAnswers,
    string EscalationConditions,
    string? Keywords,
    bool IsActive);

/// <summary>Update KB article body — mirrors <c>UpdateArticleCommand</c> shape.</summary>
public record UpdateArticleRequest(
    Guid Id,
    string Title,
    string QuestionsToAsk,
    string DiagnosisSteps,
    string SuggestedAnswers,
    string EscalationConditions,
    string? Keywords,
    bool IsActive);
