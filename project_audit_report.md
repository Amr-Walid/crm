# تقرير تدقيق المشروع ومطابقة المتطلبات
## UniGroup CRM Platform — نسخة التدقيق الشاملة بعد إنجاز 5 مراحل كاملة

**تاريخ آخر تحديث:** 5 يوليو 2026
**الحالة الإجمالية:** Phase 1 ✅ + Phase 2 ✅ + Phase 3 ✅ + Phase 4 ✅ + Phase 5 ✅ — **مكتملة ومختبرة بالكامل**
**آخر Commit:** `bbfe577` — feat: add Call-Ticket link and User-Department relationship

---

## 1. ملخص تنفيذي (Executive Summary)

يوثق هذا التقرير الحالة الفعلية والمحدثة لنظام CRM المبني بـ **Clean Architecture + CQRS/MediatR + EF Core 9 + .NET 9 + SQL Server**، بما يشمل:

- مطابقة المتطلبات الـ 19 مع ما تم تنفيذه فعلياً
- تدقيق هيكل قاعدة البيانات ومقارنته بالتصميم الأصلي
- نتائج الاختبارات: **49 اختبار إجمالياً** (16 للمرحلة 2 + 10 للمرحلة 3 + 15 للمرحلة 4 + 8 للمرحلة 5) — **100% ناجح**
- التحسينات المطبقة من EF Core 9 و .NET 9
- المشاكل المكتشفة والمحلولة
- الخطوات القادمة للمرحلة 6

**الخلاصة:** تم إنجاز 5 مراحل كاملة بنجاح مع تطبيق أحدث ميزات EF Core 9. المشروع جاهز للانتقال للمرحلة 6 (الإشعارات + Chatwoot + CSAT + Audit Trail).

---

## 2. تدقيق هيكل قاعدة البيانات (Database Schema Audit)

### الجداول الموجودة حالياً في SQL Server:

| الجدول | الكيان (Entity) | المرحلة | الملاحظات |
|---|---|:-:|---|
| `Users` | `ApplicationUser` | Phase 1 | ✅ يرث من `IdentityUser<Guid>` — حقول مخصصة: FirstName, LastName, IsActive, CreatedAt, **DepartmentId (FK Nullable → Departments, SetNull)** |
| `Roles` | `ApplicationRole` | Phase 1 | ✅ يرث من `IdentityRole<Guid>` — حقل Description إضافي |
| `RefreshTokens` | `RefreshToken` | Phase 1 | ✅ FK لـ Users — مقيد بـ IP Address — Cascade delete |
| `UserRoles` | ASP.NET Identity | Phase 1 | ✅ جدول ربط بين Users وRoles |
| `UserClaims` | ASP.NET Identity | Phase 1 | ✅ جزء من نظام Identity |
| `RoleClaims` | ASP.NET Identity | Phase 1 | ✅ جزء من نظام Identity |
| `UserLogins` | ASP.NET Identity | Phase 1 | ✅ جزء من نظام Identity |
| `UserTokens` | ASP.NET Identity | Phase 1 | ✅ جزء من نظام Identity |
| `Customers` | `Customer` | Phase 2 | ✅ حقل `PreferredChannels nvarchar(max)` كـ Primitive Collection JSON (EF Core 9) |
| `CustomerPhones` | `CustomerPhone` | Phase 2 | ✅ Unique Index على `Phone` — حقل `IsPrimary` |
| `DeviceBrands` | `DeviceBrand` | Phase 2 | ✅ Unique Index على `Name` |
| `DeviceModels` | `DeviceModel` | Phase 2 | ✅ Composite Unique Index على `(BrandId, Name)` |
| `CustomerDevices` | `CustomerDevice` | Phase 2 | ✅ Filtered Unique Index على IMEI وSerialNumber (يسمح بـ NULL) |
| `Calls` | `Call` | Phase 3 | ✅ FK Nullable للـ Customer (SetNull) — FK Required للـ Agent (Restrict) — **FK Nullable للـ Ticket (SetNull)** |
| `Departments` | `Department` | Phase 4 | ✅ Unique Index على `Name` |
| `Tickets` | `Ticket` | Phase 4 | ✅ PK بصيغة `T-YYYY-NNNNN` — حقول SLA وChatwootConversationId |
| `Attachments` | `Attachment` | Phase 4 | ✅ FK Cascade لـ Tickets — يخزن المرفقات محلياً |
| `InternalNotes` | `InternalNote` | Phase 4 | ✅ FK Cascade لـ Tickets — مخصصة للموظفين فقط |
| `TicketHistories` | `TicketHistory` | Phase 4 | ✅ FK Cascade لـ Tickets — يسجل كل انتقالات الحالة |

