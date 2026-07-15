# Implementation Plan — Arabic/English Localization, RTL Styling & Emoji-to-Icon Migration

> Feature branch: `feature/arabic-rtl-icons`
> Scope: `src/UniGroup.CRM.Client` (Blazor WASM) only — no backend changes.

## 1. Architecture Overview

### 1.1 Language pipeline
```
index.html pre-paint script  ──►  <html lang="ar" dir="rtl">   (no flash on reload)
        ▲                                   ▲
        │ localStorage['unigroup.lang']     │ JS interop (unigroup.setLanguage / getLanguage)
        │                                   │
LanguageService (C#, scoped) ──► OnChange event ──► components re-render
```

- **`wwwroot/js/app.js`** — add `setLanguage(lang)` / `getLanguage()` helpers that set
  `document.documentElement` `lang` + `dir` attributes and persist to `localStorage`.
- **`wwwroot/index.html`** — pre-paint script (like the theme script) applies the persisted
  language/direction before first paint; also imports **Bootstrap Icons 1.11.3** CDN and the
  **Cairo** Google Font for Arabic typography.
- **`Services/LanguageService.cs`** — modeled after `ThemeService`: `Current` ("en"/"ar"),
  `OnChange` event, `InitializeAsync()`, `SetLanguageAsync(lang)`, `ToggleAsync()`,
  `IsRtl => Current == "ar"`. Registered scoped in `Program.cs`.

### 1.2 Translation resources
- **`Services/TranslationResources.cs`** — one central
  `Dictionary<string, Dictionary<string, string>>` with namespaced keys
  (`Nav.*`, `Common.*`, `Login.*`, `Customers.*`, `Tickets.*`, `TicketCreate.*`,
  `TicketDetails.*`, `Kb.*`, `Dash.*`, `Csat.*`, `Reports.*`, `Survey.*`, `Caller.*`,
  `Toast.*`, `Validation.*`, `Status.*`, `Priority.*`, `Category.*`, `Warranty.*`, `Time.*`)
  and a `Get(key, lang)` accessor that falls back to the key itself.

### 1.3 Component wiring (re-render on switch)
- **`Components/LocalizedComponentBase.cs`** — a `ComponentBase` that injects
  `LanguageService`, subscribes to `OnChange` (auto `StateHasChanged`), and exposes helpers:
  - `T(key)` — translate,
  - `TStatus(s)` / `TPriority(p)` / `TCategory(c)` / `TWarranty(w)` — enum label shortcuts,
  - `TimeAgo(utc)` — localized relative time,
  - `IsRtl`.
  All pages/components with UI text switch to `@inherits LocalizedComponentBase`
  (existing `OnInitialized`/`Dispose` overrides call `base`).
- **`Components/LocalizedValidationMessage.razor`** — a drop-in replacement for
  `ValidationMessage<T>` that translates DataAnnotations error messages: attributes carry a
  translation **key** (e.g. `Validation.EmailRequired`) which gets translated at render time.
- **`UiHelpers`** — `TimeAgo` gains a lang-aware overload; `CategoryIcon` now returns a
  **Bootstrap Icons class name** instead of an emoji.

### 1.4 Fully localized surfaces
Pages: Login, Register, Home(Dashboard via DashboardContent), Customers, CustomerDetails,
Tickets, TicketCreate, TicketDetails, MyTickets, KnowledgeBase, Reports, CsatReport, Survey,
App.razor (404).
Components: MainLayout (nav, search, footer), CallerIdWidget, DashboardContent, ToastHost,
SlaTimer, Modal titles (passed from callers).
Service-side text: all toasts, empty states, placeholders, validation and error messages
raised from the client (server `ApiException` messages pass through as-is).

## 2. RTL Styling Strategy

1. **Logical properties first**: convert `margin-left`, `padding-left/right`, `left/right`
   offsets, `border-left`, `text-align: left/right` → `margin-inline-start`,
   `padding-inline-*`, `inset-inline-*`, `border-inline-start`, `text-align: start/end`
   in `app.css` + `theme.css`. Flex/grid auto-mirror for free.
