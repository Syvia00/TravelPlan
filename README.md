# TravelPlan

A full-stack travel planning application built with .NET 8. Plan trips, track
budgets, log itineraries, and generate trip memory reports — accessible from a
browser, a mobile device, or a shared link.

---

## Contents

- [Architecture overview](#architecture-overview)
- [Prerequisites](#prerequisites)
- [Project structure](#project-structure)
- [Local setup](#local-setup)
  - [1. TravelPlan.Api](#1-travelplanapi)
  - [2. TravelPlan.Web](#2-travelplanweb)
  - [3. TravelPlan.Mobile](#3-travelplanmobile)
  - [4. TravelPlan.Tests](#4-travelplantests)
- [Solution filters](#solution-filters)
- [Environment configuration](#environment-configuration)

---

## Architecture overview

| Layer | Technology | Hosts |
|---|---|---|
| REST API | ASP.NET Core 10, EF Core 10 | Azure App Service |
| Web frontend | Blazor WebAssembly | Azure Static Web Apps |
| Mobile app | .NET MAUI (iOS & Android) | Sideload / App Store |
| Shared library | .NET 8 class library | Referenced by all |
| Database | SQLite (dev) / Azure SQL (prod) | Azure SQL Database |
| Auth | Microsoft Entra External ID (email + Google) + a custom phone-OTP flow via Azure Communication Services | Azure |
| Reports | SkiaSharp (image/PDF) + a Razor HTML template — no external AI service | In-process |

---

## Prerequisites

| Tool | Minimum version | Notes |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` |
| .NET MAUI workload | 10.0 | `dotnet workload install maui` |
| Git | any | — |
| Visual Studio 2022 or Rider | — | optional but recommended |

> **Microsoft Entra External ID** is required only for production. Local development
> uses a dev-auth bypass (see [Environment configuration](#environment-configuration)).

---

## Project structure

```
TravelPlan/
├── TravelPlan.Shared/          # DTOs, domain models, FluentValidation validators
│   ├── DTOs/                   # Request/response records grouped by domain
│   └── Models/                 # EF entity classes + enums
│
├── TravelPlan.Api/             # ASP.NET Core 8 REST API
│   ├── Controllers/            # MVC controllers (one per aggregate)
│   ├── Data/                   # DbContext + EF Fluent-API configurations
│   ├── Endpoints/              # Minimal-API endpoint groups
│   ├── Middleware/             # UserSyncMiddleware
│   ├── Repositories/           # IRepository + EF implementations
│   └── Services/               # TripReportService (SkiaSharp), MapsService,
│                                # ExchangeRateService, PhoneOtpService
│
├── TravelPlan.Web/             # Blazor WebAssembly frontend
│   ├── Pages/                  # .razor page components (book/diary UI)
│   ├── Layout/                 # MainLayout + NavMenu
│   └── Services/               # API client, auth state provider
│
├── TravelPlan.Mobile/          # .NET MAUI iOS/Android app
│   ├── Pages/                  # XAML content pages
│   │   ├── Auth/               # LoginPage
│   │   ├── Trips/              # TripList, TripDetail, TripForm, MemoryReport
│   │   └── History/            # HistoryPage
│   ├── ViewModels/             # MVVM view models (CommunityToolkit.Mvvm)
│   ├── Services/               # AuthService (MSAL), ApiService (HttpClient)
│   └── Converters/             # IValueConverter implementations
│
├── TravelPlan.Tests/           # xUnit unit tests (80 %+ coverage target)
│
├── TravelPlan.sln              # Full solution (all 5 projects)
├── TravelPlan.CI.slnf          # CI solution filter (excludes Mobile)
└── .github/workflows/          # GitHub Actions CI pipeline
```

---

## Local setup

```bash
git clone https://github.com/<your-org>/TravelPlan.git
cd TravelPlan
```

### 1. TravelPlan.Api

The API uses **SQLite** in development — no database server required.

```bash
cd TravelPlan.Api
dotnet run
```

**Default URLs**

| Scheme | URL |
|---|---|
| HTTP | `http://localhost:5280` |
| HTTPS | `https://localhost:7277` |
| Swagger UI | `https://localhost:7277/swagger` |
| Health check | `https://localhost:7277/health` |

**Dev-auth bypass**

In Development mode the API registers a `DevAuthHandler` that automatically
authenticates every request as a fixed dev user — no Microsoft Entra External ID or
Azure Communication Services credentials are needed locally.

---

### 2. TravelPlan.Web

```bash
cd TravelPlan.Web
dotnet run
```

**Default URLs**

| Scheme | URL |
|---|---|
| HTTPS | `https://localhost:7156` (or similar — check terminal output) |

In `DEBUG` builds the web app uses `DevAuthStateProvider`, which returns a
pre-authenticated "Dev User" session without hitting Entra External ID.

The API base URL is read from `wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "https://localhost:7277/"
}
```

---

### 3. TravelPlan.Mobile

```bash
dotnet workload install maui

cd TravelPlan.Mobile
dotnet run -f net10.0-android
dotnet run -f net10.0-ios   # macOS only
```

**API endpoint**

Points to `https://travelplan-api.azurewebsites.net/` by default (`ApiService.cs`).
For local development, change `BaseUrl` to `https://localhost:7277/` and trust the
dev certificate:

```bash
dotnet dev-certs https --trust
```

**Auth**

`AuthService.cs` uses MSAL against Entra External ID. For local testing, temporarily
return a hard-coded bearer token from the dev API, or disable the auth header check.

---

### 4. TravelPlan.Tests

```bash
cd TravelPlan.Tests
dotnet test
```

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Uses an **EF Core in-memory database** — no external services required.

---

## Solution filters

| Filter | Projects included | Use when |
|---|---|---|
| `TravelPlan.sln` | All 5 projects | Full local development |
| `TravelPlan.CI.slnf` | Shared, Api, Web, Tests | CI pipeline (no MAUI workload needed) |

```bash
dotnet build TravelPlan.CI.slnf
```

---

## Environment configuration

### TravelPlan.Api — `appsettings.json`

| Key | Description |
|---|---|
| `ConnectionStrings:DefaultConnection` | Azure SQL connection string (production only) |
| `AzureAdExternalId:Instance` | External ID login endpoint, e.g. `https://<tenant>.ciamlogin.com` |
| `AzureAdExternalId:ClientId` | API app registration client ID |
| `AzureAdExternalId:Domain` | Tenant domain, e.g. `<tenant>.onmicrosoft.com` |
| `AzureAdExternalId:SignUpSignInPolicyId` | User flow name |
| `AzureAdExternalId:TenantId` | Tenant ID |
| `AzureCommunicationServices:ConnectionString` | ACS connection string, for phone-OTP SMS |
| `AzureMaps:SubscriptionKey` | Azure Maps key, for geocoding/static maps |
| `ExchangeRateApi:BaseUrl` | FX rate provider base URL (Frankfurter — free, no key, no quota) |

> In Development the `DefaultConnection` key is ignored — SQLite is used
> automatically, and the phone-OTP/Maps/FX settings are optional (those features
> are Phase 2+, not required to run the core app locally).

### TravelPlan.Mobile — `AuthService.cs` constants

| Constant | Description |
|---|---|
| `Tenant` | Entra External ID tenant, e.g. `<tenant>.onmicrosoft.com` |
| `ClientId` | Native client app registration ID |
| `SignInPolicy` | User flow name |
| `Scope` | API scope URI |

### GitHub Actions secrets

| Secret | Description |
|---|---|
| *(none required for CI)* | The CI pipeline only builds and runs tests |