**إجمالي الجداول:** 19 جدول ✅ (8 من Identity + 11 من التطبيق)

### الـ Migrations المطبقة بالترتيب:

| # | اسم الـ Migration | التاريخ | ما تفعله |
|:-:|---|---|---|
| 1 | `20260701080141_InitialCreate` | 2026-07-01 | جداول ASP.NET Identity + RefreshTokens |
| 2 | `20260701085518_AddPhase2Entities` | 2026-07-01 | Customers + CustomerPhones + DeviceBrands + DeviceModels + CustomerDevices |
| 3 | `20260701092950_AddPhase3Calls` | 2026-07-01 | Calls + Indexes على PhoneNumber, CustomerId, AgentId |
| 4 | `20260701093604_AddUniqueIndexesForBrandAndModel` | 2026-07-01 | Unique Index على DeviceBrand.Name + Composite على DeviceModel(BrandId, Name) |
| 5 | `20260702102445_AddPhase4TicketsWorkflowsAndSla` | 2026-07-02 | Departments + Tickets + Attachments + InternalNotes + TicketHistories |
| 6 | `20260705110501_AddCustomerPreferredChannels` | 2026-07-05 | حقل `PreferredChannels` كـ Primitive Collection JSON في جدول Customers |
| 7 | `20260705115426_AddCallTicketLinkAndUserDepartment` | 2026-07-05 | إضافة `TicketId` FK في Calls + `DepartmentId` FK في Users + Indexes |

### القيود والفهارس المطبقة (Fluent API):

```csharp
// Unique Phone
HasIndex(cp => cp.Phone).IsUnique()

// Filtered Unique IMEI (allows null/empty)
HasIndex(cd => cd.IMEI).IsUnique()
    .HasFilter("[IMEI] IS NOT NULL AND [IMEI] != ''")

// Filtered Unique SerialNumber (allows null/empty)
HasIndex(cd => cd.SerialNumber).IsUnique()
    .HasFilter("[SerialNumber] IS NOT NULL AND [SerialNumber] != ''")

// Unique Brand Name
HasIndex(db => db.Name).IsUnique()

// Unique Model per Brand (Composite)
HasIndex(dm => new { dm.BrandId, dm.Name }).IsUnique()

// Unique Department Name
HasIndex(d => d.Name).IsUnique()

// Ticket performance indexes
HasIndex(t => t.Status)
HasIndex(t => t.Priority)
HasIndex(t => t.SlaDeadline)
HasIndex(t => t.CreatedAt)
HasIndex(t => t.AssignedToId)
HasIndex(t => t.CustomerId)

// Call performance indexes
HasIndex(c => c.PhoneNumber)
HasIndex(c => c.CustomerId)
HasIndex(c => c.AgentId)
HasIndex(c => c.TicketId)
```

---

## 3. مطابقة المتطلبات (Requirements Mapping)

```mermaid
flowchart TD
    P1[Phase 1: Identity ✅] --> P2[Phase 2: Customers & Devices ✅]
    P2 --> P3[Phase 3: Call Center ✅]
    P3 --> P4[Phase 4: Tickets & SLA ✅]
    P4 --> P5[Phase 5: Dashboards & Reports ✅]
    P5 --> P6[Phase 6: Notifications, Chatwoot, CSAT, Audit ⏳]
```

### جدول مطابقة المتطلبات التفصيلي:

