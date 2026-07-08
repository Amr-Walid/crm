# Technical Specifications: Phase 6 Integration & Architecture
**Topic:** Notifications, CSAT, Audit Trail & Chatwoot Omnichannel Integration  
**Date:** 2026-07-08  
**Status:** PROPOSED (Awaiting User Review)

---

## 1. Executive Summary & Goals
This document outlines the technical design for Phase 6 of the CRM project. The core objectives are:
1. **Omnichannel Integration:** Connect the CRM to a self-hosted Chatwoot instance via Docker Compose to centralize customer communications (WhatsApp, Web Live Chat, etc.) and automatically link chats to support tickets.
2. **Secure Webhook Ingest:** Expose an endpoint that securely receives webhook events from Chatwoot, validating signatures using HMAC-SHA256.
3. **High-Performance Audit Trail:** Automatically capture all data changes (inserts, updates, deletes) in the CRM using EF Core 9 interceptors and write them asynchronously to prevent degrading application write throughput.
4. **Event-Driven Notifications:** Build an extensible notifications engine triggered by Domain Events (e.g., ticket closed, SLA breached) to send notifications via In-App, Email, and Chatwoot messages.
5. **CSAT Feedback loop:** Automate sending customer satisfaction surveys with unique tokens upon ticket resolution.

---

## 2. System Architecture & High-Performance Data Flow

To prevent database locks and keep API response times under 10ms for webhooks, all heavy operations are decoupled using C# `System.Threading.Channels`.

```mermaid
graph TD
    %% External Layer
    Customer([Customer WhatsApp/Live Chat]) <--> CW[Chatwoot Server (Docker)]
    
    %% Ingest Layer
    CW -->|HTTPS Webhook| WebhookCtrl[Webhook Controller]
    WebhookCtrl -->|HMAC Validation| IngestChannel[In-Memory Ingest Channel]
    
    %% Background Processor Layer
    IngestChannel -->|Consumer| WebhookWorker[Webhook Background Service]
    WebhookWorker -->|EF Core 9 Compiled Query| DB{SQL Server}
    
    %% CRM App & Auditing Layer
    User[Agent Actions / CRM Logic] -->|Writes| AppDbContext[ApplicationDbContext]
    AppDbContext -->|EF Core Interceptor| AuditChannel[In-Memory Audit Channel]
    AuditChannel -->|Batch Consumer| AuditWorker[Audit Trail Background Service]
    AuditWorker -->|Bulk Insert| DB
    
    %% Archiver
    Archiver[Audit Log Archiver Service] -->|Daily Purge > 6 Months| DB
```

### Key Performance Patterns:
1. **Inbox Pattern for Webhooks:** The controller validates the HMAC signature, pushes the raw payload to `Channel<ChatwootWebhookPayload>`, and returns `202 Accepted` immediately.
2. **Outbox Pattern for Auditing:** The `AuditSaveChangesInterceptor` captures modifications and pushes them to `Channel<AuditLog>`. A background service flushes them to the DB in batches of 50 or every 5 seconds, keeping main transactions short.
3. **EF Core 9 Compiled Queries:** Used to resolve customer phone numbers and existing tickets during webhook ingestion.

---

## 3. Database Schema Design (New Entities)

The following tables will be added via EF Core 9 Migrations:

### 3.1. `AuditLogs`
Tracks all modifications automatically. Implemented using **EF Core 9 Complex Types** for client info.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `Id` | `uniqueidentifier` | Primary Key | GUID |
| `UserId` | `uniqueidentifier` | FK → Users (Nullable) | The agent who performed the action |
| `Action` | `nvarchar(100)` | NOT NULL | `Added`, `Modified`, `Deleted` |
| `TableName` | `nvarchar(100)` | NOT NULL | Name of the table affected |
| `RecordId` | `nvarchar(100)` | NOT NULL | Primary key value of the affected record |
| `BeforeValue` | `nvarchar(max)` | NULL | JSON of original values (before update) |
| `AfterValue` | `nvarchar(max)` | NULL | JSON of new values (after insert/update) |
| `ClientInfo_IpAddress`| `nvarchar(100)`| NULL | IP Address (Complex Type) |
| `ClientInfo_UserAgent`| `nvarchar(max)`| NULL | Browser User Agent (Complex Type) |
| `CreatedAt` | `datetime2` | NOT NULL, INDEX | UTC Timestamp |

### 3.2. `CsatSurveys`
Stores customer ratings. Exposes opaque unique tokens for secure submissions.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `Id` | `uniqueidentifier` | Primary Key | GUID |
| `TicketId` | `nvarchar(450)` | FK → Tickets, UNIQUE INDEX | One survey per ticket |
| `CustomerId` | `uniqueidentifier` | FK → Customers | Customer taking the survey |
| `Rating` | `int` | NOT NULL (1 to 5) | Star rating score |
| `Feedback` | `nvarchar(1000)` | NULL | Optional text review |
| `SurveyToken` | `nvarchar(450)` | UNIQUE INDEX, NOT NULL | Secure unique GUID token |
| `SentAt` | `datetime2` | NOT NULL | Timestamp of link dispatch |
| `SubmittedAt` | `datetime2` | NULL | Timestamp of submission |

