# Phase 6 Implementation Plan — Notifications, CSAT, Audit Trail & Chatwoot Integration

**Date:** 2026-07-14 | **Branch:** `genspark_ai_developer` | **Status:** IN PROGRESS 🔄

Source documents: `docs/superpowers/specs/2026-07-08-phase6-chatwoot-audit-notifications-design.md`,
`docs/superpowers/plans/2026-07-08-phase6-chatwoot-audit-notifications.md`,
`modules_design_blueprint.md` §6, `database_design.md` §8-9, `project_audit_report.md` (Phase 6 roadmap).

---

## 0. Design Decisions & Doc Reconciliation

| Topic | Decision | Rationale |
|---|---|---|
| `CsatSurvey.TicketId` type | `string` (nvarchar(450)) | Ticket PK is `T-YYYY-NNNNN` string — matches spec §3.2 & database_design.md; blueprint's `Guid` snippet is outdated |
| `AuditLog` client info | EF Core 9 **Complex Type** `ClientInfo { IpAddress, UserAgent }` → columns `ClientInfo_IpAddress`, `ClientInfo_UserAgent` | Spec §3.1 + database_design.md require Complex Types; complex type is required instance with nullable members (EF 9 does not allow nullable complex types) |
| `CsatSurvey.ExpiresAt` | Included (`SentAt + 7 days`) | Spec §3.2; database_design.md will be updated to include it |
| HMAC encoding | Base64 of HMAC-SHA256 over raw body, header `X-Chatwoot-Signature` | Reference implementation in plan Task 3; constant-time compare via `CryptographicOperations.FixedTimeEquals` |
| Webhook secret source | `Chatwoot:WebhookSecret` config key with `CHATWOOT_WEBHOOK_SECRET` env-var fallback | .NET config binding already maps `Chatwoot__WebhookSecret`; explicit env fallback kept for plan compatibility |
| Domain events | MediatR `INotification` published in-process after `SaveChangesAsync` from existing command handlers | Project has no domain-event infra; MediatR publish keeps CQRS intact with zero new plumbing |
| Audit exclusions | `AuditLog`, `ProcessedWebhookEvent`, `NotificationLog` | Spec guardrail 8.1 lists the first two; `NotificationLog` also excluded to avoid audit noise from the notification engine itself |
| Retry policy | Polly v8 `AsyncRetryPolicy` — 3 retries, exponential backoff 2s/4s/8s | Spec guardrail 8.4 |
| Migration strategy | `dotnet ef migrations add AddPhase6Entities` (SQL Server, design-time only in sandbox); SQLite test provider keeps using `EnsureCreated()` | Sandbox has no SQL Server; migration is generated and committed for production use |

---

## 1. Database Migrations

**One migration: `AddPhase6Entities`** creating 4 tables:

### `AuditLogs`
- `Id` uniqueidentifier PK
- `UserId` uniqueidentifier NULL, FK → AspNetUsers **OnDelete(SetNull)**
- `Action` nvarchar(100) NOT NULL, `TableName` nvarchar(100) NOT NULL, `RecordId` nvarchar(100) NOT NULL
- `BeforeValue` / `AfterValue` nvarchar(max) NULL (JSON)
- `ClientInfo_IpAddress` nvarchar(100) NULL, `ClientInfo_UserAgent` nvarchar(max) NULL (Complex Type)
- `CreatedAt` datetime2 NOT NULL → **INDEX `IX_AuditLogs_CreatedAt`**

### `CsatSurveys`
- `Id` uniqueidentifier PK
- `TicketId` nvarchar(450) NOT NULL, FK → Tickets **OnDelete(Cascade)** → **UNIQUE INDEX**
- `CustomerId` uniqueidentifier NOT NULL, FK → Customers **OnDelete(Cascade)** *(NoAction on SQL Server if multiple cascade paths conflict — verified: Ticket→Customer is Restrict, so Cascade is safe)*
- `Rating` int NOT NULL (validated 1–5 at Application layer), `Feedback` nvarchar(1000) NULL
- `SurveyToken` nvarchar(450) NOT NULL → **UNIQUE INDEX**
- `SentAt`, `ExpiresAt` datetime2 NOT NULL; `SubmittedAt` datetime2 NULL