| م | المتطلب | الحالة | الجداول | الملاحظات |
|:-:|---|:-:|---|---|
| 1 | **إدارة العملاء** | ✅ | `Customers`, `CustomerPhones` | تسجيل عملاء + رفض تكرار الهاتف + هواتف متعددة + PreferredChannels |
| 2 | **إدارة مركز الاتصال** | ✅ | `Calls` | Inbound/Outbound — AgentId من JWT تلقائياً |
| 3 | **إدارة الحالات والشكاوى** | ✅ | `Tickets` | رقم مقروء T-YYYY-NNNNN + ربط بعميل وجهاز وقسم |
| 4 | **الملف التعريفي للعميل 360°** | ✅ | `Customers`, `CustomerPhones`, `CustomerDevices` | يجلب الهواتف + الأجهزة + حالة الضمان + PreferredChannels |
| 5 | **رؤية العميل الموحدة (Caller ID)** | ✅ | `Customers`, `CustomerPhones`, `Calls` | Compiled Query — تعريف العميل فورياً عند الاتصال |
| 6 | **تصنيفات الاتصال والحالات** | ✅ | `Tickets` | `TicketCategory` enum + أولوية + موعد SLA تلقائي |
| 7 | **قاعدة المعرفة والتشخيص** | ⏳ | `KnowledgeBase` (مخطط) | مخطط للمرحلة 6 |
| 8 | **دورة حياة التذكرة (State Machine)** | ✅ | `Tickets`, `TicketHistories` | 8 حالات + محرك انتقال صارم + تسجيل أوقات البقاء |
| 9 | **توجيه الحالات بين الإدارات** | ✅ | `Tickets`, `Departments`, `TicketHistories` | توجيه لقسم أو موظف + تسجيل التحويلات |
| 10 | **التصعيد التلقائي** | ✅ | `Tickets`, `TicketHistories` | SlaMonitorService يعمل في الخلفية كـ BackgroundService |
| 11 | **اتفاقية مستوى الخدمة (SLA)** | ✅ | `Tickets` | إيقاف العداد عند Waiting + إعادة التشغيل + حساب Deadline |
| 12 | **الملاحظات الداخلية والمرفقات** | ✅ | `InternalNotes`, `Attachments` | ملاحظات سرية + رفع صور/PDF حد أقصى 10MB |
| 13 | **نظام البحث المتقدم** | ✅ | `Customers`, `CustomerPhones`, `CustomerDevices` | بحث بالاسم + هاتف + IMEI + Serial |
| 14 | **لوحة التحكم والإحصائيات** | ✅ | `Tickets`, `Calls` | 5 استعلامات إحصائية + HybridCache + Cache Tags |
| 15 | **التقارير والتحليلات** | ✅ | `Tickets`, `Calls` | تقارير أداء الموظفين + أعطال الأجهزة + تصدير CSV |
| 16 | **الإشعارات والتنبيهات** | ⏳ | — | مخطط للمرحلة 6 (Chatwoot + Email) |
| 17 | **قياس رضا العملاء (CSAT)** | ⏳ | `CsatSurveys` (مخطط) | مخطط للمرحلة 6 |
| 18 | **الصلاحيات والأدوار** | ✅ | `Users`, `Roles` | JWT + Roles: Agent, Team Leader, Admin |
| 19 | **سجل التدقيق (Audit Trail)** | ⏳ | `AuditLogs` (مخطط) | مخطط للمرحلة 6 — سيستخدم EF Core 9 Complex Types |

**ملخص:** 15 متطلب مكتمل ✅ — 4 متطلبات في خطة المرحلة 6 ⏳

---

## 4. هيكل ملفات الكود الحالي (Actual Codebase Structure)

