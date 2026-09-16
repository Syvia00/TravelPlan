# TravelPlan — Project Plan (v2)

Supersedes `Travel_Plan_Project.docx` / `TravelPlan_Updated.docx` and the interim
feature-list notes. This is the current source of truth for the rebuild — hand this
file, `erd.md`, `architecture.md`, and `travelplan-home-mockup.html` to Claude Code
as starting context.

---

## 1. What this is

A travel planning and memory app, built as a Blazor WebAssembly web app and a .NET
MAUI mobile app, sharing one ASP.NET Core API and one Azure SQL database.

A **trip is a self-contained project**: a date range (minimum 1 day) holding one or
more destinations, an itinerary, a budget, and everything else inside it. A user
creates many trips; each is independent. Planning happens on a **calendar** that
zooms from year down to a single day, and once a trip is done, the app generates a
shareable visual summary of it automatically.

The whole interface is styled as a **diary/notebook** — a closed book to sign in,
an open two-page spread as the home screen, and a half-open book for everything else
(settings, reports, trip detail).

---

## 2. Feature scope

Phase legend: **1** core CRUD, dev-auth, SQLite, no Azure · **2** deployed skeleton
(Azure, still dev-auth) · **3** real auth (Entra External ID + phone OTP) · **4** MAUI mobile

| Area | What it does | Phase |
|---|---|---|
| **Trips & calendar** | Trip = project; calendar zooms year→month→week→day; a trip is one block on month/year views; day view is a vertical timeline | 1 |
| **Plan items** | Unified model for itinerary + to-do: an item with optional `Date`/`Time`. No date → trip backlog. Date, no time → "unscheduled today" on that day. Date + time → on the timeline. Drag between states. | 1 |
| **Live time tracking** | "3 days until Tokyo" / "next stop in 4 hours" — computed client-side from existing dates, no notifications infra | 1 |
| **Budget** | Add items by category, running total. Live currency conversion (Frankfurter, free/no key, cached, refreshed on a schedule) | 1 (fixed currency) → 2–3 (live FX) |
| **Accommodation & transport** | Add records per trip. Optional static map thumbnail per transport leg (Azure Maps) | 1 (records) → 2+ (map) |
| **Companions & sharing** | Invite by email (pending until they sign up). Roles: Owner / Editor / Viewer, named or via anonymous link (Google Docs-style) | 2 |
| **Reports & memories** | Itinerary report (chronological, available anytime) + Memory summary (auto-generated on completion, poster-style with a reflection area). One render pipeline (SkiaSharp → PNG/PDF, plus an HTML template for the shareable web view) | 1 (basic itinerary report) → 2 (memory summary + all formats) |
| **Photos & journaling** | Attach photos to a plan item, visible only at day-view zoom. Simple attachment in v1, no custom layout yet | 2 |
| **Quick capture** | Paste a link/caption → text extracted (no scraping) → LLM summarizes into a backlog item, original link kept | 3+ |
| **Accounts & auth** | Email + Google via Microsoft Entra External ID; phone-number sign-up built independently via Azure Communication Services SMS OTP | 1 (dev-auth) → 3 (real) |

**Later, not scoped**: passport/visa reminders, smarter quick-capture (suggest items
from saved places).

**Explicitly out of scope**: payment/subscriptions/booking integrations, automatic
import from photos/emails/bookings, full itinerary editing on mobile, CSV export,
i18n (English only).

---

## 3. Data model changes vs. current `erd.md`

- **New**: `PlanItems` (nullable `Date`/`Time`, replaces `ItineraryDays`),
  `TripCollaborators` (named roles), `TripShareLinks` (anonymous link + role),
  `ExchangeRates` (FX cache), `PhoneVerifications` (OTP codes)
- **Changed**: `Users.B2CObjectId` → `Users.ExternalAuthId` (provider-agnostic) +
  `Users.PhoneNumber` / `PhoneVerifiedAt`; `TripMemories` needs a `ReportType`
  discriminator (itinerary vs. memory summary) or one row per type
- **Dropped**: `ItineraryDays` (folded into `PlanItems`); GitHub as a social login
  option (Google only)

`erd.md` itself hasn't been redrawn yet — this is the delta to apply when it is.

---

## 4. UI design — book/diary concept

- **Landing/auth**: closed diary, right side of screen, cover reads "Log in /
  Sign up." App name/tagline on the left. Clicking the cover is the entry point.