### `ProcessedWebhookEvents`
- `EventId` nvarchar(450) **PK**
- `ProcessedAt` datetime2 NOT NULL

### `NotificationLogs`
- `Id` uniqueidentifier PK
- `RecipientType`, `RecipientId`, `Channel`, `TemplateType`, `Status` nvarchar(100) NOT NULL
- `MessageContent`, `ErrorMessage` nvarchar(max) NULL
- `SentAt` datetime2 NOT NULL → **INDEX** (report queries)

---

## 2. Audit Pipeline Design (Interceptor → Channel → Batch Consumer)

```
SaveChangesAsync ──► AuditSaveChangesInterceptor (SavingChangesAsync/SavingChanges)
                        │ captures Added/Modified/Deleted entries
                        │ skips AuditLog / ProcessedWebhookEvent / NotificationLog
                        │ UserId ← ICurrentUserService (JWT), IP/UA ← IHttpContextAccessor
                        ▼
                  AuditLogChannel (Bounded, capacity 5,000, FullMode.Wait, SingleReader)
                        ▼
                  AuditLogProcessor : BackgroundService
                        │ buffers up to 100 records or drains reader
                        │ Polly retry 3x (2s/4s/8s) around bulk AddRange+SaveChanges
                        │ IServiceScopeFactory → scoped ApplicationDbContext per flush
                        │ Graceful shutdown: on cancellation, drains channel and flushes
                        ▼
                  AuditLogs table (bulk insert)

AuditLogArchiverService : BackgroundService (PeriodicTimer 24h)
    → ExecuteDeleteAsync(a => a.CreatedAt < UtcNow.AddMonths(-AuditLogRetentionMonths))  // default 6
```

Guardrails honored: interceptor never re-enters (excluded entities), background services resolve
DbContext via `IServiceScopeFactory` (never ctor-injected), channel is bounded.

**Snapshot semantics:** the interceptor serializes property values **before** the save completes
(Added → AfterValue from CurrentValues; Modified → changed-props diff Before/After; Deleted →
BeforeValue from OriginalValues). RecordId = primary key `CurrentValue` string.

---

## 3. Webhook Ingest Design (Controller → Channel → Consumer)

```
POST /api/webhooks/chatwoot  [AllowAnonymous]
    │ Request.EnableBuffering() → read raw body → rewind
    │ HMAC-SHA256(body, secret) Base64 == X-Chatwoot-Signature ? (FixedTimeEquals)
    │   missing header → 400, bad signature → 401, no secret configured → 500
    ▼
ChatwootWebhookChannel (Bounded, capacity 10,000, FullMode.Wait) → 202 Accepted (<10ms)
    ▼
ChatwootWebhookProcessor : BackgroundService (Polly retry 3x)
    1. Parse JSON → require event == "message_created" && message_type == "incoming"
    2. EventId = payload "id" → idempotency lookup in ProcessedWebhookEvents → duplicate? discard
    3. Resolve customer by sender.phone_number (create Customer + primary CustomerPhone if missing)
    4. Find ACTIVE ticket (not Resolved/Closed/Cancelled) with same ChatwootConversationId
       → exists: append InternalNote with message content
       → none: create Ticket (Category=GeneralInquiry, Priority=Medium, SLA 72h,
         Title="Chatwoot Conversation - [convId]", Id via ITicketNumberGenerator) + initial TicketHistory
    5. Insert ProcessedWebhookEvent — SAME SaveChangesAsync (single transaction with business writes)
    Graceful shutdown: drains channel on cancellation before exit.
```