```
src/
├── UniGroup.CRM.Domain/
│   ├── Entities/                         (14 ملف)
│   │   ├── ApplicationUser.cs            ← Phase 1 | IdentityUser<Guid> + FirstName, LastName, IsActive, CreatedAt + DepartmentId? (FK → Departments)
│   │   ├── ApplicationRole.cs            ← Phase 1 | IdentityRole<Guid> + Description
│   │   ├── RefreshToken.cs               ← Phase 1 | Token + ExpiresAt + IpAddress + IsRevoked
│   │   ├── Customer.cs                   ← Phase 2 | Name, Email, Province, City + List<string> PreferredChannels (EF Core 9)
│   │   ├── CustomerPhone.cs              ← Phase 2 | Phone + IsPrimary + FK Customer
│   │   ├── DeviceBrand.cs                ← Phase 2 | Name (Unique)
│   │   ├── DeviceModel.cs                ← Phase 2 | Name + FK Brand (Composite Unique)
│   │   ├── CustomerDevice.cs             ← Phase 2 | IMEI, SerialNumber, PurchaseDate, WarrantyExpiry
│   │   ├── Call.cs                       ← Phase 3 | PhoneNumber, Direction, Duration, Notes + FK nullable Customer + FK Agent + FK nullable Ticket
│   │   ├── Department.cs                 ← Phase 4 | Name (Unique) + Description + IsActive + Users collection
│   │   ├── Ticket.cs                     ← Phase 4 | ID: T-YYYY-NNNNN + Category + Status + Priority + SLA fields + ChatwootConversationId
│   │   ├── TicketHistory.cs              ← Phase 4 | FromStatus, ToStatus, ChangedAt, TimeSpentInState
│   │   ├── InternalNote.cs               ← Phase 4 | Content + CreatedAt + FK Ticket + FK Agent
│   │   └── Attachment.cs                 ← Phase 4 | FileName, FileType, StorageUrl + FK Ticket + FK UploadedBy
│   └── Enums/                            (4 ملفات)
│       ├── CallDirection.cs              ← Inbound, Outbound
│       ├── TicketCategory.cs             ← 8 تصنيفات (Hardware, Software, Network, ...)
│       ├── TicketStatus.cs               ← 8 حالات (New, Open, InProgress, WaitingForCustomer, ...)
│       └── TicketPriority.cs             ← Low, Medium, High, Critical
│
├── UniGroup.CRM.Application/
│   ├── Common/Interfaces/               (4 ملفات)
│   │   ├── IApplicationDbContext.cs     ← DbSets لكل الكيانات + SaveChangesAsync
│   │   ├── IJwtProvider.cs              ← GenerateToken(user, roles)
│   │   ├── ITicketNumberGenerator.cs    ← GenerateNextAsync()
│   │   └── IFileStorageService.cs       ← SaveFileAsync + DeleteFileAsync
│   └── Features/                        (8 موديولات)
│       ├── Auth/
│       │   ├── Commands/Login/LoginCommand.cs                    ← Phase 1
│       │   ├── Commands/Register/RegisterCommand.cs              ← Phase 1
│       │   └── Common/AuthResponse.cs                           ← Phase 1
│       ├── Customers/
│       │   ├── Commands/CreateCustomer/CreateCustomerCommand.cs  ← Phase 2
│       │   └── Queries/
│       │       ├── Common/ (CustomerDetailsDto, CustomerDeviceDto, CustomerPhoneDto)
│       │       ├── GetCustomerDetails/GetCustomerDetailsQuery.cs ← Phase 2 | Compiled Query (EF Core 9)
│       │       └── SearchCustomers/SearchCustomersQuery.cs       ← Phase 2
│       ├── Devices/
│       │   └── Commands/
│       │       ├── AddCustomerDevice/AddCustomerDeviceCommand.cs ← Phase 2 | حساب Warranty تلقائي +2 سنة
│       │       ├── CreateDeviceBrand/CreateDeviceBrandCommand.cs ← Phase 2
│       │       └── CreateDeviceModel/CreateDeviceModelCommand.cs ← Phase 2
│       ├── Calls/
│       │   ├── Commands/LogCall/LogCallCommand.cs                ← Phase 3
│       │   └── Queries/
│       │       ├── Common/CallDto.cs
│       │       ├── GetCallHistory/GetCallHistoryQuery.cs         ← Phase 3
│       │       ├── GetCallerProfile/GetCallerProfileQuery.cs     ← Phase 3 | Compiled Query (EF Core 9)
│       │       └── SearchSystem/SearchSystemQuery.cs             ← Phase 3
│       ├── Tickets/
│       │   ├── Commands/
│       │   │   ├── CreateTicket/CreateTicketCommand.cs           ← Phase 4
│       │   │   ├── TransitionTicketStatus/TransitionTicketStatusCommand.cs ← Phase 4 | State Machine + SLA
│       │   │   ├── AssignTicket/AssignTicketCommand.cs           ← Phase 4
│       │   │   ├── AddInternalNote/AddInternalNoteCommand.cs     ← Phase 4
│       │   │   ├── AddAttachment/AddAttachmentCommand.cs         ← Phase 4
│       │   │   └── EscalateOverdueTickets/EscalateOverdueTicketsCommand.cs ← Phase 4
│       │   └── Queries/
│       │       ├── Common/ (TicketDetailsDto, TicketSummaryDto)
│       │       ├── GetTicketDetails/GetTicketDetailsQuery.cs     ← Phase 4
│       │       ├── GetTicketsList/GetTicketsListQuery.cs         ← Phase 4 | Paging + Filtering
│       │       └── GetMyTickets/GetMyTicketsQuery.cs             ← Phase 4
│       ├── Departments/
│       │   ├── Commands/CreateDepartment/CreateDepartmentCommand.cs ← Phase 4
│       │   └── Queries/
│       │       ├── Common/DepartmentDto.cs
│       │       └── GetDepartments/GetDepartmentsQuery.cs         ← Phase 4
│       ├── Dashboards/                                           ← Phase 5
│       │   └── Queries/
│       │       ├── Common/ (AgentPerformanceDto, DashboardSummaryDto, DeviceFailureReportDto, HourlyCallVolumeDto)
│       │       ├── GetDashboardSummary/GetDashboardSummaryQuery.cs     ← HybridCache + Tags
│       │       ├── GetAgentPerformance/GetAgentPerformanceQuery.cs     ← HybridCache + Tags
│       │       ├── GetDeviceFailureReport/GetDeviceFailureReportQuery.cs ← HybridCache + Tags
│       │       ├── GetHourlyCallVolume/GetHourlyCallVolumeQuery.cs     ← HybridCache + Tags
│       │       └── GetTicketsByStatus/GetTicketsByStatusQuery.cs       ← HybridCache + Tags
│       └── Reports/                                              ← Phase 5
│           └── Queries/ExportAgentReport/ExportAgentReportQuery.cs ← CSV Export Stream
│
├── UniGroup.CRM.Infrastructure/
│   ├── Data/ApplicationDbContext.cs      ← DbContext + Fluent API لكل الكيانات + PrimitiveCollection Config
│   ├── Services/                         (5 ملفات)
│   │   ├── JwtOptions.cs                ← POCO لإعدادات JWT
│   │   ├── JwtProvider.cs               ← توليد JWT + Refresh Token
│   │   ├── TicketNumberGenerator.cs     ← توليد T-YYYY-NNNNN بـ DB Lock
│   │   ├── LocalFileStorageService.cs   ← حفظ الملفات في wwwroot/uploads
│   │   └── SlaMonitorService.cs         ← BackgroundService يراقب SLA كل دقيقة
│   ├── DependencyInjection.cs           ← تسجيل كل الخدمات + HybridCache + JWT
│   └── Migrations/                      (7 migrations — 15 ملف)
│       ├── 20260701080141_InitialCreate
│       ├── 20260701085518_AddPhase2Entities
│       ├── 20260701092950_AddPhase3Calls
│       ├── 20260701093604_AddUniqueIndexesForBrandAndModel
│       ├── 20260702102445_AddPhase4TicketsWorkflowsAndSla
│       ├── 20260705110501_AddCustomerPreferredChannels
│       └── 20260705115426_AddCallTicketLinkAndUserDepartment
│
└── UniGroup.CRM.API/
    ├── Controllers/                     (9 controllers)
    │   ├── AuthController.cs            ← Phase 1 | /api/auth/login + /api/auth/register
    │   ├── CustomersController.cs       ← Phase 2 | /api/customers (CRUD + Search)
    │   ├── DevicesController.cs         ← Phase 2 | /api/devices (Brands + Models + Assign)
    │   ├── CallsController.cs           ← Phase 3 | /api/calls + /api/calls/caller-id + /api/calls/history
    │   ├── SearchController.cs          ← Phase 3 | /api/search (Unified Search)
    │   ├── TicketsController.cs         ← Phase 4 | /api/tickets (Full CRUD + Status + Notes + Attachments)
    │   ├── DepartmentsController.cs     ← Phase 4 | /api/departments
    │   ├── DashboardsController.cs      ← Phase 5 | /api/dashboard/* (5 endpoints)
    │   └── ReportsController.cs         ← Phase 5 | /api/reports/agents/export (CSV)
    ├── Program.cs                       ← Minimal API setup
    ├── appsettings.json                 ← ConnectionString + JWT settings
    └── UniGroup.CRM.API.http            ← HTTP test file (manual testing)
```

