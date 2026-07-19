# UniGroup CRM — Blazor WebAssembly Frontend Progress Tracker

> **Rule:** Updated at the end of EVERY task. Reviewed at the start of every session.

## Stack Decision (FINAL)
- **Blazor WebAssembly (.NET 9 / C#)** — project `src/UniGroup.CRM.Client`, added to `UniGroup.CRM.sln`.
- References **`UniGroup.CRM.Application`** directly → all DTO records (CustomerDetailsDto, TicketDetailsDto, DashboardSummaryDto, KnowledgeBaseArticleDto, …) and `UniGroup.CRM.Domain` enums (TicketStatus/Priority/Category, CallDirection) reused with ZERO duplication.
- Packages: `Microsoft.AspNetCore.Components.Authorization` (auth state), `Markdig` (KB Markdown rendering).
- ~~React/Vite frontend~~ **deleted** per instruction (100% C#/.NET ecosystem).
- API base: `http://localhost:5112` (configurable in `wwwroot/appsettings.json`).
- Premium design system: custom CSS variables, glassmorphism, Inter + Outfit Google Fonts, dark/light theme toggle, skeleton loaders, micro-animations.

## Backend API Map (audited from Controllers — 42 endpoints)
| Area | Endpoints | Auth |
|---|---|---|
| Auth | POST `/api/auth/register`, POST `/api/auth/login` → `AuthResponse{Token, TokenExpiration, RefreshToken, Email, FirstName, LastName}` | Anonymous. **No refresh endpoint** — 401 → force re-login. |
| Customers | POST `/api/customers`, GET `/api/customers/{id}`, GET `/api/customers/search?searchTerm=` | Authenticated |
| Calls | POST `/api/calls` (AgentId from JWT), GET `/api/calls/caller-id?phoneNumber=` (null body if unknown), GET `/api/calls/history/{customerId}?page&pageSize` | Authenticated |
| Devices | POST `/api/devices/brands`, `/api/devices/models`, `/api/devices/assign` | Authenticated |
| Departments | GET/POST `/api/departments` | Authenticated |
| Tickets | POST `/api/tickets`, GET `/api/tickets/{ticketId}`, GET `/api/tickets` (filters + paging → `PagedResult<TicketSummaryDto>`), GET `/api/tickets/my?status=`, PATCH `/{id}/status`, PATCH `/{id}/assign`, POST `/{id}/notes`, POST `/{id}/attachments` (multipart `file`) | Authenticated |
| Dashboard | GET `/api/dashboard/summary`, `/agent-performance`, `/device-failures`, `/call-volume`, `/tickets-by-status` | **Admin, Team Leader** |
| Reports | GET `/api/reports/agents/export?dateFrom&dateTo` → CSV | **Admin** |
| CSAT | POST `/api/surveys/submit` `{token, rating, feedback}` (**anonymous**), GET `/api/surveys/report` (Admin/TL), GET `/api/surveys/ticket/{ticketId}` (Admin) | mixed |
| Knowledge Base | GET `/api/knowledge-base` (paged/search/category/isActive), GET `/category/{category}`, GET `/{id}`; POST/PUT/DELETE (**Admin**) | read: authenticated |
| Notifications | GET `/api/notifications/logs` | **Admin** |
| Audit Logs | GET `/api/audit-logs`, GET `/api/audit-logs/{id}` | **Admin** |
| Search | GET `/api/search?q=` → `List<CustomerDetailsDto>` | Authenticated |

### Key facts
- SLA hours by priority: Low=120h, Medium=72h, High=24h, Critical=4h. SLA pauses in `WaitingForCustomer`/`WaitingForParts` (uses `SlaPausedAt`, `TotalPausedSeconds`).
- Ticket read DTOs return Status/Priority/Category as **strings**; write DTOs use numeric enums.
- Roles in JWT `ClaimTypes.Role`: `Admin`, `Team Leader` (also `TeamLeader`), `Agent`, `User`. `sub` = user GUID, `name` = full name.
- Seed test user: `testuser@unigroup.com` / `Password123!` (all roles).

---

## Task Status

### ✅ Task 0 — Cleanup & Setup (DONE)
- [x] Deleted React/Vite/npm `frontend/` directory — workspace is 100% .NET.
- [x] Installed .NET 9 SDK (9.0.315) in sandbox.
- [x] Created `src/UniGroup.CRM.Client` (Blazor WASM, net9.0, no-https template).
- [x] Added to `UniGroup.CRM.sln` under `src` solution folder.
- [x] Referenced `UniGroup.CRM.Application` (transitively `UniGroup.CRM.Domain`) — DTO reuse verified by build.
- [x] Added `Microsoft.AspNetCore.Components.Authorization` + `Markdig`. Build: **0 warnings, 0 errors**.

### ✅ Foundation (DONE)
- [x] `wwwroot/index.html` (Google Fonts, pre-paint theme script, boot loader), `wwwroot/css/theme.css` + `app.css` premium design system (~29KB, CSS variables, glassmorphism, dark/light)
- [x] `wwwroot/appsettings.json` (ApiBaseUrl = http://localhost:5112)
- [x] Services: `LocalStorageService` (JS interop), `JwtAuthenticationStateProvider`, `CrmApiClient` (typed, all 42 endpoints, Bearer + 401→re-login), `ToastService`, `ThemeService`, `CallerIdService`, `UiHelpers`
- [x] `Program.cs` DI wiring; `Models/Requests.cs` mirrors all controller request records

### ✅ Task 1 — Auth & Layout (DONE, committed)
- [x] Custom `AuthenticationStateProvider` w/ localStorage JWT (`unigroup.session`) + persistent session check + expiry validation (30s skew)
- [x] Login page (premium glass card, expired banner, returnUrl), Register page
- [x] Responsive sidebar layout + topbar + debounced global search (350ms) + light/dark theme toggle
- [x] Role-aware nav (Dashboard/CSAT/Reports gated by `AuthorizeView Roles`)
- [x] `AuthorizeRouteView` route guards + RedirectToLogin

### ✅ Task 2 — Customer 360° View (DONE, committed)
- [x] Customer directory w/ search (name/phone/IMEI/serial), card grid, skeletons
- [x] Create customer modal form
- [x] Customer details: glass info header, phones, devices w/ real-time warranty badges, call history + tickets tab timelines
- [x] Device linking panel (assign device modal — create brand → model → assign chain)

### ✅ Task 3 — Caller ID Simulation Popup (DONE, committed)
- [x] Floating bottom-corner widget: dial pad → ring animation + WebAudio ringtone
- [x] Caller-ID lookup → known: answer opens 360° profile instantly; unknown: quick-create customer
- [x] Log call (Inbound, duration, summary) on end

### ✅ Task 4 — Ticket Lifecycle & SLA Timer (DONE, committed)
- [x] Tickets list w/ filters (status, priority, department, dates) + pagination (12/page) + My Tickets card grid
- [x] Ticket creation form (debounced customer picker, device dropdown w/ warranty badge, category w/ live KB guidance side panel, priority w/ SLA hours)
- [x] Ticket details: status transition panel (valid-transitions map, required note on Resolve/Cancel), department assignment + assign-to-me, internal notes thread, attachment upload (InputFile, 10MB), history timeline
- [x] Live SLA countdown `<SlaTimer>` (1s tick): green → amber (<25%) → red (<10%) → BREACHED; paused in waiting states; met/breached terminal display

### ✅ Task 5 — Dashboards & Reporting (DONE, committed)
- [x] Metric cards (new today / open / SLA breached / avg resolution + calls), date picker + refresh
- [x] Tickets-by-status horizontal bars, hourly call volume spark chart w/ hover tooltips
- [x] Agent performance grid (handled/open/avg resolution/SLA badge/CSAT)
- [x] Device failure breakdown (top 10, repeat customers, common category)
- [x] `/reports` (Admin): CSV export via base64 JS download; `/csat-report` (Admin/TL): rating distribution

### ✅ Task 6 — Knowledge Base Guidance (DONE, committed)
- [x] Interactive guidance page: category chips → 4 color-coded Markdig-rendered sections (Questions / Diagnosis / Answers / Escalation) + keyword tags
- [x] Article directory w/ debounced search + pagination; Admin CRUD modal (markdown editors, active toggle, delete confirm)

### ✅ Task 7 — Public CSAT Rating Screen (DONE, committed)
- [x] Public route `/survey/{Token}` (EmptyLayout, anonymous) — animated 1–5 star selector w/ hover + emoji labels + feedback textarea
- [x] Success / error / already-submitted / expired states (server message surfaced)

### ✅ Task 8 — 4 CRM Enhancements (DONE, committed)
- [x] Task 8.1: Added Customer Group field (`Customer.CustomerGroup`) to Create Customer modal, updated API/DTOs, and displayed group badge in Customer 360° profile.
- [x] Task 8.2: Built Excel bulk import using `MiniExcel 1.45.0` (template download endpoint, upload parser endpoint, phone uniqueness check, and 3-step UI upload wizard).
- [x] Task 8.3: Split ticket category into Cascading dropdowns (Main Category enum: Maintenance, Complaint, General Support + Sub-category) on Create Ticket form and displays.
- [x] Task 8.4: Added call classification (cascading optional categories) in Caller ID floating popup and display in customer calls history.

### ✅ Task 9 — Customer Edit & Directory Auto-Load / Instant Search (DONE, committed)
- [x] Task 9.1: Added "Complete/Update Customer Data" button ("استكمال البيانات") to Customer 360° header, implemented Edit Modal (EditForm), added PUT endpoint on backend, and updated client API.
- [x] Task 9.2: Changed Customers Directory tab to auto-load all customers by default upon page mounting, and added debounced instant search (300ms) on typing, with automatic fallback to full list if search is cleared.

### ✅ Final Verification
- [x] Client project build passes — 0 warnings, 0 errors (verified after every task)
- [x] Full solution build passes
- [x] Commits per task on `genspark_ai_developer`, pushed + PR created
- [x] Applied migrations: `AddCustomerGroupToCustomer`, `AddMainCategoryToTickets`, `AddCallClassification`

## Session Log
- **2026-07-14 (a)**: Backend audit complete (14 controllers, DTOs, enums, JWT claims). API map documented.
- **2026-07-14 (b)**: Direction changed to **Blazor WASM only** — React artifacts deleted; .NET 9 SDK installed; Client project created, added to sln, referencing Application; baseline build green. Next: foundation services + design system.
- **2026-07-14 (c)**: Foundation + Tasks 1–3 built & committed (auth provider, layout, Customer 360°, Caller-ID widget).
- **2026-07-14 (d)**: Task 4 complete — Tickets list/MyTickets/TicketCreate (with embedded KB guidance panel)/TicketDetails (transitions, assignment, notes, attachments, history, big SLA timer). Build green, committed.
- **2026-07-14 (e)**: Task 5 complete — real dashboard (metric cards, status bars, call-volume spark chart, agent grid, device failures), Reports CSV export, CSAT report. Build green, committed.
- **2026-07-14 (f)**: Task 6 complete — Knowledge Base guidance viewer + admin CRUD. Task 7 complete — public survey page. Build green, committed. Final: solution build + push + PR.
- **2026-07-19 (a)**: Sandbox AI completed 4 enhancements. Merged with master local configs & seeding exit fixes. EF migrations applied to database and servers started. Added Task 8 to tracker.
- **2026-07-19 (b)**: Fixed Caller ID Widget typing button-disabled issue. Sandbox AI completed Task 9 (Customer Edit & Auto-load / Instant Search). Pulled, clean built with 0 warnings/errors, restarted servers. Added Task 9 to tracker.