2. **Targeted `[dir="rtl"]` overrides** for the things logical properties can't express:
   - sidebar pinned side (`inset`) + mobile off-canvas transform (`translateX(100%)`),
   - `.md-content blockquote` corner radii,
   - directional icon flipping via a `.flip-rtl` utility (back arrows, chevrons).
3. **Arabic typography**: `html[lang="ar"]` swaps `--font-body` / `--font-display` to
   **Cairo** for proper Arabic glyph rendering.
4. **Crucial checks**: responsive sidebar (desktop + mobile drawer), customer timeline
   (rail + dots), dashboard bar/spark charts, modals, caller-ID floating popup (FAB + panel
   anchored to the inline-end corner), toasts, tables, forms, pagination.

## 3. UI Language Toggle
A pill button next to the theme toggle in the `MainLayout` topbar showing the *target*
language (`عربي` when in English, `EN` when in Arabic). Clicking calls
`LanguageService.ToggleAsync()` → JS updates `lang`/`dir` → `OnChange` re-renders every
localized component instantly (no page reload).

## 4. Emoji → Bootstrap Icons Migration
Import `bootstrap-icons@1.11.3` CDN in `index.html`, then replace **every** emoji:

| Emoji | Icon | | Emoji | Icon |
|---|---|---|---|---|
| 📊 Dashboard | `bi-speedometer2` | | 👥 Customers | `bi-people` |
| 🎫 Tickets | `bi-ticket-perforated` | | 📋 My Tickets | `bi-clipboard-check` |
| 📚 Knowledge Base | `bi-book` | | ⭐ CSAT | `bi-star` / `bi-star-fill` |
| 📤 Reports | `bi-file-earmark-spreadsheet` | | 🔎 Search | `bi-search` |
| 🌙/☀️ Theme | `bi-moon` / `bi-sun` | | ↩ Sign out | `bi-box-arrow-left` |
| ☰ Menu | `bi-list` | | ➕ | `bi-plus-lg` |
| 📞/📳/📵 Caller-ID | `bi-telephone` / `bi-phone-vibrate` / `bi-telephone-x` | | 🎧 | `bi-headset` |
| ✅/⚠️/ℹ️ Toasts | `bi-check-circle-fill` / `bi-exclamation-triangle-fill` / `bi-info-circle-fill` |  | ⏱/⏸/⏰ SLA | `bi-stopwatch` / `bi-pause-circle` / `bi-alarm` |
| 💬 Notes | `bi-chat-dots` | | 📎 Attachments | `bi-paperclip` |
| 🕓 History | `bi-clock-history` | | 🖼/🎞/🎧/📕/📄 files | `bi-file-earmark-*` |
| 😠…🤩 CSAT faces | `bi-emoji-frown-fill` … `bi-emoji-laughing` | | ★ star selector | `bi-star-fill` |
| Ticket categories (📱🔋🔌💾📶📷🔊🛠️🛡️💬) | `bi-phone`, `bi-battery-half`, `bi-plug`, `bi-cpu`, `bi-wifi`, `bi-camera`, `bi-volume-up`, `bi-tools`, `bi-shield-check`, `bi-chat-dots` |

`<select><option>` elements cannot contain `<i>` markup → options show text-only labels;
icon chips/badges elsewhere use `<i class="bi …">`.

## 5. Execution Order
1. `git checkout -b feature/arabic-rtl-icons`
2. JS interop + index.html (pre-paint script, Bootstrap Icons, Cairo font)
3. `LanguageService`, `LocalizedComponentBase`, `LocalizedValidationMessage`, DI wiring
4. `TranslationResources.cs` (full EN/AR dictionary, ~400 keys)
5. Rewrite layout, all pages & components: `T(...)` keys + icon migration
6. RTL pass over `app.css` / `theme.css`
7. Key-coverage audit script (every `T("…")` present in the dictionary)
8. `dotnet build` → 0 warnings / 0 errors
9. Commit & push `feature/arabic-rtl-icons`