- **Home**: the diary opens flat, two pages. Left = budget + quick actions. Right =
  the calendar. Realistic notebook look — center fold shadow, not flat icons.
  Persistent settings icon, top-right, on every page.
- **Secondary pages** (settings, reports, trip detail): asymmetric half-open book —
  narrow left page as nav/table-of-contents, wide right page as content. The page
  itself is fixed size; only the content inside the right panel scrolls.
- **Interaction**: moving *between* screens uses a page-turn animation. Scrolling is
  only for content *within* one panel, never for navigating between screens.
  Desktop-first for now.
- **Palette**: pale yellow `#FFF6C8` / `#FFF3B0` (page tone), soft blue `#D3ECFF` /
  `#A9D6E5` (secondary panel), deep blue `#2C7DA0` (accent/CTA), off-white `#FAFAFA`
  (app background). Clean, soft, nostalgic rather than corporate.
- **Reference build**: `travelplan-home-mockup.html` — a real, working HTML/CSS
  mockup of the home screen: dimensional book shadow (layered "page stack" behind
  the spread, soft multi-layer drop shadow so it reads as an object on a desk), a
  center spine shadow, SaaS-card-style content inside, and a desktop/mobile toggle
  that demonstrates the same design reflowing into a single-page mobile layout with
  a page-turn transition between tabs. Use it as the design-token source (colors,
  shadow values, spacing, type) — it's a style reference to translate into Blazor
  components, not literal drop-in markup. The closed-cover landing page and the
  half-open settings/report page still need the same treatment applied.

---

## 5. Technology stack

> **Target framework: .NET 10 (LTS), not .NET 8.** The original plan pinned .NET 8,
> written before .NET 10 existed. As of now, .NET 8 reaches end of support November
> 10, 2026, and .NET MAUI 8 and 9 are *already* out of support — only .NET MAUI 10
> (paired with .NET 10) is currently supported. Switched while only
> `TravelPlan.Shared` existed, to avoid building the rest on a framework about to
> lose support.

| Layer | Technology | Notes |
|---|---|---|
| Backend language | C# 14 / .NET 10 (LTS) | |
| Web API | ASP.NET Core 10 Web API | RESTful + minimal APIs for light endpoints; Swagger auto-generated |
| ORM | Entity Framework Core 10 | Code-first migrations; SQLite (dev) / Azure SQL (prod) |
| Web frontend | Blazor WebAssembly | No JS framework; page-turn transitions in CSS/JS interop |
| Mobile | .NET MAUI 10 | iOS + Android, MVVM, shares HttpClient services with Web |
| Shared library | .NET 10 class library | Models, DTOs, FluentValidation — referenced by API, Web, Mobile |
| Auth | **Microsoft Entra External ID** | Replaces Azure AD B2C — new B2C tenants have been unavailable since May 2025; Entra External ID is the current equivalent product, same architectural role. Email + Google via MSAL. |
| Phone sign-up | **Azure Communication Services (SMS)** | Independent of Entra External ID — sends a 6-digit OTP, verified against `PhoneVerifications`, own JWT issued. Built this way because Entra's phone-primary sign-up flow is still immature/custom-config-only. |
| Currency conversion | Frankfurter (api.frankfurter.dev) | Free, no API key, no quota — ECB-sourced. Scheduled refresh job, cached in `ExchangeRates`, not called per-request |
| Maps | Azure Maps | Static thumbnails for transport legs; geocoding for destinations |
| Report generation | SkiaSharp (image + PDF from one canvas) + a Razor HTML template | No headless browser dependency; covers itinerary report and memory summary |
| File storage | Azure Blob Storage | Trip photos, organised by userId/tripId |
| Infrastructure | Azure CLI scripts (`deployment-runbook.md`) | Bicep templates are a planned future improvement, not present yet |
| Testing | xUnit + Moq + bUnit | Target 80% API coverage; critical-path Blazor component tests |
| CI/CD | GitHub Actions → Azure App Service | Build/test on every push; deploy job added once Phase 2 is reached |
| Monitoring | Azure Application Insights + Serilog | |

---

## 6. Build sequence

