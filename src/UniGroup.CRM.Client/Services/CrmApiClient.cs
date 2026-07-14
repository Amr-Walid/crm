using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using UniGroup.CRM.Application.Features.Auth.Common;
using UniGroup.CRM.Application.Features.Calls.Queries.Common;
using UniGroup.CRM.Application.Features.Csat.Commands.SubmitCsatSurvey;
using UniGroup.CRM.Application.Features.Csat.Queries.GetCsatReport;
using UniGroup.CRM.Application.Features.Customers.Queries.Common;
using UniGroup.CRM.Application.Features.Dashboards.Queries.Common;
using UniGroup.CRM.Application.Features.Departments.Queries.Common;
using UniGroup.CRM.Application.Features.KnowledgeBase.Common;
using UniGroup.CRM.Application.Features.KnowledgeBase.Queries.GetArticles;
using UniGroup.CRM.Application.Features.Tickets.Queries.Common;
using UniGroup.CRM.Application.Features.Tickets.Queries.GetTicketsList;
using UniGroup.CRM.Client.Models;
using UniGroup.CRM.Domain.Enums;

namespace UniGroup.CRM.Client.Services;

/// <summary>
/// Exception carrying the HTTP status and server-provided message for failed API calls.
/// </summary>
public class ApiException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ApiException"/> class.</summary>
    public ApiException(HttpStatusCode status, string message) : base(message) => Status = status;

    /// <summary>The HTTP status code of the failed response.</summary>
    public HttpStatusCode Status { get; }
}