**إحصائيات الكود:**
| المشروع | عدد الملفات |
|---|:-:|
| Domain (Entities + Enums) | 18 ملف |
| Application (Commands + Queries + DTOs + Interfaces) | ~40 ملف |
| Infrastructure (DbContext + Services + Migrations) | ~20 ملف |
| API (Controllers + Config) | ~14 ملف |
| **الإجمالي** | **~92 ملف** |

---

## 5. تدقيق الأمان (Security Audit)

| النقطة | الحالة | التفاصيل |
|---|:-:|---|
| AgentId من JWT Claims وليس Request Body | ✅ | `User.FindFirstValue(ClaimTypes.NameIdentifier)` في جميع Controllers |
| كل الـ Endpoints محمية بـ `[Authorize]` | ✅ | AuthController فقط بدون Authorize |
| صلاحيات دقيقة بالأدوار | ✅ | `[Authorize(Roles = "Admin")]` للعمليات الحساسة، `[Authorize(Roles = "Admin,Team Leader")]` للتقارير |
| Unique constraints على مستوى DB | ✅ | لا يمكن تجاوزها حتى لو تجاوز الكود |
| Filtered Unique Indexes للـ NULL | ✅ | يمنع أخطاء FK مع IMEI وSerial الاختياريين |
| DeleteBehavior.Restrict عند حذف Agent | ✅ | يحافظ على سجلات المكالمات والتذاكر |
| DeleteBehavior.SetNull عند حذف Customer | ✅ | يحافظ على المكالمات بدون ربط للعميل |
| حد أقصى لحجم الملفات | ✅ | 10MB + امتدادات مسموحة فقط (صور + PDF) |
| HybridCache لا يكشف بيانات حساسة | ✅ | الكاش يخزن DTOs فقط بدون بيانات هوية |