### 3.3. `NotificationLogs`
Audits alert deliveries.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `Id` | `uniqueidentifier` | Primary Key | GUID |
| `RecipientType` | `nvarchar(100)` | NOT NULL | `Agent` or `Customer` |
| `RecipientId` | `nvarchar(100)` | NOT NULL | Target identifier (User ID or phone number) |
| `Channel` | `nvarchar(100)` | NOT NULL | `Email`, `WhatsApp`, `InApp` |
| `TemplateType` | `nvarchar(100)` | NOT NULL | e.g. `TicketCreated`, `SlaBreached` |
| `Status` | `nvarchar(100)` | NOT NULL | `Sent` or `Failed` |
| `MessageContent` | `nvarchar(max)` | NULL | Compiled text content sent |
| `ErrorMessage` | `nvarchar(max)` | NULL | Failure trace if delivery failed |
| `SentAt` | `datetime2` | NOT NULL | UTC Timestamp |

---

## 4. Webhook Security & Signature Verification
All payloads incoming to `POST /api/webhooks/chatwoot` must be validated to prevent malicious invocations.
- Chatwoot sends an `X-Chatwoot-Signature` (HMAC-SHA256 hash of the JSON body using a configured shared secret) along with `X-Chatwoot-Timestamp`.
- The Webhook Controller intercepts the request, computes the SHA256 HMAC of the raw request body using the environment variable `CHATWOOT_WEBHOOK_SECRET`, and compares it securely using cryptographic constant-time comparison to prevent timing attacks.

---

## 5. Docker Infrastructure Setup (Chatwoot & SQL Server integration)
To host Chatwoot locally on the new laptop, a `docker-compose.yml` file will be created in `.docker/chatwoot/`:

### Services Configured:
1. **`postgres:12-alpine`:** DB for Chatwoot config and conversations.
2. **`redis:6.0-alpine`:** Redis queue for Sidekiq worker jobs.
3. **`chatwoot/chatwoot:v3.10.0` (web):** Rails web app exposed on `http://localhost:3000`.
4. **`chatwoot/chatwoot:v3.10.0` (sidekiq):** Background workers for Chatwoot webhook dispatch.

### Local Webhook Exposer:
- We will install `ngrok` via `winget`.
- Running `ngrok http 5112` will generate an external HTTPS URL (e.g. `https://crm-api.ngrok.app`).
- This URL will be registered as a Webhook inside Chatwoot Dashboard: `https://crm-api.ngrok.app/api/webhooks/chatwoot`.

---

## 6. Implementation Checklist & Phase Steps

### Step 1: Tooling Installation & Docker Orchestration
- [ ] Install Docker Desktop on laptop (Manual step for user).
- [ ] Install ngrok locally: `winget install Equinix.ngrok --silent`.
- [ ] Create `.docker/chatwoot/docker-compose.yml` and `.env.chatwoot`.
- [ ] Run `docker-compose up -d` to spin up Chatwoot.

### Step 2: Database Migration & Schema Mapping
- [ ] Add `AuditLog`, `CsatSurvey`, and `NotificationLog` entities to Domain project.
- [ ] Map relations and indexes in `ApplicationDbContext` (Set null for AuditLog on User deletion, Cascade for CSAT on Ticket deletion).
- [ ] Generate EF Core migration: `dotnet ef migrations add AddPhase6Entities`.
- [ ] Update DB schema: `dotnet ef database update`.

### Step 3: Secure Webhook ingestion
- [ ] Implement signature validation utility using HMAC-SHA256 constant-time comparison.
- [ ] Create `POST /api/webhooks/chatwoot` endpoint.
- [ ] Configure in-memory Channel (`System.Threading.Channels`) for ingestion.
- [ ] Implement `ChatwootWebhookProcessor` (Background Service) to consume webhooks, resolve customer identities (via phone/email), and auto-create tickets for new chat conversations.

### Step 4: Asynchronous Auditing (EF Core Interceptor)
- [ ] Implement `AuditSaveChangesInterceptor` inheriting from `SaveChangesInterceptor`.
- [ ] Configure `Channel<AuditLog>` for audit log dispatch.
- [ ] Implement `AuditLogProcessor` (Background Service) to buffer and batch-insert audit records to database.
- [ ] Implement `AuditLogArchiverService` (Hosted Service running daily) to archive logs older than 6 months.

### Step 5: Event-Driven Notifications & CSAT Feedback
- [ ] Setup SMTP configurations for e-mail dispatch in Infrastructure.
- [ ] Implement notification handlers for Domain Events (`TicketAssigned`, `SlaBreached`, `TicketResolved`).
- [ ] Auto-create CSAT tokens upon Ticket closure, and send the survey link to Chatwoot chat room.
- [ ] Add `POST /api/surveys/submit` endpoint for customer rating collection.

---

## 7. Verification & Automated Testing Plan
- **Mock Webhook Dispatch Tests:** PowerShell scripts simulating Chatwoot webhooks with custom HMAC headers to verify payload handling.
- **Audit Verification Tests:** Perform CRUD operations on Tickets and verify that AuditLogs are successfully created asynchronously in the background.
- **SLA Breach Notification Tests:** Trigger an SLA breach and confirm a notification log of type `SlaBreached` is written to `NotificationLogs`.
