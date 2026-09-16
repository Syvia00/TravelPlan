# Architecture — C4 Model (v2)

Updates `architecture.md` per `TravelPlan-Project-Plan-v2.md`. Diagrams use
[Mermaid C4 notation](https://mermaid.js.org/syntax/c4.html).

---

## Level 1 — System Context

```mermaid
C4Context
    title System Context — TravelPlan

    Person(traveller, "Traveller", "Plans trips, logs budgets and itineraries, reviews trip memories from any device.")

    System(travelplan, "TravelPlan", "Full-stack travel planning platform. Provides a REST API, a browser-based SPA, and a native mobile app.")

    System_Ext(externalId, "Microsoft Entra External ID", "Handles email and Google sign-in, issues JWT tokens consumed by the API and client apps.")
    System_Ext(acs, "Azure Communication Services", "Sends SMS one-time codes for phone-number sign-up, verified directly by the API.")
    System_Ext(maps, "Azure Maps", "Geocoding and static map images for transport legs and destinations.")
    System_Ext(fx, "Exchange rate provider", "Frankfurter (api.frankfurter.dev) — free, no key, polled on a schedule and cached by the API.")

    Rel(traveller, travelplan, "Uses", "HTTPS")
    Rel(travelplan, externalId, "Delegates email/Google authentication to", "HTTPS / OIDC")
    Rel(traveller, externalId, "Signs in via", "HTTPS / browser redirect or MSAL")
    Rel(travelplan, acs, "Sends phone verification codes via", "HTTPS")
    Rel(travelplan, maps, "Requests geocoding and map images from", "HTTPS")
    Rel(travelplan, fx, "Fetches exchange rates from", "HTTPS, scheduled")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

**Note**: Azure AD B2C, previously the auth provider here, has been unavailable for
new tenants since May 2025. Microsoft Entra External ID is the current equivalent
product and fills the same role.

---

## Level 2 — Container Diagram

```mermaid
C4Container
    title Container Diagram — TravelPlan

    Person(traveller, "Traveller", "Uses browser or mobile device.")

    System_Ext(externalId, "Microsoft Entra External ID", "Identity provider for email/Google sign-in. Issues JWT Bearer tokens.")
    System_Ext(acs, "Azure Communication Services", "Sends SMS one-time codes.")
    System_Ext(maps, "Azure Maps", "Geocoding + static map images.")
    System_Ext(fx, "Exchange rate provider", "External FX rate API.")

    System_Boundary(travelplan, "TravelPlan") {

        Container(web, "Blazor WebAssembly", "Blazor WASM / .NET 10", "Browser SPA. Renders the diary/book UI, the calendar, and reports. Authenticates via MSAL and calls the API with a Bearer token.")

        Container(mobile, "MAUI Mobile App", ".NET MAUI 10 / iOS & Android", "Native mobile client. Uses MSAL for auth and HttpClient to call the API. MVVM with CommunityToolkit.")

        Container(api, "REST API", "ASP.NET Core 10", "Exposes all domain operations over HTTP. Validates JWT tokens from Entra External ID. Also verifies phone-OTP codes directly (independent of Entra External ID) and issues its own JWT for phone-only accounts. Contains the report service (SkiaSharp), the FX rate refresh job, and the Azure Maps client.")

        ContainerDb(db, "Azure SQL Database", "SQL Server / EF Core 8", "Stores users, trips, plan items, accommodations, travel legs, budget items, trip memories, collaborators, share links, and the exchange-rate cache.")

        ContainerDb(blob, "Azure Blob Storage", "Storage account", "Trip photos attached to plan items, organised by userId/tripId.")
    }

    Rel(traveller, web,    "Opens in browser",        "HTTPS")
    Rel(traveller, mobile, "Uses on device",           "")

    Rel(web,    externalId, "Redirects to sign in / acquires token", "HTTPS / OIDC")
    Rel(mobile, externalId, "Acquires token interactively",          "HTTPS / MSAL")

    Rel(web,    api, "Calls",  "HTTPS / JSON, Bearer token")
    Rel(mobile, api, "Calls",  "HTTPS / JSON, Bearer token")

    Rel(api, externalId, "Validates JWT tokens against JWKS endpoint", "HTTPS")
    Rel(api, acs,   "Sends phone verification SMS via", "HTTPS")
    Rel(api, maps,  "Requests geocoding / static maps from", "HTTPS")
    Rel(api, fx,    "Fetches exchange rates on a schedule from", "HTTPS")
    Rel(api, db,    "Reads and writes via EF Core", "TCP 1433")
    Rel(api, blob,  "Uploads/reads trip photos via", "HTTPS")

    UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

---

## Key design decisions

| Decision | Choice | Rationale |
|---|---|---|
| Target framework | .NET 10 (LTS), not .NET 8 | .NET 8 reaches end of support November 10, 2026; .NET MAUI 8 and 9 are already out of support today — only .NET MAUI 10 (paired with .NET 10) is currently supported. Switched early, while only the shared library existed. |
| Shared library | `TravelPlan.Shared` referenced by all projects | Single source of truth for DTOs, models, and validators. Eliminates duplication between API and clients. |
| Auth strategy | Microsoft Entra External ID (prod) + dev-bypass handler (dev) | Azure AD B2C stopped accepting new tenants in May 2025; External ID is the current equivalent product, same architectural role. The dev bypass allows local development without configuring a tenant. |
| Phone sign-up | Custom OTP flow via Azure Communication Services, independent of Entra External ID | Entra's phone-primary sign-up is still immature/custom-config-only; a self-built OTP flow keeps the "quick signup" UX fully in our control. |
| Database per environment | SQLite (dev) / Azure SQL (prod) | Zero-config local setup; Azure SQL for production scalability and managed backups. |
| Plan items | Single `PlanItems` table with nullable `Date`/`Time` instead of separate itinerary and wishlist tables | One item moves between backlog → dated → timed states by filling in fields — matches the drag-and-drop UX without a schema change per state. |
| Sharing model | `TripCollaborators` (named invite) + `TripShareLinks` (anonymous link), each carrying a role | Mirrors Google Docs-style permissions — Owner/Editor/Viewer, with or without an account. |
| Report generation | SkiaSharp draws once to a canvas, producing both PNG and PDF; a separate Razor template produces the HTML view | Avoids a headless-browser dependency while still supporting three export formats (HTML, PDF, image) from one data source. |
| Mobile → API only | MAUI calls the REST API directly | Avoids a second backend surface area; the API is the single data contract for all clients. |
| CI solution filter | `TravelPlan.CI.slnf` excludes Mobile | MAUI requires Android/iOS SDK workloads not available on `ubuntu-latest` runners. |
| Report storage | JSON blob in `TripMemories.ReportData`, discriminated by `ReportType` | Avoids a schema migration every time the report structure evolves; one table serves both the itinerary report and the memory summary. |