---

## 6. تدقيق الأداء — تحسينات EF Core 9 المطبقة

### أ) Compiled Queries (استعلامات مترجمة مسبقاً):

```csharp
// في GetCallerProfileQuery.cs
private static readonly Func<ApplicationDbContext, string, IAsyncEnumerable<...>> GetCallerProfileQuery =
    EF.CompileAsyncQuery((ApplicationDbContext db, string phone) =>
        db.Customers.Where(c => c.CustomerPhones.Any(p => p.Phone == phone))...);

// في GetCustomerDetailsQuery.cs
private static readonly Func<ApplicationDbContext, Guid, Task<Customer?>> GetCustomerByIdQuery =
    EF.CompileAsyncQuery((ApplicationDbContext db, Guid id) =>
        db.Customers.Include(...).FirstOrDefault(c => c.Id == id));
```

**الفائدة:** تخفيض 20-30% من وقت ترجمة LINQ إلى SQL — مهم جداً لـ Caller ID الذي يُستدعى مئات المرات يومياً.

### ب) Primitive Collections (مجموعات أولية بدون جدول وسيط):

```csharp
// في Customer.cs
public List<string> PreferredChannels { get; set; } = new List<string>();

// لا يوجد تكوين Fluent API صريح — EF Core 9 يتعرف عليها تلقائياً (by convention)
// كـ Primitive Collection ويخزنها JSON في عمود nvarchar(max).
// القيمة الافتراضية '[]' معرّفة في الـ Migration فقط:
// (20260705110501_AddCustomerPreferredChannels → defaultValue: "[]")
```

**الفائدة:** تخزين `["WhatsApp", "Email"]` مباشرة كـ JSON دون جدول وسيط — يقلل الـ Joins ويبسط الاستعلامات.

### ج) HybridCache مع Cache Tags (Phase 5):

```csharp
// مثال في GetDashboardSummaryQuery.cs
return await _hybridCache.GetOrCreateAsync(
    "dashboard:summary",
    async _ => await BuildSummaryAsync(),
    tags: ["dashboard", "tickets", "calls"]
);
```

**الفائدة:** L1 Cache (In-Memory) + L2 Cache (Distributed) + Tag-Based Invalidation جاهزة للمرحلة 6.

---

## 7. نتائج الاختبارات الكاملة

### Phase 2 — 16/16 ✅

| # | الاختبار | Endpoint | النتيجة |
|:-:|---|---|:-:|
| T1 | Create Customer | POST /api/customers | ✅ 201 |
| T2 | رفض هاتف مكرر | POST /api/customers | ✅ 400 |
| T3 | Get Customer 360° مع Warranty Active | GET /api/customers/{id} | ✅ 200 |
| T4 | Customer غير موجود | GET /api/customers/{id} | ✅ 404 |
| T5 | Search by Name | GET /api/customers/search | ✅ 200 |
| T6 | Search by Phone | GET /api/customers/search | ✅ 200 |
| T7 | Create Brand `Nokia` | POST /api/devices/brands | ✅ 200 |
| T8 | رفض Brand مكرر | POST /api/devices/brands | ✅ 400 |
| T9 | Create Model `Nokia 3310` | POST /api/devices/models | ✅ 200 |
| T10 | رفض Model مكرر تحت نفس الماركة | POST /api/devices/models | ✅ 400 |
| T11 | Assign Device (auto-warranty +2 سنة) | POST /api/devices/assign | ✅ 200 |
| T12 | Get 360° بعد ربط الجهاز | GET /api/customers/{id} | ✅ 200 |
| T13 | رفض نفس IMEI لعميل آخر | POST /api/devices/assign | ✅ 400 |
| T14 | Search by IMEI | GET /api/customers/search | ✅ 200 |
| T15 | Search by Serial | GET /api/customers/search | ✅ 200 |
| T16 | Search — لا نتائج | GET /api/customers/search | ✅ 200 (0 results) |