---

## 4. Notification Engine & CSAT Loop

```
Command handlers publish MediatR notifications after successful SaveChanges:
  AssignTicketCommand            → TicketAssignedEvent
  TransitionTicketStatusCommand  → TicketResolvedEvent (→Resolved) / TicketClosedEvent (→Closed)
  EscalateOverdueTicketsCommand  → SlaBreachedEvent (per escalated ticket)

Event handlers (Application layer) → SendNotificationCommand (the engine):
  TicketAssignedEvent  → InApp + Email to assigned agent      (TemplateType=TicketAssigned)
  TicketResolvedEvent  → Chatwoot message to customer          (TemplateType=TicketResolved)
  SlaBreachedEvent     → InApp + Email to Team Leaders/Admins  (TemplateType=SlaBreached)
  TicketClosedEvent    → 1) create CsatSurvey (unique token, ExpiresAt = SentAt+7d, one per ticket)
                         2) Chatwoot/WhatsApp message with survey link (TemplateType=CsatSurvey)

SendNotificationCommand(RecipientType, RecipientId, Channel, TemplateType, MessageContent):
  Email    → IEmailService (SMTP; unconfigured host ⇒ Status=Failed + ErrorMessage)
  WhatsApp → IChatwootClientService.SendMessageAsync (unconfigured ⇒ Failed + error)
  InApp    → always logged as Sent (DB record is the in-app store)
  ⇒ every dispatch writes a NotificationLog row (Sent/Failed + content + error)
```

**CSAT submission flow** (`POST /api/surveys/submit?token=...`, anonymous):
1. Find survey by unique `SurveyToken` → 404 if missing
2. `SubmittedAt != null` → 400 "already submitted"
3. `UtcNow > ExpiresAt` → 400 "expired"
4. Validate Rating ∈ [1..5] → 400 otherwise
5. Persist Rating/Feedback/SubmittedAt → 200

---

## 5. New API Surface

| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/api/webhooks/chatwoot` | Anonymous + HMAC | Webhook ingest → 202 |
| POST | `/api/surveys/submit` | Anonymous + SurveyToken | CSAT submission |
| GET | `/api/surveys/report` | Admin, Team Leader | CSAT stats (avg rating, response rate, list) |
| GET | `/api/surveys/ticket/{ticketId}` | Admin | Survey details incl. token (support tooling & tests) |
| GET | `/api/audit-logs` | Admin | Paged/filtered audit search (tableName, action, userId, date range) |
| GET | `/api/audit-logs/{id}` | Admin | Single audit entry with Before/After JSON |
| GET | `/api/notifications/logs` | Admin | Paged notification delivery log |

---

## 6. File Mapping Matrix

| Action | File |
|---|---|
| NEW | `.docker/chatwoot/docker-compose.yml`, `.docker/chatwoot/.env.chatwoot` |
| NEW | `Domain/Entities/AuditLog.cs` (+ `ClientInfo` complex type), `CsatSurvey.cs`, `NotificationLog.cs`, `ProcessedWebhookEvent.cs` |
| MODIFY | `Domain/Entities/Ticket.cs` (nav `CsatSurvey?`), `Customer.cs` (nav collection) — only if needed |
| MODIFY | `Application/Common/Interfaces/IApplicationDbContext.cs` (+4 DbSets) |
| NEW | `Application/Common/Interfaces/ICurrentUserService.cs`, `IChatwootClientService.cs`, `IEmailService.cs` |
| NEW | `Application/Features/Notifications/Commands/SendNotification/SendNotificationCommand.cs` |
| NEW | `Application/Features/Notifications/Events/*` (4 events + 4 handlers) |
| NEW | `Application/Features/Surveys/Commands/SubmitCsatSurvey/*`, `Queries/GetCsatReport/*`, `Queries/GetSurveyByTicket/*` |
| NEW | `Application/Features/AuditLogs/Queries/GetAuditLogs/*`, `GetAuditLogDetails/*` |
| NEW | `Application/Features/Notifications/Queries/GetNotificationLogs/*` |
| MODIFY | `Application/Features/Tickets/Commands/{AssignTicket,TransitionTicketStatus,EscalateOverdueTickets}` (publish events) |
| NEW | `Infrastructure/Channels/BoundedChannels.cs` |
| NEW | `Infrastructure/Interceptors/AuditSaveChangesInterceptor.cs` |
| NEW | `Infrastructure/BackgroundServices/{AuditLogProcessor,AuditLogArchiverService,ChatwootWebhookProcessor}.cs` |
| NEW | `Infrastructure/Services/{CurrentUserService,ChatwootClientService,EmailService}.cs` |
| MODIFY | `Infrastructure/Data/ApplicationDbContext.cs` (DbSets + Fluent API) |
| MODIFY | `Infrastructure/DependencyInjection.cs` (channels, interceptor, hosted services, options, HttpClient) |
| MODIFY | `Infrastructure/UniGroup.CRM.Infrastructure.csproj` (+Polly) |
| NEW | `Infrastructure/Migrations/*_AddPhase6Entities.cs` |
| NEW | `API/Controllers/{ChatwootWebhookController,CsatController,AuditLogsController,NotificationsController}.cs` |
| MODIFY | `API/appsettings.json` (Chatwoot/Smtp/Audit sections), `API/Program.cs` (seed: closed ticket + expired CSAT token) |
| NEW | `run_phase6_tests.ps1` |
| MODIFY | `project_log.md`, `project_audit_report.md`, `modules_design_blueprint.md`, `database_design.md` |

---

## 7. Testing Plan (`run_phase6_tests.ps1`)

| # | Test | Expected |
|---|---|---|
| T1 | Login | 200 + JWT |
| T2 | Webhook — valid HMAC signature | 202 Accepted |
| T3 | Webhook — invalid HMAC signature | 401 Unauthorized |
| T4 | Webhook — missing signature header | 400 Bad Request |
| T5 | Webhook idempotency — duplicate payload (same event id) creates exactly 1 ticket (totalCount +1 then unchanged) | pass |
| T6 | Ticket lifecycle → Closed ⇒ CsatSurvey auto-created (`GET /api/surveys/ticket/{id}` returns token) | 200 + token |
| T7 | CSAT submit with valid token | 200 |
| T8 | CSAT resubmit same token | 400 (already submitted) |
| T9 | CSAT submit with seeded EXPIRED token | 400 (expired) |
| T10 | Audit trail — `GET /api/audit-logs?tableName=Tickets` after CRUD activity | 200, totalCount > 0 |
| T11 | Audit logs without JWT | 401 |
| T12 | Notification log — TicketClosed dispatch recorded (`GET /api/notifications/logs`) | 200, contains CsatSurvey/TicketClosed entry |

Then re-run `run_phase4_tests.ps1` (15) and `run_phase5_tests.ps1` (8) — **zero regressions required**.

---

## 8. Execution Order (commits)

1. `infra: configure Chatwoot Docker Compose environment` — Task 1
2. `feat(domain): add Phase 6 entities (AuditLog, CsatSurvey, NotificationLog, ProcessedWebhookEvent)` — Task 2 (+DbContext, +migration)
3. `feat(api): secure Chatwoot webhook controller with HMAC verification and bounded channel` — Task 3
4. `feat(infra): idempotent Chatwoot webhook background processor with Polly retries` — Task 4
5. `feat(infra): async audit trail via EF Core interceptor, bounded channel, batch processor and archiver` — Task 5
6. `feat(app): event-driven notification engine and CSAT feedback loop` — Task 6
7. `test(phase6): add run_phase6_tests.ps1 integration suite` + full 3-suite verification
8. `docs: update project docs for Phase 6 completion`
