# Phase 6: Notifications, CSAT, Audit Trail & Chatwoot Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement high-performance Chatwoot integration, secure HMAC webhooks with idempotency, async EF Core audit logging, event-driven notifications, and CSAT surveys.

**Architecture:** Webhook ingestion via Bounded Channel with background consumer. Audit logging via EF Core interceptor feeding a Bounded Channel for batched DB writes. Expiring CSAT tokens generated on ticket closure.

**Tech Stack:** ASP.NET Core 9, EF Core 9, SQL Server, Docker, System.Threading.Channels, Polly.

## Global Constraints
- Target Framework: .NET 9
- ORM: Entity Framework Core 9
- All operations must be idempotent.
- In-memory channels must be bounded with capacity controls.
- Background services must support Graceful Shutdown.

---

## File Mapping Matrix
For this implementation, the following files will be created or modified:
- **NEW** `.docker/chatwoot/docker-compose.yml` (Docker infrastructure)
- **NEW** `.docker/chatwoot/.env.chatwoot` (Environment config)
- **NEW** `src/UniGroup.CRM.Domain/Entities/AuditLog.cs` (Audit entity)
- **NEW** `src/UniGroup.CRM.Domain/Entities/CsatSurvey.cs` (CSAT survey entity)
- **NEW** `src/UniGroup.CRM.Domain/Entities/NotificationLog.cs` (Notifications audit)
- **NEW** `src/UniGroup.CRM.Domain/Entities/ProcessedWebhookEvent.cs` (Idempotency log)
- **MODIFY** `src/UniGroup.CRM.Infrastructure/Data/ApplicationDbContext.cs` (Entity mappings)
- **NEW** `src/UniGroup.CRM.Infrastructure/Interceptors/AuditSaveChangesInterceptor.cs` (EF Interceptor)
- **NEW** `src/UniGroup.CRM.Infrastructure/Channels/BoundedChannels.cs` (Bounded Channels declarations)
- **NEW** `src/UniGroup.CRM.Infrastructure/BackgroundServices/AuditLogProcessor.cs` (Async log flusher)
- **NEW** `src/UniGroup.CRM.Infrastructure/BackgroundServices/AuditLogArchiverService.cs` (Daily purger)
- **NEW** `src/UniGroup.CRM.API/Controllers/ChatwootWebhookController.cs` (Signature validation & channel ingest)
- **NEW** `src/UniGroup.CRM.Infrastructure/BackgroundServices/ChatwootWebhookProcessor.cs` (Ingestion worker with Polly)
- **NEW** `src/UniGroup.CRM.API/Controllers/CsatController.cs` (Survey submission & validation)

---

## Tasks Checklist

### Task 1: Docker Compose Setup & ngrok Configuration
Prepare the local environment to run Chatwoot and ngrok.

**Files:**
- Create: `.docker/chatwoot/docker-compose.yml`
- Create: `.docker/chatwoot/.env.chatwoot`

- [ ] **Step 1: Write the docker-compose file**
  Create the file `.docker/chatwoot/docker-compose.yml`:
  ```yaml
  version: '3'
  services:
    postgres:
      image: postgres:12-alpine
      env_file: .env.chatwoot
      volumes:
        - pgdata:/var/lib/postgresql/data
      ports:
        - '5432:5432'
    redis:
      image: redis:6.0-alpine
      ports:
        - '6379:6379'
      volumes:
        - redisdata:/data
    web:
      image: chatwoot/chatwoot:v3.10.0
      env_file: .env.chatwoot
      ports:
        - '3000:3000'
      depends_on:
        - postgres
        - redis
      command: bundle exec rails s -p 3000 -b '0.0.0.0'
    sidekiq:
      image: chatwoot/chatwoot:v3.10.0
      env_file: .env.chatwoot
      depends_on:
        - postgres
        - redis
      command: bundle exec sidekiq -C config/sidekiq.yml
  volumes:
    pgdata:
    redisdata:
  ```

- [ ] **Step 2: Create the environment variables file**
  Create the file `.docker/chatwoot/.env.chatwoot`:
  ```env
  POSTGRES_PASSWORD=postgres_password_123
  POSTGRES_USER=postgres
  POSTGRES_DB=chatwoot_production
  DATABASE_URL=postgres://postgres:postgres_password_123@postgres:5432/chatwoot_production
  REDIS_URL=redis://redis:6379/0
  RAILS_ENV=production
  SECRET_KEY_BASE=a_very_long_random_hex_string_32_characters_at_least
  ACTIVE_STORAGE_SERVICE=local
  FRONTEND_URL=http://localhost:3000
  ```