### Phase 3 — 10/10 ✅

| # | الاختبار | Endpoint | النتيجة |
|:-:|---|---|:-:|
| T1 | Caller ID — رقم معروف | GET /api/calls/caller-id | ✅ 200 + Profile |
| T2 | Caller ID — رقم مجهول | GET /api/calls/caller-id | ✅ 200 + null |
| T3 | Log Call — عميل معروف | POST /api/calls | ✅ 201 |
| T4 | Log Call — customerId = null | POST /api/calls | ✅ 201 |
| T5 | Call History للعميل | GET /api/calls/history/{id} | ✅ 200 |
| T6 | Unified Search بالاسم | GET /api/search?q=Ahmed | ✅ 200 |
| T7 | Unified Search بالهاتف | GET /api/search?q=010... | ✅ 200 |
| T8 | Unified Search بالـ IMEI | GET /api/search?q=359... | ✅ 200 + Device |
| T9 | Unified Search بالـ Serial | GET /api/search?q=S24U... | ✅ 200 |
| T10 | Unified Search — لا نتائج | GET /api/search?q=XXXXX | ✅ 200 (0 results) |

### Phase 4 — 15/15 ✅

| # | الاختبار | Endpoint | النتيجة |
|:-:|---|---|:-:|
| T1 | Login Authenticated | POST /api/auth/login | ✅ 200 |
| T2 | Create Department (Admin) | POST /api/departments | ✅ 201 |
| T3 | Get Departments | GET /api/departments | ✅ 200 |
| T4 | Create Ticket (Agent) | POST /api/tickets | ✅ 201 |
| T5 | Assign Ticket | PATCH /api/tickets/{id}/assign | ✅ 200 |
| T6 | Add Internal Note | POST /api/tickets/{id}/notes | ✅ 200 |
| T7 | Add Attachment (.jpg) | POST /api/tickets/{id}/attachments | ✅ 200 |
| T8 | Get Ticket Details | GET /api/tickets/{id} | ✅ 200 |
| T9 | Get Tickets List with Paging | GET /api/tickets | ✅ 200 |
| T10 | Get My Tickets | GET /api/tickets/my | ✅ 200 |
| T11 | Transition New → Open (Valid) | PATCH /api/tickets/{id}/status | ✅ 200 |
| T12 | Transition Open → Resolved (Invalid) | PATCH /api/tickets/{id}/status | ✅ 400 |
| T13 | Transition Open → InProgress (Valid) | PATCH /api/tickets/{id}/status | ✅ 200 |
| T14 | Transition InProgress → WaitingForCustomer (Pauses SLA) | PATCH /api/tickets/{id}/status | ✅ 200 |
| T15 | Transition WaitingForCustomer → InProgress (Resumes SLA) | PATCH /api/tickets/{id}/status | ✅ 200 |

### Phase 5 — 8/8 ✅

| # | الاختبار | Endpoint | النتيجة |
|:-:|---|---|:-:|
| T1 | Login Authenticated | POST /api/auth/login | ✅ 200 |
| T2 | Get Dashboard Summary | GET /api/dashboard/summary | ✅ 200 |
| T3 | Get Agent Performance | GET /api/dashboard/agent-performance | ✅ 200 |
| T4 | Get Device Failure Report | GET /api/dashboard/device-failures | ✅ 200 |
| T5 | Get Hourly Call Volume | GET /api/dashboard/call-volume | ✅ 200 |
| T6 | Get Tickets By Status | GET /api/dashboard/tickets-by-status | ✅ 200 |
| T7 | Export Agent Report CSV | GET /api/reports/agents/export | ✅ 200 CSV |
| T8 | Dashboard without JWT | GET /api/dashboard/summary | ✅ 401 |

**الإجمالي: 49/49 اختبار ناجح — 100% ✅**

---

## 8. مشاكل اكتُشفت وتم إصلاحها