/// <summary>
/// Typed API client for all UniGroup CRM REST endpoints. Injects the JWT bearer
/// token from the auth state provider, converts errors into <see cref="ApiException"/>,
/// and forces sign-out + redirect on 401 responses.
/// </summary>
public class CrmApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly JwtAuthenticationStateProvider _auth;
    private readonly NavigationManager _nav;

    /// <summary>Initializes a new instance of the <see cref="CrmApiClient"/> class.</summary>
    public CrmApiClient(HttpClient http, JwtAuthenticationStateProvider auth, NavigationManager nav)
    {
        _http = http;
        _auth = auth;
        _nav = nav;
    }

    /// <summary>The API base URL (used to resolve relative storage URLs, e.g. attachments).</summary>
    public string BaseUrl => _http.BaseAddress?.ToString() ?? string.Empty;

    /* ================= Auth ================= */

    /// <summary>POST /api/auth/login.</summary>
    public Task<AuthResponse> LoginAsync(LoginRequest request) =>
        PostAsync<AuthResponse>("api/auth/login", request, authorize: false);

    /// <summary>POST /api/auth/register.</summary>
    public Task<AuthResponse> RegisterAsync(RegisterRequest request) =>
        PostAsync<AuthResponse>("api/auth/register", request, authorize: false);

    /* ================= Customers ================= */

    /// <summary>POST /api/customers → new customer id.</summary>
    public Task<Guid> CreateCustomerAsync(CreateCustomerRequest request) =>
        PostAsync<Guid>("api/customers", request);

    /// <summary>GET /api/customers/{id}.</summary>
    public Task<CustomerDetailsDto> GetCustomerAsync(Guid id) =>
        GetAsync<CustomerDetailsDto>($"api/customers/{id}");

    /// <summary>GET /api/customers/search?searchTerm=.</summary>
    public Task<List<CustomerDetailsDto>> SearchCustomersAsync(string searchTerm) =>
        GetAsync<List<CustomerDetailsDto>>($"api/customers/search?searchTerm={Uri.EscapeDataString(searchTerm)}");

    /// <summary>GET /api/search?q= (unified system search).</summary>
    public Task<List<CustomerDetailsDto>> SystemSearchAsync(string q) =>
        GetAsync<List<CustomerDetailsDto>>($"api/search?q={Uri.EscapeDataString(q)}");

    /* ================= Calls ================= */

    /// <summary>POST /api/calls → call id.</summary>
    public Task<Guid> LogCallAsync(LogCallRequest request) =>
        PostAsync<Guid>("api/calls", request);

    /// <summary>GET /api/calls/caller-id?phoneNumber= — null when caller unknown.</summary>
    public Task<CustomerDetailsDto?> GetCallerProfileAsync(string phoneNumber) =>
        GetAsync<CustomerDetailsDto?>($"api/calls/caller-id?phoneNumber={Uri.EscapeDataString(phoneNumber)}");

    /// <summary>GET /api/calls/history/{customerId}.</summary>
    public Task<List<CallDto>> GetCallHistoryAsync(Guid customerId, int page = 1, int pageSize = 20) =>
        GetAsync<List<CallDto>>($"api/calls/history/{customerId}?page={page}&pageSize={pageSize}");

    /* ================= Devices ================= */

    /// <summary>POST /api/devices/brands → brand id.</summary>
    public Task<Guid> CreateBrandAsync(CreateBrandRequest request) =>
        PostAsync<Guid>("api/devices/brands", request);

    /// <summary>POST /api/devices/models → model id.</summary>
    public Task<Guid> CreateModelAsync(CreateModelRequest request) =>
        PostAsync<Guid>("api/devices/models", request);

    /// <summary>POST /api/devices/assign → customer-device id.</summary>
    public Task<Guid> AssignDeviceAsync(AssignDeviceRequest request) =>
        PostAsync<Guid>("api/devices/assign", request);

    /* ================= Departments ================= */

    /// <summary>GET /api/departments.</summary>
    public Task<List<DepartmentDto>> GetDepartmentsAsync() =>
        GetAsync<List<DepartmentDto>>("api/departments");

    /// <summary>POST /api/departments → department id.</summary>
    public Task<Guid> CreateDepartmentAsync(CreateDepartmentRequest request) =>
        PostAsync<Guid>("api/departments", request);

    /* ================= Tickets ================= */

    /// <summary>POST /api/tickets → new ticket id (e.g. "TKT-2026-00042").</summary>
    public Task<string> CreateTicketAsync(CreateTicketRequest request) =>
        PostAsync<string>("api/tickets", request);

    /// <summary>GET /api/tickets/{ticketId}.</summary>
    public Task<TicketDetailsDto> GetTicketAsync(string ticketId) =>
        GetAsync<TicketDetailsDto>($"api/tickets/{Uri.EscapeDataString(ticketId)}");

    /// <summary>GET /api/tickets with filters and paging.</summary>
    public Task<PagedResult<TicketSummaryDto>> GetTicketsAsync(
        TicketStatus? status = null,
        TicketPriority? priority = null,
        Guid? departmentId = null,
        Guid? assignedToId = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int page = 1,
        int pageSize = 10)
    {
        var qs = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (status.HasValue) qs.Add($"status={(int)status.Value}");
        if (priority.HasValue) qs.Add($"priority={(int)priority.Value}");
        if (departmentId.HasValue) qs.Add($"departmentId={departmentId}");
        if (assignedToId.HasValue) qs.Add($"assignedToId={assignedToId}");
        if (dateFrom.HasValue) qs.Add($"dateFrom={dateFrom:yyyy-MM-dd}");
        if (dateTo.HasValue) qs.Add($"dateTo={dateTo:yyyy-MM-dd}");

        return GetAsync<PagedResult<TicketSummaryDto>>($"api/tickets?{string.Join('&', qs)}");
    }

    /// <summary>GET /api/tickets/my?status=.</summary>
    public Task<List<TicketSummaryDto>> GetMyTicketsAsync(TicketStatus? status = null) =>
        GetAsync<List<TicketSummaryDto>>(
            status.HasValue ? $"api/tickets/my?status={(int)status.Value}" : "api/tickets/my");

    /// <summary>PATCH /api/tickets/{id}/status.</summary>
    public Task TransitionTicketStatusAsync(string ticketId, TransitionStatusRequest request) =>
        PatchAsync($"api/tickets/{Uri.EscapeDataString(ticketId)}/status", request);

    /// <summary>PATCH /api/tickets/{id}/assign.</summary>
    public Task AssignTicketAsync(string ticketId, AssignTicketRequest request) =>
        PatchAsync($"api/tickets/{Uri.EscapeDataString(ticketId)}/assign", request);

    /// <summary>POST /api/tickets/{id}/notes → note id.</summary>
    public Task<Guid> AddInternalNoteAsync(string ticketId, AddInternalNoteRequest request) =>
        PostAsync<Guid>($"api/tickets/{Uri.EscapeDataString(ticketId)}/notes", request);

    /// <summary>POST /api/tickets/{id}/attachments (multipart) → attachment id.</summary>
    public async Task<Guid> UploadAttachmentAsync(string ticketId, Stream fileStream, string fileName, string contentType, long maxBytes = 10 * 1024 * 1024)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        content.Add(streamContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/tickets/{Uri.EscapeDataString(ticketId)}/attachments")
        {
            Content = content,
        };
        await AddAuthAsync(request);

        var response = await _http.SendAsync(request);
        return await ReadAsync<Guid>(response);
    }

    /* ================= Dashboard (Admin / Team Leader) ================= */

    /// <summary>GET /api/dashboard/summary.</summary>
    public Task<DashboardSummaryDto> GetDashboardSummaryAsync(DateTime? date = null) =>
        GetAsync<DashboardSummaryDto>(date.HasValue
            ? $"api/dashboard/summary?date={date:yyyy-MM-dd}"
            : "api/dashboard/summary");

    /// <summary>GET /api/dashboard/agent-performance.</summary>
    public Task<List<AgentPerformanceDto>> GetAgentPerformanceAsync(DateTime? from = null, DateTime? to = null)
    {
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"dateFrom={from:yyyy-MM-dd}");
        if (to.HasValue) qs.Add($"dateTo={to:yyyy-MM-dd}");
        var suffix = qs.Count > 0 ? "?" + string.Join('&', qs) : string.Empty;
        return GetAsync<List<AgentPerformanceDto>>($"api/dashboard/agent-performance{suffix}");
    }

    /// <summary>GET /api/dashboard/device-failures.</summary>
    public Task<List<DeviceFailureReportDto>> GetDeviceFailuresAsync(DateTime? from = null, DateTime? to = null)
    {
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"dateFrom={from:yyyy-MM-dd}");
        if (to.HasValue) qs.Add($"dateTo={to:yyyy-MM-dd}");
        var suffix = qs.Count > 0 ? "?" + string.Join('&', qs) : string.Empty;
        return GetAsync<List<DeviceFailureReportDto>>($"api/dashboard/device-failures{suffix}");
    }

    /// <summary>GET /api/dashboard/call-volume.</summary>
    public Task<List<HourlyCallVolumeDto>> GetCallVolumeAsync(DateTime? date = null) =>
        GetAsync<List<HourlyCallVolumeDto>>(date.HasValue
            ? $"api/dashboard/call-volume?date={date:yyyy-MM-dd}"
            : "api/dashboard/call-volume");

    /// <summary>GET /api/dashboard/tickets-by-status.</summary>
    public Task<Dictionary<string, int>> GetTicketsByStatusAsync() =>
        GetAsync<Dictionary<string, int>>("api/dashboard/tickets-by-status");

    /* ================= Reports (Admin) ================= */

    /// <summary>GET /api/reports/agents/export → CSV bytes + file name.</summary>
    public async Task<(byte[] Content, string FileName)> ExportAgentReportAsync(DateTime? from = null, DateTime? to = null)
    {
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"dateFrom={from:yyyy-MM-dd}");
        if (to.HasValue) qs.Add($"dateTo={to:yyyy-MM-dd}");
        var suffix = qs.Count > 0 ? "?" + string.Join('&', qs) : string.Empty;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/reports/agents/export{suffix}");
        await AddAuthAsync(request);
        var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response);

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? $"agent_report_{DateTime.UtcNow:yyyyMMdd}.csv";

        return (await response.Content.ReadAsByteArrayAsync(), fileName);
    }

    /* ================= CSAT ================= */

    /// <summary>POST /api/surveys/submit (anonymous, token-secured).</summary>
    public async Task<SubmitCsatSurveyResult> SubmitSurveyAsync(SubmitSurveyRequest request)
    {
        // 400 responses still carry a SubmitCsatSurveyResult body — read it either way.
        using var msg = new HttpRequestMessage(HttpMethod.Post, "api/surveys/submit")
        {
            Content = JsonContent.Create(request, options: Json),
        };
        var response = await _http.SendAsync(msg);
        var result = await response.Content.ReadFromJsonAsync<SubmitCsatSurveyResult>(Json);
        return result ?? new SubmitCsatSurveyResult(false, "Unexpected empty response.");
    }

    /// <summary>GET /api/surveys/report (Admin / Team Leader).</summary>
    public Task<CsatReportDto> GetCsatReportAsync(DateTime? from = null, DateTime? to = null)
    {
        var qs = new List<string>();
        if (from.HasValue) qs.Add($"from={from:yyyy-MM-dd}");
        if (to.HasValue) qs.Add($"to={to:yyyy-MM-dd}");
        var suffix = qs.Count > 0 ? "?" + string.Join('&', qs) : string.Empty;
        return GetAsync<CsatReportDto>($"api/surveys/report{suffix}");
    }

    /* ================= Knowledge Base ================= */

    /// <summary>GET /api/knowledge-base (paged list with filters).</summary>
    public Task<GetArticlesResult> GetArticlesAsync(int page = 1, int pageSize = 10, string? search = null, TicketCategory? category = null, bool? isActive = null)
    {
        var qs = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (category.HasValue) qs.Add($"category={(int)category.Value}");
        if (isActive.HasValue) qs.Add($"isActive={isActive.Value.ToString().ToLowerInvariant()}");
        return GetAsync<GetArticlesResult>($"api/knowledge-base?{string.Join('&', qs)}");
    }

    /// <summary>GET /api/knowledge-base/category/{category} — active article, or null on 404.</summary>
    public async Task<KnowledgeBaseArticleDto?> GetArticleByCategoryAsync(TicketCategory category)
    {
        try
        {
            return await GetAsync<KnowledgeBaseArticleDto>($"api/knowledge-base/category/{(int)category}");
        }
        catch (ApiException ex) when (ex.Status == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>GET /api/knowledge-base/{id}.</summary>
    public Task<KnowledgeBaseArticleDto> GetArticleAsync(Guid id) =>
        GetAsync<KnowledgeBaseArticleDto>($"api/knowledge-base/{id}");

    /// <summary>POST /api/knowledge-base (Admin) → article id.</summary>
    public Task<Guid> CreateArticleAsync(CreateArticleRequest request) =>
        PostAsync<Guid>("api/knowledge-base", request);

    /// <summary>PUT /api/knowledge-base/{id} (Admin).</summary>
    public async Task UpdateArticleAsync(UpdateArticleRequest request)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Put, $"api/knowledge-base/{request.Id}")
        {
            Content = JsonContent.Create(request, options: Json),
        };
        await AddAuthAsync(msg);
        var response = await _http.SendAsync(msg);
        await EnsureSuccessAsync(response);
    }

    /// <summary>DELETE /api/knowledge-base/{id} (Admin).</summary>
    public async Task DeleteArticleAsync(Guid id)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Delete, $"api/knowledge-base/{id}");
        await AddAuthAsync(msg);
        var response = await _http.SendAsync(msg);
        await EnsureSuccessAsync(response);
    }

    /* ================= Core helpers ================= */

    private async Task<T> GetAsync<T>(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        await AddAuthAsync(request);
        var response = await _http.SendAsync(request);
        return await ReadAsync<T>(response);
    }

    private async Task<T> PostAsync<T>(string path, object body, bool authorize = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, body.GetType(), options: Json),
        };
        if (authorize)
        {
            await AddAuthAsync(request);
        }

        var response = await _http.SendAsync(request);
        return await ReadAsync<T>(response);
    }

    private async Task PatchAsync(string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(body, body.GetType(), options: Json),
        };
        await AddAuthAsync(request);
        var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    private Task AddAuthAsync(HttpRequestMessage request)
    {
        var session = _auth.Session;
        if (session is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        }

        return Task.CompletedTask;
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);

        var raw = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return default!;
        }

        // Plain-string payloads (e.g. ticket ids) may come back JSON-quoted or raw.
        if (typeof(T) == typeof(string))
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith('"'))
            {
                return (T)(object)JsonSerializer.Deserialize<string>(trimmed, Json)!;
            }

            return (T)(object)trimmed;
        }

        return JsonSerializer.Deserialize<T>(raw, Json)!;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Session invalid or expired — force sign-out and return to login.
            await _auth.SignOutAsync();
            _nav.NavigateTo("/login?expired=1");
            throw new ApiException(HttpStatusCode.Unauthorized, "Session expired. Please sign in again.");
        }

        var message = response.StatusCode == HttpStatusCode.Forbidden
            ? "You do not have permission to perform this action."
            : await ExtractErrorAsync(response);

        throw new ApiException(response.StatusCode, message);
    }

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("message", out var m))
                    {
                        return m.GetString() ?? $"Request failed ({(int)response.StatusCode}).";
                    }

                    if (doc.RootElement.TryGetProperty("title", out var t))
                    {
                        return t.GetString() ?? $"Request failed ({(int)response.StatusCode}).";
                    }
                }
            }
        }
        catch
        {
            // fall through to generic message
        }

        return $"Request failed ({(int)response.StatusCode}).";
    }
}