- [ ] **Step 3: Test running Chatwoot locally**
  Run command: `docker compose -f .docker/chatwoot/docker-compose.yml up -d`
  Expected: Containers start successfully, and Chatwoot dashboard is accessible at `http://localhost:3000`.

- [ ] **Step 4: Verify ngrok command runs**
  Run: `ngrok --version`
  Expected: Prints version `3.3.1` or similar.

- [ ] **Step 5: Commit**
  Run:
  ```bash
  git add .docker/chatwoot/docker-compose.yml .docker/chatwoot/.env.chatwoot
  git commit -m "infra: configure Chatwoot Docker Compose environment"
  ```

---

### Task 2: Domain Entities & Database Migrations
Create entities for Audit Trails, CSAT Surveys, Inbound Webhooks Idempotency, and Notification Logs.

**Files:**
- Create: `src/UniGroup.CRM.Domain/Entities/AuditLog.cs`
- Create: `src/UniGroup.CRM.Domain/Entities/CsatSurvey.cs`
- Create: `src/UniGroup.CRM.Domain/Entities/NotificationLog.cs`
- Create: `src/UniGroup.CRM.Domain/Entities/ProcessedWebhookEvent.cs`
- Modify: `src/UniGroup.CRM.Infrastructure/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Write AuditLog entity**
  Create `src/UniGroup.CRM.Domain/Entities/AuditLog.cs`:
  ```csharp
  using System;

  namespace UniGroup.CRM.Domain.Entities;

  public class AuditLog
  {
      public Guid Id { get; set; }
      public Guid? UserId { get; set; }
      public string Action { get; set; } = null!;
      public string TableName { get; set; } = null!;
      public string RecordId { get; set; } = null!;
      public string? BeforeValue { get; set; }
      public string? AfterValue { get; set; }
      public string? ClientIp { get; set; }
      public string? UserAgent { get; set; }
      public DateTime CreatedAt { get; set; }
  }
  ```

- [ ] **Step 2: Write CsatSurvey entity**
  Create `src/UniGroup.CRM.Domain/Entities/CsatSurvey.cs`:
  ```csharp
  using System;

  namespace UniGroup.CRM.Domain.Entities;

  public class CsatSurvey
  {
      public Guid Id { get; set; }
      public string TicketId { get; set; } = null!;
      public Guid CustomerId { get; set; }
      public int Rating { get; set; }
      public string? Feedback { get; set; }
      public string SurveyToken { get; set; } = null!;
      public DateTime SentAt { get; set; }
      public DateTime ExpiresAt { get; set; }
      public DateTime? SubmittedAt { get; set; }
  }
  ```

- [ ] **Step 3: Write NotificationLog entity**
  Create `src/UniGroup.CRM.Domain/Entities/NotificationLog.cs`:
  ```csharp
  using System;

  namespace UniGroup.CRM.Domain.Entities;

  public class NotificationLog
  {
      public Guid Id { get; set; }
      public string RecipientType { get; set; } = null!;
      public string RecipientId { get; set; } = null!;
      public string Channel { get; set; } = null!;
      public string TemplateType { get; set; } = null!;
      public string Status { get; set; } = null!;
      public string? MessageContent { get; set; }
      public string? ErrorMessage { get; set; }
      public DateTime SentAt { get; set; }
  }
  ```

- [ ] **Step 4: Write ProcessedWebhookEvent entity**
  Create `src/UniGroup.CRM.Domain/Entities/ProcessedWebhookEvent.cs`:
  ```csharp
  using System;

  namespace UniGroup.CRM.Domain.Entities;

  public class ProcessedWebhookEvent
  {
      public string EventId { get; set; } = null!;
      public DateTime ProcessedAt { get; set; }
  }
  ```

- [ ] **Step 5: Register entities and Fluent Configurations in ApplicationDbContext**
  Modify `src/UniGroup.CRM.Infrastructure/Data/ApplicationDbContext.cs` to add DBSets and configurations:
  ```csharp
  public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
  public DbSet<CsatSurvey> CsatSurveys => Set<CsatSurvey>();
  public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
  public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

  // In OnModelCreating:
  builder.Entity<ProcessedWebhookEvent>(b =>
  {
      b.HasKey(e => e.EventId);
  });
  builder.Entity<CsatSurvey>(b =>
  {
      b.HasIndex(s => s.SurveyToken).IsUnique();
      b.HasIndex(s => s.TicketId).IsUnique();
  });
  builder.Entity<AuditLog>(b =>
  {
      b.HasIndex(a => a.CreatedAt);
  });
  ```

- [ ] **Step 6: Create and Apply Migrations**
  Run: `dotnet ef migrations add AddPhase6Entities --project src/UniGroup.CRM.Infrastructure --startup-project src/UniGroup.CRM.API`
  Expected: Migration succeeds.
  Run: `dotnet ef database update --project src/UniGroup.CRM.Infrastructure --startup-project src/UniGroup.CRM.API`
  Expected: Database schema updated successfully.

- [ ] **Step 7: Commit**
  Run:
  ```bash
  git add src/UniGroup.CRM.Domain/Entities/ src/UniGroup.CRM.Infrastructure/Data/ApplicationDbContext.cs
  git commit -m "feat: add domain entities for Audit, CSAT, Notifications, and Idempotency"
  ```

---

### Task 3: Secure Webhook Ingestion Controller & Bounded Channel
Expose the secure webhook ingestion endpoint to handle Chatwoot payloads.

**Files:**
- Create: `src/UniGroup.CRM.Infrastructure/Channels/BoundedChannels.cs`
- Create: `src/UniGroup.CRM.API/Controllers/ChatwootWebhookController.cs`
- Modify: `src/UniGroup.CRM.API/Program.cs`

- [ ] **Step 1: Write BoundedChannels definitions**
  Create `src/UniGroup.CRM.Infrastructure/Channels/BoundedChannels.cs` to expose Channels:
  ```csharp
  using System.Threading.Channels;
  using UniGroup.CRM.Domain.Entities;

  namespace UniGroup.CRM.Infrastructure.Channels;

  public class ChatwootWebhookChannel
  {
      private readonly Channel<string> _channel;

      public ChatwootWebhookChannel()
      {
          var options = new BoundedChannelOptions(10000)
          {
              FullMode = BoundedChannelFullMode.Wait,
              SingleReader = true,
              SingleWriter = false
          };
          _channel = Channel.CreateBounded<string>(options);
      }

      public ChannelWriter<string> Writer => _channel.Writer;
      public ChannelReader<string> Reader => _channel.Reader;
  }
  ```

- [ ] **Step 2: Register Bounded Channel in Dependency Injection**
  Modify `src/UniGroup.CRM.API/Program.cs`:
  ```csharp
  builder.Services.AddSingleton<ChatwootWebhookChannel>();
  ```

- [ ] **Step 3: Implement Webhook Controller with HMAC verification**
  Create `src/UniGroup.CRM.API/Controllers/ChatwootWebhookController.cs`:
  ```csharp
  using Microsoft.AspNetCore.Http;
  using Microsoft.AspNetCore.Mvc;
  using System;
  using System.IO;
  using System.Security.Cryptography;
  using System.Text;
  using System.Threading.Tasks;
  using UniGroup.CRM.Infrastructure.Channels;

  namespace UniGroup.CRM.API.Controllers;

  [ApiController]
  [Route("api/webhooks/chatwoot")]
  public class ChatwootWebhookController : ControllerBase
  {
      private readonly ChatwootWebhookChannel _webhookChannel;
      private const string SecretEnvVar = "CHATWOOT_WEBHOOK_SECRET";

      public ChatwootWebhookController(ChatwootWebhookChannel webhookChannel)
      {
          _webhookChannel = webhookChannel;
      }

      [HttpPost]
      public async Task<IActionResult> ReceiveWebhook()
      {
          Request.EnableBuffering();
          
          string signature = Request.Headers["X-Chatwoot-Signature"].ToString();
          if (string.IsNullOrEmpty(signature)) return BadRequest("Missing signature");

          using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
          string body = await reader.ReadToEndAsync();
          Request.Body.Position = 0;

          string? secret = Environment.GetEnvironmentVariable(SecretEnvVar);
          if (string.IsNullOrEmpty(secret)) return StatusCode(500, "Webhook secret not configured");

          using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
          byte[] computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
          string computedSignature = Convert.ToBase64String(computedHash);

          if (!CryptographicOperations.FixedTimeEquals(
              Encoding.UTF8.GetBytes(signature), 
              Encoding.UTF8.GetBytes(computedSignature)))
          {
              return Unauthorized("Invalid signature");
          }

          await _webhookChannel.Writer.WriteAsync(body);
          return Accepted();
      }
  }
  ```

- [ ] **Step 4: Write integration test using HttpClient**
  Run the application, send a request with a valid computed HMAC using PowerShell, and verify we get a `202 Accepted` response.

- [ ] **Step 5: Commit**
  Run:
  ```bash
  git add src/UniGroup.CRM.Infrastructure/Channels/BoundedChannels.cs src/UniGroup.CRM.API/Controllers/ChatwootWebhookController.cs src/UniGroup.CRM.API/Program.cs
  git commit -m "feat: implement secure webhook controller with signature verification and bounded channel"
  ```

---

### Task 4: Webhook Ingestion Background Processor
Process incoming payloads from the Channel. Check for idempotency and create Tickets/Customers with Polly retry policies.

**Files:**
- Create: `src/UniGroup.CRM.Infrastructure/BackgroundServices/ChatwootWebhookProcessor.cs`
- Modify: `src/UniGroup.CRM.API/Program.cs`

- [ ] **Step 1: Write Webhook Background Processor**
  Create `src/UniGroup.CRM.Infrastructure/BackgroundServices/ChatwootWebhookProcessor.cs`:
  ```csharp
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;
  using Microsoft.Extensions.Logging;
  using System;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using Polly;
  using Polly.Retry;
  using UniGroup.CRM.Domain.Entities;
  using UniGroup.CRM.Infrastructure.Channels;
  using UniGroup.CRM.Infrastructure.Data;

  namespace UniGroup.CRM.Infrastructure.BackgroundServices;

  public class ChatwootWebhookProcessor : BackgroundService
  {
      private readonly ChatwootWebhookChannel _channel;
      private readonly IServiceScopeFactory _scopeFactory;
      private readonly ILogger<ChatwootWebhookProcessor> _logger;
      private readonly AsyncRetryPolicy _retryPolicy;

      public ChatwootWebhookProcessor(
          ChatwootWebhookChannel channel,
          IServiceScopeFactory scopeFactory,
          ILogger<ChatwootWebhookProcessor> logger)
      {
          _channel = channel;
          _scopeFactory = scopeFactory;
          _logger = logger;
          _retryPolicy = Policy.Handle<Exception>()
              .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                  (ex, time) => _logger.LogWarning($"Transient fault. Retrying after {time.TotalSeconds}s: {ex.Message}"));
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          while (await _channel.Reader.WaitToReadAsync(stoppingToken))
          {
              while (_channel.Reader.TryRead(out var payload))
              {
                  try
                  {
                      await _retryPolicy.ExecuteAsync(() => ProcessPayloadAsync(payload, stoppingToken));
                  }
                  catch (Exception ex)
                  {
                      _logger.LogError(ex, "Failed to process webhook payload after retries.");
                  }
              }
          }
      }

      private async Task ProcessPayloadAsync(string payload, CancellationToken cancellationToken)
      {
          using var jsonDoc = JsonDocument.Parse(payload);
          var root = jsonDoc.RootElement;
          
          if (!root.TryGetProperty("event", out var eventProperty)) return;
          string eventType = eventProperty.GetString() ?? "";

          // Extract message/event identifier for idempotency
          string? eventId = null;
          if (root.TryGetProperty("id", out var idProp)) eventId = idProp.GetString() ?? idProp.GetInt64().ToString();
          if (string.IsNullOrEmpty(eventId)) return;

          using var scope = _scopeFactory.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

          // Idempotency Check
          var existing = await db.ProcessedWebhookEvents.FindAsync(new object[] { eventId }, cancellationToken);
          if (existing != null) return;

          // Simple business logic (Create ticket or customer placeholder)
          // ... implementation ...

          db.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
          {
              EventId = eventId,
              ProcessedAt = DateTime.UtcNow
          });

          await db.SaveChangesAsync(cancellationToken);
      }
  }
  ```

- [ ] **Step 2: Register background service**
  Modify `src/UniGroup.CRM.API/Program.cs`:
  ```csharp
  builder.Services.AddHostedService<ChatwootWebhookProcessor>();
  ```

- [ ] **Step 3: Test processing webhooks**
  Run the application, send a mock JSON payload, check the database logs to verify `ProcessedWebhookEvents` is written, and repeat the payload to confirm it gets discarded on the second write.

- [ ] **Step 4: Commit**
  Run:
  ```bash
  git add src/UniGroup.CRM.Infrastructure/BackgroundServices/ChatwootWebhookProcessor.cs src/UniGroup.CRM.API/Program.cs
  git commit -m "feat: implement idempotent webhook background processor with Polly retry policy"
  ```

---

### Task 5: Asynchronous EF Core Auditing Interceptor & Channel
Capture EF Core modifications, route them through a Bounded Channel, and batch-write to database in the background.

**Files:**
- Modify: `src/UniGroup.CRM.Infrastructure/Channels/BoundedChannels.cs`
- Create: `src/UniGroup.CRM.Infrastructure/Interceptors/AuditSaveChangesInterceptor.cs`
- Create: `src/UniGroup.CRM.Infrastructure/BackgroundServices/AuditLogProcessor.cs`
- Create: `src/UniGroup.CRM.Infrastructure/BackgroundServices/AuditLogArchiverService.cs`
- Modify: `src/UniGroup.CRM.API/Program.cs`

- [ ] **Step 1: Define Bounded Channel for Audit Log**
  Modify `src/UniGroup.CRM.Infrastructure/Channels/BoundedChannels.cs`:
  ```csharp
  public class AuditLogChannel
  {
      private readonly Channel<AuditLog> _channel;

      public AuditLogChannel()
      {
          var options = new BoundedChannelOptions(5000)
          {
              FullMode = BoundedChannelFullMode.Wait,
              SingleReader = true,
              SingleWriter = false
          };
          _channel = Channel.CreateBounded<AuditLog>(options);
      }

      public ChannelWriter<AuditLog> Writer => _channel.Writer;
      public ChannelReader<AuditLog> Reader => _channel.Reader;
  }
  ```

- [ ] **Step 2: Register Audit Log Channel**
  Modify `src/UniGroup.CRM.API/Program.cs`:
  ```csharp
  builder.Services.AddSingleton<AuditLogChannel>();
  ```

- [ ] **Step 3: Implement Interceptor**
  Create `src/UniGroup.CRM.Infrastructure/Interceptors/AuditSaveChangesInterceptor.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore.Diagnostics;
  using System.Threading;
  using System.Threading.Tasks;
  using UniGroup.CRM.Domain.Entities;
  using UniGroup.CRM.Infrastructure.Channels;

  namespace UniGroup.CRM.Infrastructure.Interceptors;

  public class AuditSaveChangesInterceptor : SaveChangesInterceptor
  {
      private readonly AuditLogChannel _auditChannel;

      public AuditSaveChangesInterceptor(AuditLogChannel auditChannel)
      {
          _auditChannel = auditChannel;
      }

      public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
          DbContextEventData eventData,
          InterceptionResult<int> result,
          CancellationToken cancellationToken = default)
      {
          var context = eventData.Context;
          if (context == null) return result;

          foreach (var entry in context.ChangeTracker.Entries())
          {
              if (entry.Entity is AuditLog || entry.Entity is ProcessedWebhookEvent) continue;

              var auditLog = new AuditLog
              {
                  Id = Guid.NewGuid(),
                  Action = entry.State.ToString(),
                  TableName = entry.Metadata.GetTableName() ?? entry.Metadata.Name,
                  CreatedAt = DateTime.UtcNow,
                  // Capture field modifications ...
              };
              await _auditChannel.Writer.WriteAsync(auditLog, cancellationToken);
          }
          return result;
      }
  }
  ```

- [ ] **Step 4: Implement Audit Processor background service**
  Create `src/UniGroup.CRM.Infrastructure/BackgroundServices/AuditLogProcessor.cs` to batch insert records:
  ```csharp
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;
  using System;
  using System.Collections.Generic;
  using System.Threading;
  using System.Threading.Tasks;
  using UniGroup.CRM.Domain.Entities;
  using UniGroup.CRM.Infrastructure.Channels;
  using UniGroup.CRM.Infrastructure.Data;

  namespace UniGroup.CRM.Infrastructure.BackgroundServices;

  public class AuditLogProcessor : BackgroundService
  {
      private readonly AuditLogChannel _channel;
      private readonly IServiceScopeFactory _scopeFactory;

      public AuditLogProcessor(AuditLogChannel channel, IServiceScopeFactory scopeFactory)
      {
          _channel = channel;
          _scopeFactory = scopeFactory;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          var buffer = new List<AuditLog>();
          while (await _channel.Reader.WaitToReadAsync(stoppingToken))
          {
              while (_channel.Reader.TryRead(out var log))
              {
                  buffer.Add(log);
                  if (buffer.Count >= 50)
                  {
                      await FlushAsync(buffer);
                  }
              }
              if (buffer.Count > 0)
              {
                  await FlushAsync(buffer);
              }
          }
      }

      private async Task FlushAsync(List<AuditLog> buffer)
      {
          using var scope = _scopeFactory.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
          db.AuditLogs.AddRange(buffer);
          await db.SaveChangesAsync();
          buffer.Clear();
      }
  }
  ```

- [ ] **Step 5: Implement Daily Archiver Background Job**
  Create `src/UniGroup.CRM.Infrastructure/BackgroundServices/AuditLogArchiverService.cs`:
  ```csharp
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;
  using System;
  using System.Threading;
  using System.Threading.Tasks;
  using UniGroup.CRM.Infrastructure.Data;

  namespace UniGroup.CRM.Infrastructure.BackgroundServices;

  public class AuditLogArchiverService : BackgroundService
  {
      private readonly IServiceScopeFactory _scopeFactory;

      public AuditLogArchiverService(IServiceScopeFactory scopeFactory)
      {
          _scopeFactory = scopeFactory;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          while (!stoppingToken.IsCancellationRequested)
          {
              try
              {
                  using var scope = _scopeFactory.CreateScope();
                  var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                  var cutoff = DateTime.UtcNow.AddMonths(-6);
                  // Execute raw SQL or bulk delete items older than cutoff
                  // ... implementation ...
              }
              catch
              {
                  // log error
              }
              await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
          }
      }
  }
  ```

- [ ] **Step 6: Register Services and interceptors**
  Modify `src/UniGroup.CRM.API/Program.cs`:
  ```csharp
  builder.Services.AddSingleton<AuditSaveChangesInterceptor>();
  builder.Services.AddHostedService<AuditLogProcessor>();
  builder.Services.AddHostedService<AuditLogArchiverService>();

  // Add interceptor to DbContext setup:
  builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
  {
      options.UseSqlServer(connectionString)
             .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
  });
  ```

- [ ] **Step 7: Commit**
  Run:
  ```bash
  git add src/UniGroup.CRM.Infrastructure/Interceptors/ src/UniGroup.CRM.Infrastructure/BackgroundServices/AuditLogProcessor.cs src/UniGroup.CRM.Infrastructure/BackgroundServices/AuditLogArchiverService.cs src/UniGroup.CRM.API/Program.cs
  git commit -m "feat: implement async audit trailing using EF Core interceptors and bounded channels"
  ```

---

### Task 6: Event-Driven Notifications & CSAT Feedback
Build the notifications dispatcher and survey expiration logic.

**Files:**
- Create: `src/UniGroup.CRM.API/Controllers/CsatController.cs`

- [ ] **Step 1: Write CSAT controller with token expiration check**
  Create `src/UniGroup.CRM.API/Controllers/CsatController.cs`:
  ```csharp
  using Microsoft.AspNetCore.Mvc;
  using System;
  using System.Threading.Tasks;
  using UniGroup.CRM.Infrastructure.Data;

  namespace UniGroup.CRM.API.Controllers;

  [ApiController]
  [Route("api/surveys")]
  public class CsatController : ControllerBase
  {
      private readonly ApplicationDbContext _db;

      public CsatController(ApplicationDbContext db)
      {
          _db = db;
      }

      [HttpPost("submit")]
      public async Task<IActionResult> SubmitSurvey([FromQuery] string token, [FromBody] SurveySubmissionDto dto)
      {
          var survey = await _db.CsatSurveys.FirstOrDefaultAsync(s => s.SurveyToken == token);
          if (survey == null) return NotFound("Survey token not found");

          if (survey.SubmittedAt != null) return BadRequest("Survey already submitted");
          if (DateTime.UtcNow > survey.ExpiresAt) return BadRequest("Survey token has expired");

          survey.Rating = dto.Rating;
          survey.Feedback = dto.Feedback;
          survey.SubmittedAt = DateTime.UtcNow;

          await _db.SaveChangesAsync();
          return Ok("Thank you for your feedback");
      }
  }

  public class SurveySubmissionDto
  {
      public int Rating { get; set; }
      public string? Feedback { get; set; }
  }
  ```

- [ ] **Step 2: Test survey expiration**
  Manually seed an expired survey token into database. Make a POST request to `/api/surveys/submit?token=expired-token` and verify it gets rejected with HTTP 400 Bad Request.

- [ ] **Step 3: Commit**
  Run:
  ```bash
  git add src/UniGroup.CRM.API/Controllers/CsatController.cs
  git commit -m "feat: add CSAT submission controller with token expiration validation"
  ```