| # | المشكلة | النوع | الملف | الإصلاح |
|:-:|---|---|---|---|
| 1 | `GetCallerProfileQuery`: استخدام `.Select(p => p.Customer).Include(...)` — EF Core لا يضمن Include بعد Select | 🔴 Critical | GetCallerProfileQuery.cs | إعادة كتابة: `Customers.Where(c => c.CustomerPhones.Any(p => p.Phone == phone))` |
| 2 | `DeviceBrand.Name` بدون Unique Index على مستوى DB | 🟡 Data Integrity | ApplicationDbContext.cs | `HasIndex(db => db.Name).IsUnique()` |
| 3 | `DeviceModel` بدون Composite Unique Index | 🟡 Data Integrity | ApplicationDbContext.cs | `HasIndex(dm => new { dm.BrandId, dm.Name }).IsUnique()` |
| 4 | `LogCallCommand` مع `customerId = null` يرجع 500 — `CreatedAtAction` يفشل مع null parameter | 🔴 Critical | CallsController.cs | `if (CustomerId == null) → return Created($"/api/calls/{callId}", callId)` |
| 5 | `DbUpdateConcurrencyException` في AssignTicket وTransitionStatus بسبب Navigation Property مع Tracker مُلوث | 🔴 Critical | AssignTicketCommand.cs / TransitionTicketStatusCommand.cs | `AsNoTracking()` + `_context.TicketHistories.Add()` مباشرة بدلاً من `ticket.Histories.Add()` |
| 6 | اختبار T7 يرفع `.txt` والكود يرفض غير الصور والـ PDF | 🟢 Testing | run_phase4_tests.ps1 | تعديل الاختبار لرفع `.jpg` + إضافة `-UseBasicParsing` |

---

## 9. منطق الضمان (Warranty Logic Audit)

```csharp
// في AddCustomerDeviceCommand.cs — حساب تلقائي
var warrantyExpiry = request.WarrantyExpiry ?? request.PurchaseDate.AddYears(2);

// في GetCustomerDetailsQuery.cs — حالة الضمان
WarrantyStatus = d.WarrantyExpiry > currentDate ? "Active" : "Expired"
```

**نتيجة الاختبار الفعلي:** شراء `2026-07-01` → Expiry `2028-07-01` → Status: `Active` ✅

---

## 10. الخطوات القادمة — المرحلة 6 (قيد التنفيذ ⏳)

### مكونات المرحلة 6 (Notifications, Chatwoot, CSAT & Audit Trail):

| المكون | الوصف | الأولوية |
|---|---|:-:|
| **Chatwoot Self-Hosted** | استضافة Chatwoot محلياً عبر Docker Compose (مجاناً + خصوصية كاملة) | 🔴 عالية |
| **Secure Webhook Ingest** | استقبال Webhooks من Chatwoot بأمان تام والتحقق من التوقيع الرقمي (HMAC-SHA256) عبر الهيدرز `X-Chatwoot-Signature` و `X-Chatwoot-Timestamp` لمنع الاختراقات وتفادي كتابة التوكنز في السجلات. | 🔴 عالية |
| **Idempotent Ticket Link** | تحويل المحادثات لتذاكر تلقائياً (General Inquiry) مع ربط الـ `ChatwootConversationId` لضمان عدم تكرار التذاكر لنفس المحادثة. | 🔴 عالية |
| **Audit Trail (EF Core 9)** | `AuditSaveChangesInterceptor` لحفظ العمليات (أضف/عدل/احذف) تلقائياً بصيغة JSON مع Complex Types (`ClientInfo_IpAddress`, `ClientInfo_UserAgent`) لتخزين بيانات الموظف المجرى للعملية. | 🟡 متوسطة |
| **Audit Log Archiver** | خدمة خلفية `AuditLogArchiverService` لقص وأرشفة السجلات الأقدم من 6 أشهر يومياً لتفادي تضخم قاعدة البيانات (Data Bloat). | 🟡 متوسطة |
| **إشعارات Event-Driven** | تحديثات حالة التذكرة وإنذارات خرق SLA وتكليفات الموظفين عبر البريد الإلكتروني وتنبيهات In-App ورسائل Chatwoot آلياً. | 🟡 متوسطة |
| **CSAT** | روابط تقييم آمنة تحتوي على Guid فريد يرسل تلقائياً للعميل عند إغلاق التذكرة، بصلاحية 7 أيام ولمرة واحدة فقط. | 🟢 منخفضة |
| **قاعدة المعرفة** | خطوات توجيهية للموظف حسب تصنيف التذكرة لتسهيل التشخيص السريع. | 🟢 منخفضة |


### Cache Tags جاهزة للـ Invalidation في المرحلة 6:

التحسين الذي تم تطبيقه في Phase 5 (إضافة `tags` لكل استعلامات HybridCache) سيُمكّن في المرحلة 6 من:
```csharp
// مثال: عند تعديل تذكرة → تطهير كاش لوحة التحكم فوراً
await _hybridCache.RemoveByTagAsync("tickets");
await _hybridCache.RemoveByTagAsync("dashboard");
```