1. **Shared → API → Web, dev-auth, SQLite.** Build and test everything in §2's
   Phase 1 column with zero Azure dependency — a fully runnable app locally. When
   scaffolding `TravelPlan.Web` specifically, tell Claude Code up front that
   navigating between screens needs a page-turn transition — this requires a shell
   that keeps outgoing/incoming views mounted during the transition rather than
   Blazor's default instant `NavigateTo()` swap, while still using real routes so
   trip share-links can deep-link to a screen. The exact animation feel (timing,
   easing, rotation) is fine to tune later; only the underlying shell structure is
   expensive to retrofit if it's skipped now.
2. **Deploy skeleton.** Stand up Azure App Service, Static Web Apps, Azure SQL;
   still using dev-auth. Confirms the deployment path works before auth complexity
   is added.
3. **Real auth.** Wire in Microsoft Entra External ID (email + Google) and the
   Azure Communication Services phone-OTP flow.
4. **MAUI.** Build the mobile app last — read/browse only in v1, shares the same
   API and models as Web.

Each phase should leave the app in a runnable state — the goal is to always have
something working rather than debugging every layer simultaneously.

---

## 8. Cost & scale strategy

Explicit goal: stay cheap at low usage, without a redesign needed to handle a
much larger user base later. This is mostly already true of decisions made
elsewhere in this doc — collected here for visibility:

| Component | How it stays cheap now | How it scales later |
|---|---|---|
| Azure SQL | Serverless tier, `auto-pause-delay 60` — bills near-zero when idle (with a few seconds' cold-start on the next request after a pause) | Scale up (S1→S2→S3) or move off serverless, no code change |
| App Service (API) | B1 Basic (~fixed low monthly cost, no scale-to-zero — App Service runs continuously unlike serverless SQL) | Autoscale rule on CPU adds instances automatically |
| Static Web Apps (Web) | Free tier; Blazor WASM runs client-side, zero server render cost per page | CDN-distributed by default, no redesign needed at higher traffic |
| Azure Communication Services (phone OTP) | 10DLC number: no separate monthly leasing fee, pay-per-SMS-sent only. (Toll-free would add a flat $2/month regardless of usage — avoided by using 10DLC.) Requires 1–3 weeks carrier registration before live, and a paid (non-trial) Azure subscription | Scales linearly with usage, no fixed floor |
| Azure Maps | Gen2 free tier covers meaningful monthly volume before any charge | Pay-as-you-go beyond the free allowance, no redesign |
| Frankfurter (FX) | Genuinely free — no API key, no card, no quota — and only called on a schedule (not per-request), so usage stays flat regardless of traffic | Cache table means user-facing load never hits the external API directly |
| Blob Storage (photos) | LRS Hot tier — pay per GB stored, negligible at small scale | Cost grows linearly with actual photo volume, not a step function |
| Microsoft Entra External ID | **Free for the first 50,000 monthly active users** (confirmed current pricing) | Only starts costing money well past early-stage usage — a large amount of headroom before this line item exists at all |

**The one component without a true zero-cost idle state is App Service** — it's
a fixed running cost even with no traffic, unlike serverless SQL. That's a
deliberate trade-off for a real, always-available API rather than one with
cold-start delay on every idle-then-used cycle; worth knowing it's there rather
than assuming everything scales to $0. The other place a small fixed cost could
sneak in is the ACS phone number — a 10DLC number avoids it, a toll-free number
doesn't (flat $2/month regardless of usage), which is why 10DLC is the
recommendation in `deployment-runbook.md` §5.

**Practical implication for the build**: none of the sessions above need to
change because of this — the low-cost choices (serverless SQL, scheduled FX
caching, App Service autoscale, free-tier Maps/Entra) were already the natural
choices for the architecture, not an added constraint bolted on afterward.

---

## 9. Accompanying files

| File | Status |
|---|---|
| `erd.md` | Updated — PlanItems, sharing tables, FX cache, phone verification, Entra field rename |
| `architecture.md` | Updated — Entra External ID rename, .NET 10, Blob Storage, ACS/Maps/FX external systems |
| `deployment-runbook.md` | Updated — Entra External ID, ACS, Azure Maps (Gen2), Blob Storage addendum, .NET 10 |
| `README.md` | Updated — accurate tagline, Entra rename, .NET 10 |
| `travelplan-home-mockup.html` | Working reference build of the home screen (see §4) |
| `claude-code-session-prompts.md` | 12 sessions covering every row in §2, plus the not-yet-scheduled Quick capture note |
| This document | Source of truth for scope, schema deltas, stack, cost strategy, and sequencing |
